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

    public async Task<List<Coin>> GetCoinsAsync()
    {
        var cacheKey = $"coingecko:{_options.VsCurrency}:{_options.PerPage}:{_options.Order}:{_options.Precision}";
        List<Coin>? cachedCoins = null;

        if (_options.CacheSeconds > 0 && _cache.TryGetValue(cacheKey, out List<Coin>? cachedValue))
        {
            cachedCoins = cachedValue;
            return cachedCoins ?? new List<Coin>();
        }

        var query = new Dictionary<string, string>
        {
            ["vs_currency"] = _options.VsCurrency,
            ["order"] = _options.Order,
            ["per_page"] = _options.PerPage.ToString(),
            ["page"] = "1",
            ["sparkline"] = _options.Sparkline.ToString().ToLowerInvariant(),
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
                Name = dto.Name ?? string.Empty,
                Symbol = (dto.Symbol ?? string.Empty).ToUpperInvariant(),
                Price = dto.CurrentPrice,
                Change24H = dto.PriceChangePercentage24H ?? 0
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
    }
}