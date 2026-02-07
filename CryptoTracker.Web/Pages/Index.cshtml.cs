using CryptoTracker.Core.Services.Interfaces;
using CryptoTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;

namespace CryptoTracker.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ICryptoService _cryptoService;
    private static readonly int[] AllowedFetchCount = { 10, 25, 50, 100 };
    private static readonly int[] AllowedDisplayPerPage = { 10, 20, 50 };
    private static readonly string[] AllowedSortFields = { "name", "price", "change", "market_cap", "volume" };

    public List<Coin> AllCoins { get; set; } = new();
    public List<Coin> DisplayedCoins { get; set; } = new();
    public DateTimeOffset? LastUpdated { get; private set; }
    public bool HasError { get; private set; }
    public bool IsRateLimited { get; private set; }
    public string Currency { get; private set; } = "eur";
    public string CurrencySymbol { get; private set; } = "€";
    
    // API fetch count
    public int FetchCount { get; private set; } = 10;
    
    // UI display per page
    public int DisplayPerPage { get; private set; } = 10;
    
    // Pagination (client-side over fetched data)
    public int CurrentPage { get; private set; } = 1;
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; } = 1;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
    
    // Sorting
    public string SortBy { get; private set; } = "market_cap";
    public bool SortDescending { get; private set; } = true;
    
    // Cache info
    public bool FromCache { get; private set; }

    public IndexModel(ICryptoService cryptoService)
    {
        _cryptoService = cryptoService;
    }

    public async Task OnGetAsync()
    {
        // Read query parameters manually
        var query = Request.Query;
        
        var currencyParam = query["currency"].FirstOrDefault();
        var fetchCountParam = query["fetchCount"].FirstOrDefault();
        var displayPerPageParam = query["displayPerPage"].FirstOrDefault();
        var pageParam = query["page"].FirstOrDefault();
        var sortByParam = query["sortBy"].FirstOrDefault();
        var descParam = query["desc"].FirstOrDefault();
        
        // Debug logging
        Console.WriteLine($"DEBUG: Raw query params - page={pageParam}, fetchCount={fetchCountParam}, displayPerPage={displayPerPageParam}");
        
        var resolvedCurrency = ResolveCurrency(currencyParam);
        Currency = resolvedCurrency.Code;
        CurrencySymbol = resolvedCurrency.Symbol;
        FetchCount = ResolveFetchCount(int.TryParse(fetchCountParam, out var fc) ? fc : null);
        DisplayPerPage = ResolveDisplayPerPage(int.TryParse(displayPerPageParam, out var dpp) ? dpp : null);
        SortBy = ResolveSortBy(sortByParam);
        SortDescending = string.IsNullOrEmpty(descParam) || descParam.Equals("true", StringComparison.OrdinalIgnoreCase) || descParam == "True";
        
        int requestedPage = int.TryParse(pageParam, out var p) ? p : 1;

        try
        {
            // Fetch all coins from API (up to FetchCount)
            var result = await _cryptoService.GetCoinsAsync(Currency, true, FetchCount, 1, SortBy, SortDescending);
            AllCoins = result.Coins;
            FromCache = result.FromCache;
            LastUpdated = DateTimeOffset.Now;
            
            // Calculate pagination after data is loaded
            TotalCount = AllCoins.Count;
            TotalPages = DisplayPerPage > 0 ? (int)Math.Ceiling((double)TotalCount / DisplayPerPage) : 1;
            
            // Resolve page after we know total count
            CurrentPage = Math.Max(1, Math.Min(requestedPage, TotalPages > 0 ? TotalPages : 1));
            
            Console.WriteLine($"DEBUG: requestedPage={requestedPage}, TotalPages={TotalPages}, CurrentPage={CurrentPage}");
            
            // Client-side pagination
            DisplayedCoins = AllCoins
                .Skip((CurrentPage - 1) * DisplayPerPage)
                .Take(DisplayPerPage)
                .ToList();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            IsRateLimited = true;
            HasError = true;
        }
        catch
        {
            HasError = true;
        }
    }

    private static (string Code, string Symbol) ResolveCurrency(string? currency)
    {
        var normalized = (currency ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "usd" => ("usd", "$"),
            "gbp" => ("gbp", "£"),
            _ => ("eur", "€")
        };
    }

    private static int ResolveFetchCount(int? fetchCount)
    {
        if (!fetchCount.HasValue)
        {
            return AllowedFetchCount[0];
        }
        return AllowedFetchCount.Contains(fetchCount.Value) ? fetchCount.Value : AllowedFetchCount[0];
    }

    private static int ResolveDisplayPerPage(int? displayPerPage)
    {
        if (!displayPerPage.HasValue)
        {
            return AllowedDisplayPerPage[0];
        }
        return AllowedDisplayPerPage.Contains(displayPerPage.Value) ? displayPerPage.Value : AllowedDisplayPerPage[0];
    }

    private static string ResolveSortBy(string? sortBy)
    {
        var normalized = (sortBy ?? string.Empty).Trim().ToLowerInvariant();
        return AllowedSortFields.Contains(normalized) ? normalized : "market_cap";
    }
}