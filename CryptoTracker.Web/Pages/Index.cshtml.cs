using CryptoTracker.Core.Services.Interfaces;
using CryptoTracker.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CryptoTracker.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ICryptoService _cryptoService;

    public List<Coin> Coins { get; set; } = new();
    
    public IndexModel(ICryptoService cryptoService)
    {
        _cryptoService = cryptoService;
    }
    
    
    public async Task OnGetAsync()
    {
        Coins = await _cryptoService.GetCoinsAsync();
    }
}