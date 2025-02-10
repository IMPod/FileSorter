using FileGenerator.Models;
using FileGenerator.Services;

namespace FileSorter.Tests;
public class GeneraterServiceTests
{
    private readonly IGeneraterService _service;

    public GeneraterServiceTests()
    {
        _service = new GeneraterService();
    }

    [Fact]
    public async Task GenerateChunksInParallel_GeneratesCorrectNumberOfFiles()
    {
        // Arrange
        var config = new GeneratorConfig(
            TotalLines: 10,
            OutputPath: "output.txt",
            ChunkCount: 2
        );

        // Act
        string[] tempFiles = await _service.GenerateChunksInParallel(config);

        // Assert
        Assert.NotNull(tempFiles);
        Assert.Equal(config.ChunkCount, tempFiles.Length);
        foreach (var file in tempFiles)
        {
            Assert.True(File.Exists(file));
        }
    }

    [Fact]
    public async Task GenerateChunksInParallel_CorrectNumberOfLinesGenerated()
    {
        // Arrange
        var config = new GeneratorConfig(
            TotalLines: 10,
            OutputPath: "output.txt",
            ChunkCount: 2
        );

        // Act
        string[] tempFiles = await _service.GenerateChunksInParallel(config);

        // Assert
        long TotalLines = 0;
        foreach (var file in tempFiles)
        {
            TotalLines += File.ReadLines(file).LongCount();
        }
        Assert.Equal(config.TotalLines, TotalLines);
    }

    [Fact]
    public async Task ConcatenateFiles_MergesFilesCorrectly()
    {
        // Arrange
        var config = new GeneratorConfig(
            TotalLines: 4,
            OutputPath: "output.txt",
            ChunkCount: 2
        );

        string[] tempFiles = await _service.GenerateChunksInParallel(config);
        string OutputPath = Path.GetTempFileName();

        try
        {
            // Act
            await _service.ConcatenateFiles(tempFiles, config.OutputPath);

            // Assert
            long TotalLines = File.ReadLines(config.OutputPath).LongCount();
            Assert.Equal(config.TotalLines, TotalLines);

            foreach (string file in tempFiles)
            {
                var lines = File.ReadAllLines(file);
                foreach (string line in lines)
                {
                    Assert.Contains(line, File.ReadAllLines(config.OutputPath));
                }
            }
        }
        finally
        {
            if (File.Exists(config.OutputPath))
            {
                File.Delete(config.OutputPath);
            }
            foreach (string file in tempFiles)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
        }
    }

    [Fact]
    public void GenerateOneChunk_WritesCorrectNumberOfLines()
    {
        // Arrange
        var config = new GeneratorConfig(
            TotalLines: 10,
            OutputPath: "output.txt",
            ChunkCount: 1
        );
        string tempFile = Path.GetTempFileName();
        long countLines = 5;

        try
        {
            // Act
            ((GeneraterService)_service).GenerateOneChunk(tempFile, countLines, config);

            // Assert
            long linesInFile = File.ReadLines(tempFile).LongCount();
            Assert.Equal(countLines, linesInFile);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Validate_ThrowsExceptionOnInvalidTotalLines()
    {
        // Arrange & Act & Assert
        var config = new GeneratorConfig(
            TotalLines: -1,
            OutputPath: "output.txt"
        );

        var ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("TotalLines must be greater than zero", ex.Message);
    }

    [Fact]
    public void Validate_ThrowsExceptionOnEmptyOutputPath()
    {
        // Arrange & Act & Assert
        var config = new GeneratorConfig(
            TotalLines: 10,
            OutputPath: ""
        );

        var ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("OutputPath is not specified", ex.Message);
    }

    [Fact]
    public void Validate_ThrowsExceptionOnInvalidChunkCount()
    {
        // Arrange & Act & Assert
        var config = new GeneratorConfig(
            TotalLines: 10,
            OutputPath: "output.txt",
            ChunkCount: 0
        );

        var ex = Assert.Throws<ArgumentException>(() => config.Validate());
        Assert.Contains("ChunkCount must be greater than zero", ex.Message);
    }

    [Fact]
    public async Task ConcatenateFiles_ThrowsExceptionOnInvalidInput()
    {
        // Arrange
        string[] invalidFiles = { "invalid_file1.txt", "invalid_file2.txt" };
        string OutputPath = Path.GetTempFileName();

        try
        {
            // Act & Assert
            var exception = await Assert.ThrowsAsync<FileNotFoundException>(() => _service.ConcatenateFiles(invalidFiles, OutputPath));
            Assert.Contains("invalid_file1.txt", exception.Message);
        }
        finally
        {
            if (File.Exists(OutputPath))
            {
                File.Delete(OutputPath);
            }
        }
    }
}