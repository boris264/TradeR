namespace Importer.Models
{
    internal readonly record struct TradeRow(
        long TradeId,
        double Timestamp,
        double Price,
        double Volume,
        int TradeTypeId,
        int OrderTypeId);
}
