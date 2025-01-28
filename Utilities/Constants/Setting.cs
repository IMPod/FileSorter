namespace Utilities.Constants;

public static class Setting
{
    public const string GENERATED_FILE = "C:\\temp\\big_parallel.txt";
    public const string ORDERED_FILE = "C:\\temp\\big_parallel_sort.txt";
    public const long TOTALLINES = 4_000_000_00;
    public const int CHUNKCOUNT = 8;
    public const long MAX_CHUNK_SIZE_BYTES = 50_000_000_0;// ~500MB
    public const int MAX_PARALLELSORTERS = 4;

    /*
     *  
        public const long MAX_CHUNK_SIZE_BYTES = 50_000_000_0;
        public const int MAX_PARALLELSORTERS = 4;

        [Generator] Will generate 40000000 lines in 8 chunks.
        [Generator] Final file will be: C:\temp\big_parallel.txt
        [Generator] Chunk generation completed in 00:00:01.9326198.
        [Generator] Concatenating all chunks into a single file...
        [Generator] Done! Total time: 00:00:11.0010562.

        Starting external sort...
        Chunks created. Elapsed: 00:00:59.0117588
        Starting k-way merge...
        External sort completed in 00:01:41.9253273.




        [Generator] Will generate 40000000 lines in 10 chunks.
        [Generator] Final file will be: C:\temp\big_parallel.txt
        [Generator] Chunk generation completed in 00:00:01.9082571.
        [Generator] Concatenating all chunks into a single file...
        [Generator] Done! Total time: 00:00:10.6218803.

        MAX_CHUNK_SIZE_BYTES = 50_000_000_0;
        MAX_PARALLELSORTERS = 8;

        Starting external sort...
        Chunks created. Elapsed: 00:00:48.6476510
        Starting k-way merge...
        External sort completed in 00:01:27.7500698.


        MAX_CHUNK_SIZE_BYTES = 100_000_000_0;
        MAX_PARALLELSORTERS = 8;

        Starting external sort...
        Chunks created. Elapsed: 00:01:08.5632459
        Starting k-way merge...
        External sort completed in 00:01:43.9857495.
    -----------------------

        MAX_CHUNK_SIZE_BYTES = 50_000_000_0;
        MAX_PARALLELSORTERS = 8;

        [Generator] Will generate 400000000 lines in 8 chunks.
        [Generator] Final file will be: C:\temp\big_parallel.txt
        [Generator] Chunk generation completed in 00:00:12.6934353.
        [Generator] Concatenating all chunks into a single file...
        [Generator] Done! Total time: 00:01:45.3847963.

        Starting external sort...
        Chunks created. Elapsed: 00:13:43.9080654
        Starting k-way merge...
        External sort completed in 00:24:03.1965420.



        [Generator] Will generate 400000000 lines in 20 chunks.
        [Generator] Final file will be: C:\temp\big_parallel.txt
        [Generator] Chunk generation completed in 00:00:13.2845684.
        [Generator] Concatenating all chunks into a single file...
        [Generator] Done! Total time: 00:02:17.6631600.

        [Generator] Will generate 400000000 lines in 10 chunks.
        [Generator] Final file will be: C:\temp\big_parallel.txt
        [Generator] Chunk generation completed in 00:00:12.4102210.
        [Generator] Concatenating all chunks into a single file...
        [Generator] Done! Total time: 00:02:00.8039405.

        [Generator] Will generate 400000000 lines in 4 chunks.
        [Generator] Final file will be: C:\temp\big_parallel.txt
        [Generator] Chunk generation completed in 00:00:17.0741042.
        [Generator] Concatenating all chunks into a single file...
        [Generator] Done! Total time: 00:01:44.1119826.
    ------------------------
        MAX_CHUNK_SIZE_BYTES = 50_000_000_0;
        MAX_PARALLELSORTERS = 2;

        [Generator] Will generate 4000000000 lines in 8 chunks.
        [Generator] Final file will be: C:\temp\big_parallel.txt
        [Generator] Chunk generation completed in 00:04:31.3565990.
        [Generator] Concatenating all chunks into a single file...
        [Generator] Done! Total time: 00:22:03.8313290.

        Starting external sort...
        Chunks created. Elapsed: 01:40:37.5216843
        Starting k-way merge...
        External sort completed in 03:55:51.3447182.

     */
}
