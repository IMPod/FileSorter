namespace Utilities.Constants;

public static class Setting
{
    public const string GENERATED_FILE = "C:\\temp\\big_parallel.txt";
    public const string ORDERED_FILE = "C:\\temp\\big_parallel_sort.txt";
    public const long TOTALLINES = 4_000_000_00;
    public const int CHUNKCOUNT = 8;
    public const long MAX_CHUNK_SIZE_BYTES = 50_000_000_0;// ~500MB
    public const int MAX_PARALLELSORTERS = 2;
}
