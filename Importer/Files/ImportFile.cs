namespace Importer.Files
{
    /// <summary>A CSV file to import, with the asset pair (and OHLCVT interval) taken from its file name.</summary>
    internal sealed record ImportFile(string Path, string AssetPair, int? IntervalMinutes)
    {
        public string FileName => System.IO.Path.GetFileName(Path);
    }
}
