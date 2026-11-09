namespace Importer.Data
{
    internal readonly record struct FileImportResult(long RowsRead, long RowsInserted)
    {
        /// <summary>Rows that were already in the table (same key) and were left untouched.</summary>
        public long RowsSkipped => RowsRead - RowsInserted;
    }
}
