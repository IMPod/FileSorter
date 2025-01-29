namespace Utilities.Constants;

public static class Setting
{
    public const int BIGBUFFERSIZE = 1_048_576;//1mb
    public const int SMALLBUFFERSIZE = 65536;//64kb

    /*
     *  
     *  "TOTALLINES": "400000000" ~ 10gb
        "MAX_CHUNK_SIZE_BYTES": "500000000", // ~500MB
        "MAX_PARALLELSORTERS": "8"

     *  [Sorting] Starting external sort...
        [Sorting] Starting chunk production...
        [Sorting] Chunk production completed.
        [Sorting] Starting parallel sorting...
        [Sorting] Parallel sorting completed.
        [Sorting] Starting k-way merge...
        [Sorting] Delete temp files. Elapsed: 00:22:38.4543061
        [Sorting] External sort completed in 00:22:38.9615911.
     *  
        [Generator] Will generate 4000000000 lines in 8 chunks.
        [Generator] Final file will be: C:\temp\big_parallel.txt
        [Generator] Chunk generation completed in 00:04:31.3565990.
        [Generator] Concatenating all chunks into a single file...
        [Generator] Done! Total time: 00:22:03.8313290.

        MAX_CHUNK_SIZE_BYTES = 50_000_000_0;
        MAX_PARALLELSORTERS = 2;

        Starting external sort...
        Chunks created. Elapsed: 01:40:37.5216843
        Starting k-way merge...
        External sort completed in 03:55:51.3447182.

     */
}
