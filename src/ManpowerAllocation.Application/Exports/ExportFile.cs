namespace ManpowerAllocation.Application.Exports;

/// <summary>A generated file ready to stream to the client.</summary>
/// <param name="FileName">Suggested download file name.</param>
/// <param name="ContentType">MIME type.</param>
/// <param name="Content">The file bytes.</param>
public sealed record ExportFile(string FileName, string ContentType, byte[] Content);
