namespace FileSorter.StartupProject.Models;

public class SortingConfig(
    string InputPath,
    string OutputPath,
    long MaxChunkSizeBytes,
    int MaxParallelSorters
)
{
    public string InputPath { get; } = InputPath;
    public string OutputPath { get; } = OutputPath;
    public long MaxChunkSizeBytes { get; } = MaxChunkSizeBytes;
    public int MaxParallelSorters { get; } = MaxParallelSorters;


    /// <summary>
    /// Validates that all config parameters are reasonable.
    /// </summary>
    public void Validate()
    {
        if (MaxChunkSizeBytes <= 0)
            throw new ArgumentException("MaxChunkSizeBytes must be greater than zero.");
        if (string.IsNullOrWhiteSpace(OutputPath))
            throw new ArgumentException("OutputPath is not specified.");
        if (string.IsNullOrWhiteSpace(InputPath))
            throw new ArgumentException("InputPath is not specified.");
        if (MaxParallelSorters <= 0)
            throw new ArgumentException("MaxParallelSorters must be greater than zero.");
    }
}
