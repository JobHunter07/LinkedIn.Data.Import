namespace LinkedIn.Data.Import.Cli.Pipeline;

/// <summary>
/// Context passed between pipeline steps containing all necessary data.
/// </summary>
public sealed class ImportContext
{
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public int ExitCode { get; set; } = 0;
    
    // Configuration
    public string ZipRootDirectory { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    
    // Extraction results
    public string ExtractionDirectory { get; set; } = string.Empty;
    public List<string> ExtractedFiles { get; set; } = [];
    
    // Import results
    public object? ImportResult { get; set; }
    
    // General purpose data storage for steps
    public Dictionary<string, object> Data { get; set; } = new();
}
