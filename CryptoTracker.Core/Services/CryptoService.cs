using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CryptoTracker.Core.Options;
using CryptoTracker.Core.Services.Interfaces;
using CryptoTracker.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CryptoTracker.Core.Services;

public class CryptoService : ICryptoService
{
    private readonly HttpClient _httpClient;
    private readonly CryptoApiOptions _options;
    private readonly IMemoryCache _cache;

    public CryptoService(HttpClient httpClient, IOptions<CryptoApiOptions> options, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _cache = cache;
    }

    public async Task<List<Coin>> GetCoinsAsync(string? vsCurrency = null, bool? sparkline = null, int? perPage = null)
    {
        var resolvedCurrency = string.IsNullOrWhiteSpace(vsCurrency) ? _options.VsCurrency : vsCurrency.Trim().ToLowerInvariant();
        var resolvedSparkline = sparkline ?? _options.Sparkline;
        var resolvedPerPage = perPage.HasValue && perPage.Value > 0 ? perPage.Value : _options.PerPage;
        var cacheKey = $"coingecko:{resolvedCurrency}:{resolvedPerPage}:{_options.Order}:{_options.Precision}:{resolvedSparkline}";
        List<Coin>? cachedCoins = null;

        if (_options.CacheSeconds > 0 && _cache.TryGetValue(cacheKey, out List<Coin>? cachedValue))
        {
            cachedCoins = cachedValue;
            return cachedCoins ?? new List<Coin>();
        }

        var query = new Dictionary<string, string>
        {
            ["vs_currency"] = resolvedCurrency,
            ["order"] = _options.Order,
            ["per_page"] = resolvedPerPage.ToString(),
            ["page"] = "1",
            ["sparkline"] = resolvedSparkline.ToString().ToLowerInvariant(),
            ["price_change_percentage"] = "24h",
            ["precision"] = _options.Precision.ToString()
        };

        var url = "coins/markets?" + string.Join("&", query.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));

        using var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && cachedCoins is not null)
            {
                return cachedCoins;
            }

            return new List<Coin>();
        }

        var payload = await response.Content.ReadFromJsonAsync<List<CoinGeckoMarketDto>>();
        if (payload is null)
        {
            return new List<Coin>();
        }

        var coins = payload.Select(dto => new Coin
            {
                Id = Guid.NewGuid(),
                ApiId = dto.Id ?? string.Empty,
                Name = dto.Name ?? string.Empty,
                Symbol = (dto.Symbol ?? string.Empty).ToUpperInvariant(),
                Price = dto.CurrentPrice,
                Change24H = dto.PriceChangePercentage24H ?? 0,
                MarketCap = dto.MarketCap ?? 0,
                Volume24H = dto.TotalVolume ?? 0,
                Sparkline7D = dto.SparklineIn7D?.Price ?? new List<decimal>()
            })
            .ToList();

        if (_options.CacheSeconds > 0)
        {
            _cache.Set(cacheKey, coins, TimeSpan.FromSeconds(_options.CacheSeconds));
        }

        return coins;
    }

    private sealed class CoinGeckoMarketDto
    {
        public string? Id { get; init; }
        public string? Symbol { get; init; }
        public string? Name { get; init; }

        [JsonPropertyName("current_price")]
        public decimal CurrentPrice { get; init; }

        [JsonPropertyName("price_change_percentage_24h")]
        public double? PriceChangePercentage24H { get; init; }

        [JsonPropertyName("market_cap")]
        public decimal? MarketCap { get; init; }

        [JsonPropertyName("total_volume")]
        public decimal? TotalVolume { get; init; }

        [JsonPropertyName("sparkline_in_7d")]
        public SparklineDto? SparklineIn7D { get; init; }
    }

    private sealed class SparklineDto
    {
        [JsonPropertyName("price")]
        public List<decimal>? Price { get; init; }
    }
}