using TradeR.Services.Models.Market;
using TradeR.SharedKernel.Enums;

namespace Backtester.Models
{
    internal record Trade(
        string AssetPair,
        PositionDirection PositionDirection,
        Order BuyOrder,
        Order SellOrder,
        double PnL);
}
