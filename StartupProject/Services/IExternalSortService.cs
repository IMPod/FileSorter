namespace SortingFile.Services;

public interface IExternalSortService
{
    Task<bool> SplitAndSortChunks(string inputFile, CancellationToken cancellationToken);
    Task MergeSortedChunks(List<string> chunkFiles, string outputFile, CancellationToken cancellationToken);
}
