using ClosedXML.Excel;
using ManpowerAllocation.Application.Abstractions;
using ManpowerAllocation.Application.Models;
using ManpowerAllocation.Domain;
using ManpowerAllocation.Domain.Enums;

namespace ManpowerAllocation.Infrastructure.Import;

/// <summary>
/// Parses uploaded attendance and requirement workbooks with the ClosedXML library. Sheet
/// detection and column mapping follow the behaviour of the original dashboard so existing
/// spreadsheets continue to load, but the fragile hand-rolled ZIP/XLSX reader is replaced
/// with a maintained library.
/// </summary>
public sealed class ClosedXmlImportParser : IExcelImportParser
{
    /// <inheritdoc />
    public Task<IReadOnlyList<ImportedEmployeeRow>> ParseAttendanceAsync(Stream workbook, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        using var wb = new XLWorkbook(workbook);
        var rows = new List<ImportedEmployeeRow>();

        foreach (var sheet in wb.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var division = ClassifyAttendanceSheet(sheet.Name);
            if (division is null)
            {
                continue;
            }

            rows.AddRange(ExtractEmployees(sheet, division.Value));
        }

        return Task.FromResult<IReadOnlyList<ImportedEmployeeRow>>(rows);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ImportedRequirementRow>> ParseRequirementsAsync(Stream workbook, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        using var wb = new XLWorkbook(workbook);
        var rows = new List<ImportedRequirementRow>();

        foreach (var sheet in wb.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lower = sheet.Name.ToLowerInvariant();
            if (lower.Contains("brg", StringComparison.Ordinal))
            {
                rows.AddRange(ExtractRequirements(sheet, Division.Brg));
            }
            else if (IsMainRequirementsSheet(lower))
            {
                rows.AddRange(ExtractRequirements(sheet, Division.Egl));
            }
        }

        return Task.FromResult<IReadOnlyList<ImportedRequirementRow>>(rows);
    }

    /// <summary>Classifies an attendance sheet by its name, or returns null when it is not recognised.</summary>
    private static Division? ClassifyAttendanceSheet(string sheetName)
    {
        var lower = sheetName.ToLowerInvariant();

        if (lower.Contains("brg", StringComparison.Ordinal))
        {
            return Division.Brg;
        }

        if (lower is "operations" or "ops"
            || lower.Contains("operat", StringComparison.Ordinal)
            || lower.Contains("functional", StringComparison.Ordinal))
        {
            return Division.FunctionalSupport;
        }

        if (lower is "attendance" or "production" or "egl"
            || lower.Contains("attend", StringComparison.Ordinal)
            || lower.Contains("product", StringComparison.Ordinal))
        {
            return Division.Egl;
        }

        return null;
    }

    /// <summary>Returns true when a sheet name looks like the main requirements sheet.</summary>
    private static bool IsMainRequirementsSheet(string lowerName)
    {
        if (lowerName is "how to use" or "operations" or "attendance")
        {
            return false;
        }

        return lowerName.Contains("master req", StringComparison.Ordinal)
            || lowerName.Contains("requirement", StringComparison.Ordinal)
            || lowerName is "master" or "egl" or "req";
    }

    /// <summary>Extracts employee rows from a recognised attendance sheet.</summary>
    private static IEnumerable<ImportedEmployeeRow> ExtractEmployees(IXLWorksheet sheet, Division division)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastRow == 0 || lastCol == 0)
        {
            yield break;
        }

        var (headerRow, columns) = DetectAttendanceHeader(sheet, lastRow, lastCol);

        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var name = GetString(sheet, r, columns.Name);
            var badge = GetString(sheet, r, columns.Id);
            var departmentName = DepartmentName.Normalize(GetString(sheet, r, columns.Department));
            var shiftRaw = GetString(sheet, r, columns.Shift);
            var statusRaw = GetString(sheet, r, columns.Status);
            var notes = GetString(sheet, r, columns.Notes);

            var isSupply = string.Equals(name, "SUPPLY", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(badge, "SUPPLY", StringComparison.OrdinalIgnoreCase);

            var status = NormaliseStatus(statusRaw);

            // A shift cell holding a leave marker means the person is on vacation that day.
            if (shiftRaw.ToUpperInvariant() is "VACATION" or "VAC" or "LEAVE")
            {
                status = AttendanceStatus.OnVacation;
                shiftRaw = "DAY";
            }

            // A name carrying "VAC" (and not an outsource row) also indicates vacation.
            if (!isSupply && name.Contains("VAC", StringComparison.OrdinalIgnoreCase))
            {
                status = AttendanceStatus.OnVacation;
                name = name.Replace("VAC", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            }

            var shift = NormaliseShift(shiftRaw);

            // Drop rows that are missing any essential field or carry an unknown shift.
            if (string.IsNullOrWhiteSpace(name)
                || departmentName.Length == 0
                || shift is null
                || status is null)
            {
                continue;
            }

            yield return new ImportedEmployeeRow
            {
                Division = division,
                Name = name.Trim(),
                BadgeNumber = string.IsNullOrWhiteSpace(badge) ? null : badge.Trim(),
                DepartmentName = departmentName,
                Shift = shift.Value,
                Status = status.Value,
                IsSupply = isSupply,
                Notes = NormaliseNote(notes)
            };
        }
    }

    /// <summary>Detects the header row and column positions for an attendance sheet.</summary>
    private static (int HeaderRow, AttendanceColumns Columns) DetectAttendanceHeader(IXLWorksheet sheet, int lastRow, int lastCol)
    {
        var scanTo = Math.Min(8, lastRow);
        for (var r = 1; r <= scanTo; r++)
        {
            var hasName = false;
            for (var c = 1; c <= lastCol; c++)
            {
                if (GetString(sheet, r, c).Contains("NAME", StringComparison.OrdinalIgnoreCase))
                {
                    hasName = true;
                    break;
                }
            }

            if (hasName)
            {
                return (r, MapAttendanceColumns(sheet, r, lastCol));
            }
        }

        // No header row found: fall back to the fixed column order used by the source data.
        return (0, new AttendanceColumns(1, 2, 3, 4, 5, 6));
    }

    /// <summary>Maps attendance columns from a header row, keeping the fixed order as a per-column fallback.</summary>
    private static AttendanceColumns MapAttendanceColumns(IXLWorksheet sheet, int headerRow, int lastCol)
    {
        int name = 1, id = 2, dept = 3, shift = 4, status = 5, notes = 6;

        for (var c = 1; c <= lastCol; c++)
        {
            var header = GetString(sheet, headerRow, c).Trim().ToUpperInvariant();
            if (header.Length == 0)
            {
                continue;
            }

            if (header is "NAME" or "EMPLOYEE" or "EMPLOYEE NAME")
            {
                name = c;
            }
            else if (header is "ID" or "EMP ID" or "EMPID" or "EMPLOYEE ID")
            {
                id = c;
            }
            else if (header.Contains("DEPT", StringComparison.Ordinal) || header.Contains("DEPART", StringComparison.Ordinal) || header == "SECTION")
            {
                dept = c;
            }
            else if (header is "SHIFT" or "SHIFT TYPE")
            {
                shift = c;
            }
            else if (header.Contains("AVAIL", StringComparison.Ordinal) || header is "STATUS" or "PRESENT")
            {
                status = c;
            }
            else if (header is "NOTES" or "NOTE" or "REMARK" or "REMARKS" or "DESIGNATION" or "ROLE" or "POSITION")
            {
                notes = c;
            }
        }

        return new AttendanceColumns(name, id, dept, shift, status, notes);
    }

    /// <summary>Extracts requirement rows from a recognised requirements sheet.</summary>
    private static IEnumerable<ImportedRequirementRow> ExtractRequirements(IXLWorksheet sheet, Division division)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastRow == 0 || lastCol == 0)
        {
            yield break;
        }

        var layout = DetectRequirementLayout(sheet, lastRow, lastCol);
        if (layout is null)
        {
            yield break;
        }

        for (var r = layout.Value.HeaderRow + 1; r <= lastRow; r++)
        {
            var name = DepartmentName.Normalize(GetString(sheet, r, layout.Value.DeptColumn));
            if (name.Length == 0 || IsRequirementNoiseRow(name))
            {
                continue;
            }

            if (!TryGetInt(sheet, r, layout.Value.DayColumn, out var day))
            {
                continue;
            }

            // Night requirement defaults to the day figure when the cell is blank.
            var night = TryGetInt(sheet, r, layout.Value.NightColumn, out var n) ? n : day;

            // Resolve division from the Category column when present; otherwise use the sheet default.
            var rowDivision = layout.Value.CategoryColumn > 0
                ? MapCategoryToDivision(GetString(sheet, r, layout.Value.CategoryColumn), division)
                : division;

            yield return new ImportedRequirementRow
            {
                Division = rowDivision,
                DepartmentName = name,
                RequiredDay = day,
                RequiredNight = night
            };
        }
    }

    /// <summary>Locates the department / day / night columns and header row in a requirements sheet.</summary>
    private static RequirementLayout? DetectRequirementLayout(IXLWorksheet sheet, int lastRow, int lastCol)
    {
        var scanTo = Math.Min(10, lastRow);
        for (var r = 1; r <= scanTo; r++)
        {
            for (var c = 1; c <= lastCol; c++)
            {
                if (!GetString(sheet, r, c).Trim().Equals("DEPARTMENT", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int? dayCol = null, nightCol = null;
                var upper = Math.Min(lastCol, c + 4);
                for (var cc = c + 1; cc <= upper; cc++)
                {
                    var header = GetString(sheet, r, cc).ToUpperInvariant();
                    if (dayCol is null && header.Contains("DAY", StringComparison.Ordinal))
                    {
                        dayCol = cc;
                    }
                    else if (nightCol is null && header.Contains("NIGHT", StringComparison.Ordinal))
                    {
                        nightCol = cc;
                    }
                }

                if (dayCol is not null && nightCol is not null)
                {
                    // The optional Category column (any column in the header row) decides each
                    // department's division; without it, the sheet's default division is used.
                    var categoryCol = FindHeaderColumn(sheet, r, lastCol, "CATEGORY");
                    return new RequirementLayout(r, c, dayCol.Value, nightCol.Value, categoryCol);
                }
            }
        }

        // Fallback: assume the fixed column order Department / Day / Night with no header/category.
        return new RequirementLayout(0, 1, 2, 3, 0);
    }

    /// <summary>Normalises a notes cell, treating blanks and the literal placeholders "None"/"N/A" as no note.</summary>
    private static string? NormaliseNote(string raw)
    {
        var value = raw.Trim();
        if (value.Length == 0 || value.Equals("None", StringComparison.OrdinalIgnoreCase) || value.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value;
    }

    /// <summary>Finds the column in the header row whose text equals the supplied label, or 0 if absent.</summary>
    private static int FindHeaderColumn(IXLWorksheet sheet, int headerRow, int lastCol, string label)
    {
        for (var c = 1; c <= lastCol; c++)
        {
            if (GetString(sheet, headerRow, c).Trim().Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                return c;
            }
        }

        return 0;
    }

    /// <summary>
    /// Maps a requirements "Category" value (e.g. PRODUCTION / FUNCTIONAL SUPPORT / BRG) to a division,
    /// falling back to the sheet's default division when the value is unrecognised.
    /// </summary>
    private static Division MapCategoryToDivision(string category, Division fallback)
    {
        var value = category.Trim().ToUpperInvariant();
        if (value.Length == 0)
        {
            return fallback;
        }

        if (value.Contains("BRG", StringComparison.Ordinal))
        {
            return Division.Brg;
        }

        if (value.Contains("SUPPORT", StringComparison.Ordinal)
            || value.Contains("FUNCTIONAL", StringComparison.Ordinal)
            || value.Contains("OPERATION", StringComparison.Ordinal)
            || value == "OPS")
        {
            return Division.FunctionalSupport;
        }

        if (value.Contains("PRODUCTION", StringComparison.Ordinal) || value.Contains("EGL", StringComparison.Ordinal))
        {
            return Division.Egl;
        }

        return fallback;
    }

    /// <summary>Returns true for rows that are headers, instructions or totals rather than data.</summary>
    private static bool IsRequirementNoiseRow(string departmentName)
    {
        string[] markers =
        {
            "DEPARTMENT", "REQUIRED", "MASTER", "TOTAL", "HOW TO", "STEP",
            "EDIT", "DO NOT", "DAILY", "NAME", "ID", "CATEGORY", "BRG DEPT"
        };

        return markers.Any(m => departmentName.Contains(m, StringComparison.Ordinal));
    }

    /// <summary>Normalises a raw shift string to a <see cref="ShiftType"/>, or null when unknown.</summary>
    private static ShiftType? NormaliseShift(string raw)
    {
        var value = raw.Trim().ToUpperInvariant();
        return value switch
        {
            "D" or "DAY" or "DAY SHIFT" or "DAYSHIFT" => ShiftType.Day,
            "N" or "NIGHT" or "NIGHT SHIFT" or "NIGHTSHIFT" => ShiftType.Night,
            _ => null
        };
    }

    /// <summary>Normalises a raw status string to an <see cref="AttendanceStatus"/>, or null when unknown.</summary>
    private static AttendanceStatus? NormaliseStatus(string raw)
    {
        var value = raw.Trim().ToUpperInvariant();
        return value switch
        {
            "Y" or "YES" or "PRESENT" or "P" => AttendanceStatus.Present,
            "N" or "NO" or "ABSENT" or "A" => AttendanceStatus.Absent,
            "V" or "VAC" or "VACATION" or "LEAVE" => AttendanceStatus.OnVacation,
            _ => null
        };
    }

    /// <summary>Reads a cell as a trimmed string, returning an empty string for blank cells.</summary>
    private static string GetString(IXLWorksheet sheet, int row, int column)
    {
        if (column <= 0)
        {
            return string.Empty;
        }

        return sheet.Cell(row, column).GetString().Trim();
    }

    /// <summary>Attempts to read a cell as a non-negative integer.</summary>
    private static bool TryGetInt(IXLWorksheet sheet, int row, int column, out int value)
    {
        value = 0;
        if (column <= 0)
        {
            return false;
        }

        var cell = sheet.Cell(row, column);
        if (cell.TryGetValue<double>(out var number))
        {
            value = (int)Math.Round(number);
            return true;
        }

        var text = cell.GetString().Trim();
        if (double.TryParse(text, out var parsed))
        {
            value = (int)Math.Round(parsed);
            return true;
        }

        return false;
    }

    /// <summary>The resolved column positions (1-based) for an attendance sheet.</summary>
    private readonly record struct AttendanceColumns(int Name, int Id, int Department, int Shift, int Status, int Notes);

    /// <summary>The resolved header row and column positions (1-based) for a requirements sheet.</summary>
    private readonly record struct RequirementLayout(int HeaderRow, int DeptColumn, int DayColumn, int NightColumn, int CategoryColumn);
}
