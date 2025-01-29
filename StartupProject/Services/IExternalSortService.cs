namespace SortingFile.Services;

public interface IExternalSortService
{
    Task<bool> SplitAndSortChunks(string inputFile);
    Task MergeSortedChunks(List<string> chunkFiles, string outputFile);
    bool IsOutputFileSorted(string outputFile);
}
