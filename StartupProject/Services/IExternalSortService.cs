namespace SortingFile.Services;

internal interface IExternalSortService
{
    Task<List<string>> SplitAndSortChunks(string inputFile);
    Task MergeSortedChunks(List<string> chunkFiles, string outputFile);
}
