namespace CryptoTracker.Models;

public class Coin
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public double Change24H { get; set; }
}