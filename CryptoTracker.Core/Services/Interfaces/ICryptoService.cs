﻿using CryptoTracker.Models;
using System.Threading.Tasks;

namespace CryptoTracker.Core.Services.Interfaces;

public interface ICryptoService
{
    Task<List<Coin>> GetCoinsAsync(string? vsCurrency = null, bool? sparkline = null, int? perPage = null);
}