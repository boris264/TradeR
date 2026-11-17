namespace TradeR.Services.Models.Market
{
    public class Order
    {
        public OrderType Type { get; set; }

        public double Amount { get; set; }

        public double Price { get; set; }
    }
}
