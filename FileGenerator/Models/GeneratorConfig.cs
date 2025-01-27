namespace FileGenerator.Models;

/// <summary>
/// Configuration class using C# 12 primary constructor.
/// It holds settings for file generation.
/// </summary>
public class GeneratorConfig(
    long TotalLines,    // Total number of lines to generate
    string OutputPath,  // Final output file path
    int ChunkCount = 8  // Number of chunks to split into (default 8)
)
{
    // Properties automatically bound to primary constructor parameters
    public long TotalLines { get; } = TotalLines;
    public string OutputPath { get; } = OutputPath;
    public int ChunkCount { get; } = ChunkCount;

    // Example texts to randomly choose from; using a C# 12 collection expression
    public string[] SampleTexts { get; } = [
            "Apple",
            "Banana is yellow",
            "Cherry is the best",
            "Something something something",
            "Hello World",
            "Lorem ipsum"
    ];

    /// <summary>
    /// Validates that all config parameters are reasonable.
    /// </summary>
    public void Validate()
    {
        if (TotalLines <= 0)
            throw new ArgumentException("TotalLines must be greater than zero.");
        if (string.IsNullOrWhiteSpace(OutputPath))
            throw new ArgumentException("OutputPath is not specified.");
        if (ChunkCount <= 0)
            throw new ArgumentException("ChunkCount must be greater than zero.");
    }
}