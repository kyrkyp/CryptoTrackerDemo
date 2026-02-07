namespace CryptoTracker.Models;

public class Coin
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public double Change24H { get; set; }
    public decimal MarketCap { get; set; }
    public decimal Volume24H { get; set; }
    public List<decimal> Sparkline7D { get; set; } = new();
    public string ApiId { get; set; } = string.Empty;
}