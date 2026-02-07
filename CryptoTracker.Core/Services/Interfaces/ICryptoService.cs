﻿﻿using CryptoTracker.Models;
using System.Threading.Tasks;

namespace CryptoTracker.Core.Services.Interfaces;

public record CoinsResult(List<Coin> Coins, int TotalCount, int Page, int PageSize, bool FromCache);

public interface ICryptoService
{
    Task<CoinsResult> GetCoinsAsync(
        string? vsCurrency = null,
        bool? sparkline = null,
        int? perPage = null,
        int page = 1,
        string? sortBy = null,
        bool descending = true);
}