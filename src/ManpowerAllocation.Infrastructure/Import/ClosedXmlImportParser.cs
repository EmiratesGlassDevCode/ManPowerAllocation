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
    public Task<IReadOnlyList<ImportedEmployeeRow>> ParseAttendanceEditsAsync(Stream workbook, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        using var wb = new XLWorkbook(workbook);
        var rows = new List<ImportedEmployeeRow>();

        foreach (var sheet in wb.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // "Lists" is the hidden helper sheet the export writes to back the dropdowns; it holds
            // reference values, not employee rows, so it must never be parsed as data.
            if (string.Equals(sheet.Name, "Lists", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            rows.AddRange(ExtractEditedEmployees(sheet));
        }

        return Task.FromResult<IReadOnlyList<ImportedEmployeeRow>>(rows);
    }

    /// <summary>
    /// Extracts edited employee rows from a workbook downloaded from the Attendance export. Unlike
    /// the seed path, the division comes from the row's Division column, the outsource column sets
    /// the supply flag, and the Ref column (employee id) is captured for exact matching. A header
    /// row is required — without recognised headers the sheet is skipped rather than guessed.
    /// </summary>
    private static IEnumerable<ImportedEmployeeRow> ExtractEditedEmployees(IXLWorksheet sheet)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastRow == 0 || lastCol == 0)
        {
            yield break;
        }

        var headerRow = 0;
        var scanTo = Math.Min(8, lastRow);
        for (var r = 1; r <= scanTo && headerRow == 0; r++)
        {
            for (var c = 1; c <= lastCol; c++)
            {
                if (GetString(sheet, r, c).Contains("NAME", StringComparison.OrdinalIgnoreCase))
                {
                    headerRow = r;
                    break;
                }
            }
        }

        if (headerRow == 0)
        {
            yield break;
        }

        var cols = MapEditColumns(sheet, headerRow, lastCol);
        if (cols.Name == 0)
        {
            yield break;
        }

        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var name = GetString(sheet, r, cols.Name);
            var shift = NormaliseShift(GetString(sheet, r, cols.Shift));
            var status = NormaliseStatus(GetString(sheet, r, cols.Status));

            // A row needs at least a name, a recognised shift and a recognised status to be an edit.
            if (string.IsNullOrWhiteSpace(name) || shift is null || status is null)
            {
                continue;
            }

            int? refId = null;
            if (cols.Ref > 0 && TryGetInt(sheet, r, cols.Ref, out var idValue) && idValue > 0)
            {
                refId = idValue;
            }

            var badge = GetString(sheet, r, cols.Id);
            var isSupply = IsAffirmative(GetString(sheet, r, cols.Outsource))
                           || string.Equals(name, "SUPPLY", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(badge, "SUPPLY", StringComparison.OrdinalIgnoreCase);

            yield return new ImportedEmployeeRow
            {
                Ref = refId,
                Division = ParseDivision(GetString(sheet, r, cols.Division)) ?? Division.Egl,
                Name = name.Trim(),
                BadgeNumber = string.IsNullOrWhiteSpace(badge) ? null : badge.Trim(),
                DepartmentName = DepartmentName.Normalize(GetString(sheet, r, cols.Department)),
                Shift = shift.Value,
                Status = status.Value,
                IsSupply = isSupply,
                Notes = NormaliseNote(GetString(sheet, r, cols.Notes))
            };
        }
    }

    /// <summary>Maps the columns of an edited Attendance export by header text.</summary>
    private static EditColumns MapEditColumns(IXLWorksheet sheet, int headerRow, int lastCol)
    {
        int reference = 0, name = 0, id = 0, division = 0, dept = 0, shift = 0, status = 0, outsource = 0, notes = 0;

        for (var c = 1; c <= lastCol; c++)
        {
            var header = GetString(sheet, headerRow, c).Trim().ToUpperInvariant();
            if (header.Length == 0)
            {
                continue;
            }

            if (header is "REF" or "REF ID" or "REFID" or "ROW ID" or "ROWID" or "EMP REF")
            {
                reference = c;
            }
            // Match "NAME" as a substring (FULL NAME, EMP NAME, …) to stay consistent with the
            // header-row detection, which also uses Contains("NAME"); an exact-match-only list here
            // let a recognised header map to no column and silently dropped the whole sheet.
            else if (header.Contains("NAME", StringComparison.Ordinal) || header is "EMPLOYEE")
            {
                name = c;
            }
            else if (header is "ID" or "EMP ID" or "EMPID" or "EMPLOYEE ID" or "BADGE" or "BADGE NUMBER")
            {
                id = c;
            }
            else if (header is "DIVISION" or "CATEGORY")
            {
                division = c;
            }
            else if (header.Contains("DEPART", StringComparison.Ordinal) || header.Contains("DEPT", StringComparison.Ordinal) || header == "SECTION")
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
            else if (header.Contains("OUTSOURCE", StringComparison.Ordinal) || header.Contains("SUPPLY", StringComparison.Ordinal))
            {
                outsource = c;
            }
            else if (header is "NOTES" or "NOTE" or "REMARK" or "REMARKS" or "DESIGNATION" or "ROLE" or "POSITION")
            {
                notes = c;
            }
        }

        return new EditColumns(reference, name, id, division, dept, shift, status, outsource, notes);
    }

    /// <summary>Parses a division label (as written by the export) to a <see cref="Division"/>, or null when unknown.</summary>
    private static Division? ParseDivision(string raw)
    {
        var value = raw.Trim().ToUpperInvariant();
        if (value.Length == 0)
        {
            return null;
        }

        if (value.Contains("BRG", StringComparison.Ordinal))
        {
            return Division.Brg;
        }

        if (value.Contains("FUNCTION", StringComparison.Ordinal) || value.Contains("SUPPORT", StringComparison.Ordinal) || value.Contains("OPERAT", StringComparison.Ordinal) || value == "FS")
        {
            return Division.FunctionalSupport;
        }

        if (value.Contains("EGL", StringComparison.Ordinal) || value.Contains("PRODUCT", StringComparison.Ordinal))
        {
            return Division.Egl;
        }

        return null;
    }

    /// <summary>Returns true for an affirmative cell (YES / Y / TRUE / 1) used by the outsource column.</summary>
    private static bool IsAffirmative(string raw)
    {
        var value = raw.Trim().ToUpperInvariant();
        return value is "YES" or "Y" or "TRUE" or "1";
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
                           || string.Equals(badge, "SUPPLY", StringComparison.OrdinalIgnoreCase)
                           || (columns.Outsource > 0 && IsAffirmative(GetString(sheet, r, columns.Outsource)));

            // Prefer the row's own Division column (the app's single-sheet export carries one) so a
            // multi-division roster lands in the right divisions; fall back to the sheet's division.
            var rowDivision = columns.Division > 0
                ? ParseDivision(GetString(sheet, r, columns.Division)) ?? division
                : division;

            var status = NormaliseStatus(statusRaw);

            // A shift cell holding a leave marker means the person is on vacation that day.
            if (shiftRaw.ToUpperInvariant() is "VACATION" or "VAC" or "LEAVE")
            {
                status = AttendanceStatus.OnVacation;
                shiftRaw = "DAY";
            }

            // A name carrying a standalone "VAC"/"VACATION" token (and not an outsource row)
            // also indicates vacation. Match whole tokens only — a substring match would
            // corrupt legitimate names such as "VACANT POST" (→ "ANT POST") or "AVACHIAN".
            if (!isSupply)
            {
                var tokens = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                var isVacationToken = tokens.Any(t =>
                    t.Equals("VAC", StringComparison.OrdinalIgnoreCase)
                    || t.Equals("VACATION", StringComparison.OrdinalIgnoreCase));

                if (isVacationToken)
                {
                    status = AttendanceStatus.OnVacation;
                    name = string.Join(' ', tokens.Where(t =>
                        !t.Equals("VAC", StringComparison.OrdinalIgnoreCase)
                        && !t.Equals("VACATION", StringComparison.OrdinalIgnoreCase))).Trim();
                }
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
                Division = rowDivision,
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
        return (0, new AttendanceColumns(1, 2, 0, 3, 4, 5, 0, 6));
    }

    /// <summary>Maps attendance columns from a header row, keeping the fixed order as a per-column fallback.</summary>
    private static AttendanceColumns MapAttendanceColumns(IXLWorksheet sheet, int headerRow, int lastCol)
    {
        int name = 1, id = 2, division = 0, dept = 3, shift = 4, status = 5, outsource = 0, notes = 6;

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
            else if (header is "ID" or "EMP ID" or "EMPID" or "EMPLOYEE ID" or "BADGE" or "BADGE NUMBER")
            {
                id = c;
            }
            else if (header is "DIVISION" or "CATEGORY")
            {
                division = c;
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
            else if (header.Contains("OUTSOURCE", StringComparison.Ordinal) || header.Contains("SUPPLY", StringComparison.Ordinal))
            {
                outsource = c;
            }
            else if (header is "NOTES" or "NOTE" or "REMARK" or "REMARKS" or "DESIGNATION" or "ROLE" or "POSITION")
            {
                notes = c;
            }
        }

        return new AttendanceColumns(name, id, division, dept, shift, status, outsource, notes);
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
        var value = departmentName.Trim().ToUpperInvariant();
        if (value.Length == 0)
        {
            return true;
        }

        // Multi-word instruction/label phrases are unambiguous, so a substring match is safe.
        string[] phrases = { "HOW TO", "DO NOT", "BRG DEPT", "MASTER REQUIREMENT" };
        if (phrases.Any(p => value.Contains(p, StringComparison.Ordinal)))
        {
            return true;
        }

        // Short header labels must match a WHOLE word, never a substring, so real department
        // names such as "LIQUID GLASS" (contains "ID") or "SOLID LINE" are not discarded.
        string[] wordMarkers =
        {
            "DEPARTMENT", "REQUIRED", "MASTER", "TOTAL", "STEP",
            "EDIT", "DAILY", "NAME", "ID", "CATEGORY"
        };
        var tokens = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Any(t => wordMarkers.Contains(t));
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
        if (double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            value = (int)Math.Round(parsed);
            return true;
        }

        return false;
    }

    /// <summary>The resolved column positions (1-based) for an attendance sheet.</summary>
    private readonly record struct AttendanceColumns(int Name, int Id, int Division, int Department, int Shift, int Status, int Outsource, int Notes);

    /// <summary>The resolved column positions (1-based) for an edited Attendance export. 0 means absent.</summary>
    private readonly record struct EditColumns(int Ref, int Name, int Id, int Division, int Department, int Shift, int Status, int Outsource, int Notes);

    /// <summary>The resolved header row and column positions (1-based) for a requirements sheet.</summary>
    private readonly record struct RequirementLayout(int HeaderRow, int DeptColumn, int DayColumn, int NightColumn, int CategoryColumn);
}
