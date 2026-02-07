using CryptoTracker.Core.Services.Interfaces;
using CryptoTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;

namespace CryptoTracker.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ICryptoService _cryptoService;
    private static readonly int[] AllowedPerPage = { 10, 25, 50 };

    public List<Coin> Coins { get; set; } = new();
    public DateTimeOffset? LastUpdated { get; private set; }
    public bool HasError { get; private set; }
    public bool IsRateLimited { get; private set; }
    public string Currency { get; private set; } = "eur";
    public string CurrencySymbol { get; private set; } = "€";
    public int PerPage { get; private set; } = 10;

    public IndexModel(ICryptoService cryptoService)
    {
        _cryptoService = cryptoService;
    }

    public async Task OnGetAsync(string? currency = null, int? perPage = null)
    {
        var resolvedCurrency = ResolveCurrency(currency);
        Currency = resolvedCurrency.Code;
        CurrencySymbol = resolvedCurrency.Symbol;
        PerPage = ResolvePerPage(perPage);

        try
        {
            Coins = await _cryptoService.GetCoinsAsync(Currency, true, PerPage);
            LastUpdated = DateTimeOffset.Now;
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

    private static int ResolvePerPage(int? perPage)
    {
        if (!perPage.HasValue)
        {
            return AllowedPerPage[0];
        }

        return AllowedPerPage.Contains(perPage.Value) ? perPage.Value : AllowedPerPage[0];
    }
}