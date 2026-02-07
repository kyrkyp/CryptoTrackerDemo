namespace CryptoTracker.Core.Options;

public class CryptoApiOptions
{
    public string BaseUrl { get; set; } = "https://api.coingecko.com/api/v3/";
    public string VsCurrency { get; set; } = "eur";
    public int PerPage { get; set; } = 10;
    public string Order { get; set; } = "market_cap_desc";
    public bool Sparkline { get; set; }
    public int Precision { get; set; } = 2;
    public int CacheSeconds { get; set; } = 15;
}
