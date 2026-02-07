﻿using System.Net.Http.Json;
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
    private static readonly string[] ValidSortFields = { "name", "price", "change", "market_cap", "volume" };

    public CryptoService(HttpClient httpClient, IOptions<CryptoApiOptions> options, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _cache = cache;
    }

    public async Task<CoinsResult> GetCoinsAsync(
        string? vsCurrency = null,
        bool? sparkline = null,
        int? perPage = null,
        int page = 1,
        string? sortBy = null,
        bool descending = true)
    {
        var resolvedCurrency = string.IsNullOrWhiteSpace(vsCurrency) ? _options.VsCurrency : vsCurrency.Trim().ToLowerInvariant();
        var resolvedSparkline = sparkline ?? _options.Sparkline;
        var resolvedPerPage = perPage.HasValue && perPage.Value > 0 ? perPage.Value : _options.PerPage;
        var resolvedPage = page > 0 ? page : 1;
        var resolvedSort = ValidSortFields.Contains(sortBy?.ToLowerInvariant() ?? "") ? sortBy!.ToLowerInvariant() : "market_cap";
        
        // Map sortBy to CoinGecko order parameter
        var apiOrder = MapSortToApiOrder(resolvedSort, descending);
        
        var cacheKey = $"coingecko:{resolvedCurrency}:{resolvedPerPage}:{resolvedPage}:{apiOrder}:{_options.Precision}:{resolvedSparkline}";
        
        if (_options.CacheSeconds > 0 && _cache.TryGetValue(cacheKey, out CoinsResult? cachedResult) && cachedResult is not null)
        {
            return cachedResult with { FromCache = true };
        }

        var query = new Dictionary<string, string>
        {
            ["vs_currency"] = resolvedCurrency,
            ["order"] = apiOrder,
            ["per_page"] = resolvedPerPage.ToString(),
            ["page"] = resolvedPage.ToString(),
            ["sparkline"] = resolvedSparkline.ToString().ToLowerInvariant(),
            ["price_change_percentage"] = "24h",
            ["precision"] = _options.Precision.ToString()
        };

        var url = "coins/markets?" + string.Join("&", query.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));

        using var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                // Try to return stale cache if available
                if (_cache.TryGetValue(cacheKey, out CoinsResult? staleResult) && staleResult is not null)
                {
                    return staleResult with { FromCache = true };
                }
            }
            return new CoinsResult(new List<Coin>(), 0, resolvedPage, resolvedPerPage, false);
        }

        var payload = await response.Content.ReadFromJsonAsync<List<CoinGeckoMarketDto>>();
        if (payload is null)
        {
            return new CoinsResult(new List<Coin>(), 0, resolvedPage, resolvedPerPage, false);
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

        // CoinGecko doesn't return total count, estimate based on page size
        var estimatedTotal = coins.Count < resolvedPerPage ? (resolvedPage - 1) * resolvedPerPage + coins.Count : resolvedPage * resolvedPerPage + 100;
        
        var result = new CoinsResult(coins, estimatedTotal, resolvedPage, resolvedPerPage, false);

        if (_options.CacheSeconds > 0)
        {
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(_options.CacheSeconds))
                .SetSlidingExpiration(TimeSpan.FromSeconds(_options.CacheSeconds / 2));
            _cache.Set(cacheKey, result, cacheOptions);
        }

        return result;
    }

    private static string MapSortToApiOrder(string sortBy, bool descending)
    {
        return sortBy switch
        {
            "name" => descending ? "id_desc" : "id_asc",
            "price" => descending ? "price_desc" : "price_asc",
            "change" => descending ? "percent_change_24h_desc" : "percent_change_24h_asc",
            "volume" => descending ? "volume_desc" : "volume_asc",
            _ => descending ? "market_cap_desc" : "market_cap_asc"
        };
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