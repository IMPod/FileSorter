using FileSorter.StartupProject.Models;
using SortingFile.Services;

namespace FileSorter.Tests;
public class ExternalSortServiceTests
{
    private SortingConfig _config;
    private readonly ExternalSortService _service;

    public ExternalSortServiceTests()
    {
        _config = new SortingConfig(
            InputPath: "test_input.txt",
            OutputPath: "sorted_output.txt",
            MaxChunkSizeBytes: 1024 * 1024,
            MaxParallelSorters: 4
        );
        _service = new ExternalSortService(_config);
    }

    [Fact]
    public async Task FullSortingProcess_ShouldProduceSortedOutput()
    {
        // Arrange
        await File.WriteAllLinesAsync(_config.InputPath, new[] { "3. C", "1. A", "2. B", "5. E", "4. D" });

        // Act
        bool result = await _service.SplitAndSortChunks(_config.InputPath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(_config.OutputPath));

        var sortedLines = await File.ReadAllLinesAsync(_config.OutputPath);
        Assert.Equal(new[] { "1. A", "2. B", "3. C", "4. D", "5. E" }, sortedLines);

        // Cleanup
        File.Delete(_config.InputPath);
        File.Delete(_config.OutputPath);
    }

    [Fact]
    public async Task SortingProcess_ShouldHandleEmptyFile()
    {
        // Arrange
        await File.WriteAllLinesAsync(_config.InputPath, Array.Empty<string>());

        // Act
        bool result = await _service.SplitAndSortChunks(_config.InputPath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(_config.OutputPath));
        var sortedLines = await File.ReadAllLinesAsync(_config.OutputPath);
        Assert.Empty(sortedLines);

        // Cleanup
        File.Delete(_config.InputPath);
        File.Delete(_config.OutputPath);
    }

    [Fact]
    public void SortingConfig_Validate_ShouldThrowException_WhenConfigIsInvalid()
    {
        // Arrange
        var invalidConfig = new SortingConfig("", "", 0, 0);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => invalidConfig.Validate());
    }

    [Fact]
    public async Task SortingProcess_ShouldSkipInvalidLines()
    {
        // Arrange
        await File.WriteAllLinesAsync(_config.InputPath, new[] { "3. C", "Invalid line", "2. B" });

        // Act
        bool result = await _service.SplitAndSortChunks(_config.InputPath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(_config.OutputPath));
        var sortedLines = await File.ReadAllLinesAsync(_config.OutputPath);
        Assert.Equal(new[] { "2. B", "3. C" }, sortedLines);

        // Cleanup
        File.Delete(_config.InputPath);
        File.Delete(_config.OutputPath);
    }

    [Fact]
    public async Task SortingProcess_ShouldHandleLargeFile()
    {
        // Arrange
        var largeInput = Enumerable.Range(1, 10000).Select(i => $"{i}. Line").ToArray();
        await File.WriteAllLinesAsync(_config.InputPath, largeInput);

        // Act
        bool result = await _service.SplitAndSortChunks(_config.InputPath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(_config.OutputPath));
        var sortedLines = await File.ReadAllLinesAsync(_config.OutputPath);
        var expectedSorted = largeInput.OrderBy(line => int.Parse(line.Split('.')[0])).ToArray();
        Assert.Equal(expectedSorted, sortedLines);

        // Cleanup
        File.Delete(_config.InputPath);
        File.Delete(_config.OutputPath);
    }

    [Fact]
    public async Task SortingProcess_ShouldRunWithMultipleSorters()
    {
        // Arrange
        _config = new SortingConfig(_config.InputPath, _config.OutputPath, _config.MaxChunkSizeBytes, 8);
        var inputLines = new[] { "3. C", "1. A", "2. B", "5. E", "4. D" };
        await File.WriteAllLinesAsync(_config.InputPath, inputLines);

        // Act
        bool result = await _service.SplitAndSortChunks(_config.InputPath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(_config.OutputPath));
        var sortedLines = await File.ReadAllLinesAsync(_config.OutputPath);
        Assert.Equal(new[] { "1. A", "2. B", "3. C", "4. D", "5. E" }, sortedLines);

        // Cleanup
        File.Delete(_config.InputPath);
        File.Delete(_config.OutputPath);
    }
}
