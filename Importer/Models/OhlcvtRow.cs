namespace Importer.Models
{
    internal readonly record struct OhlcvtRow(
        long Timestamp,
        double Open,
        double High,
        double Low,
        double Close,
        double Volume,
        int Trades);
}
