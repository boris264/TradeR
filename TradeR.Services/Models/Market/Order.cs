namespace TradeR.Services.Models.Market
{
    public class Order
    {
        public OrderType Type { get; init; }

        public double Amount { get; set; }

        public double Price { get; set; }
    }
}
