﻿// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(function () {
  var favoritesKey = "cryptoTracker:favorites";
  var refreshKey = "cryptoTracker:autoRefresh";
  var refreshIntervalKey = "cryptoTracker:autoRefreshInterval";
  var uiStateKey = "cryptoTracker:uiState";

  function parseNumber(value) {
    if (!value) {
      return 0;
    }

    var normalized = value.toString().replace(",", ".");
    var parsed = Number.parseFloat(normalized);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  function formatCount(label, count) {
    if (!label) {
      return count.toString();
    }

    return label.replace("{0}", count.toString());
  }

  function loadFavorites() {
    try {
      var raw = window.localStorage.getItem(favoritesKey);
      if (!raw) {
        return [];
      }

      var parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? parsed : [];
    } catch (error) {
      return [];
    }
  }

  function saveFavorites(favorites) {
    window.localStorage.setItem(favoritesKey, JSON.stringify(favorites));
  }

  function renderSparkline(svg, values) {
    if (!svg || values.length < 2) {
      return;
    }

    var width = 100;
    var height = 30;
    var padding = 2;
    var min = Math.min.apply(null, values);
    var max = Math.max.apply(null, values);
    var range = max - min || 1;

    var points = values.map(function (value, index) {
      var x = (index / (values.length - 1)) * (width - padding * 2) + padding;
      var y = height - padding - ((value - min) / range) * (height - padding * 2);
      return x.toFixed(2) + "," + y.toFixed(2);
    });

    var polyline = document.createElementNS("http://www.w3.org/2000/svg", "polyline");
    polyline.setAttribute("points", points.join(" "));
    polyline.setAttribute("fill", "none");
    polyline.setAttribute("stroke", "currentColor");
    polyline.setAttribute("stroke-width", "2");

    while (svg.firstChild) {
      svg.removeChild(svg.firstChild);
    }

    svg.appendChild(polyline);
  }

  function loadRefreshSettings() {
    var enabled = window.localStorage.getItem(refreshKey) === "true";
    var interval = parseInt(window.localStorage.getItem(refreshIntervalKey) || "60", 10);
    if (!Number.isFinite(interval) || interval < 10) {
      interval = 60;
    }

    return { enabled: enabled, interval: interval };
  }

  function saveRefreshSettings(settings) {
    window.localStorage.setItem(refreshKey, settings.enabled ? "true" : "false");
    window.localStorage.setItem(refreshIntervalKey, settings.interval.toString());
  }

  function loadUiState() {
    try {
      var raw = window.localStorage.getItem(uiStateKey);
      if (!raw) {
        return {};
      }

      var parsed = JSON.parse(raw);
      return parsed && typeof parsed === "object" ? parsed : {};
    } catch (error) {
      return {};
    }
  }

  function saveUiState(state) {
    window.localStorage.setItem(uiStateKey, JSON.stringify(state));
  }

  document.addEventListener("DOMContentLoaded", function () {
    var body = document.body;
    var grid = document.querySelector("[data-coins-grid]");
    var searchInput = document.getElementById("coinSearch");
    var sortSelect = document.getElementById("coinSort");
    var countElement = document.getElementById("coinCount");
    var favoritesOnly = document.getElementById("favoritesOnly");
    var favoritesFirst = document.getElementById("favoritesFirst");
    var currencySelect = document.getElementById("currencySelect");
    var perPageSelect = document.getElementById("perPageSelect");
    var autoRefreshToggle = document.getElementById("autoRefreshToggle");
    var refreshInterval = document.getElementById("refreshInterval");
    var liveIndicator = document.getElementById("liveIndicator");
    var clearFilters = document.getElementById("clearFilters");
    var clearFavorites = document.getElementById("clearFavorites");
    var uiToast = document.getElementById("uiToast");

    if (body.classList.contains("page-loading")) {
      window.setTimeout(function () {
        body.classList.remove("page-loading");
      }, 150);
    }

    if (currencySelect && currencySelect.dataset.current) {
      currencySelect.value = currencySelect.dataset.current;
    }

    var refreshSettings = loadRefreshSettings();
    if (autoRefreshToggle) {
      autoRefreshToggle.checked = refreshSettings.enabled;
    }
    if (refreshInterval) {
      refreshInterval.value = refreshSettings.interval.toString();
    }
    if (liveIndicator) {
      liveIndicator.classList.toggle("d-none", !refreshSettings.enabled);
    }

    if (autoRefreshToggle) {
      autoRefreshToggle.addEventListener("change", function () {
        refreshSettings.enabled = autoRefreshToggle.checked;
        saveRefreshSettings(refreshSettings);
        if (liveIndicator) {
          liveIndicator.classList.toggle("d-none", !refreshSettings.enabled);
        }
      });
    }

    if (refreshInterval) {
      refreshInterval.addEventListener("change", function () {
        refreshSettings.interval = parseInt(refreshInterval.value, 10);
        saveRefreshSettings(refreshSettings);
      });
    }

    if (refreshSettings.enabled) {
      window.setInterval(function () {
        window.location.reload();
      }, refreshSettings.interval * 1000);
    }

    if (!grid) {
      return;
    }

    var cards = Array.prototype.slice.call(grid.querySelectorAll("[data-coin-card]"));
    if (cards.length === 0) {
      return;
    }

    var favorites = loadFavorites();
    var items = cards.map(function (card, index) {
      var name = (card.dataset.name || "").toLowerCase();
      var symbol = (card.dataset.symbol || "").toLowerCase();
      var apiId = card.dataset.coinId || "";
      var favoriteButton = card.querySelector("[data-favorite-button]");
      var favoriteBadge = card.querySelector("[data-favorite-badge]");
      var isFavorite = apiId && favorites.indexOf(apiId) >= 0;

      if (favoriteBadge) {
        favoriteBadge.classList.toggle("d-none", !isFavorite);
      }

      if (favoriteButton) {
        favoriteButton.setAttribute("aria-pressed", isFavorite ? "true" : "false");
        favoriteButton.classList.toggle("is-active", isFavorite);
        favoriteButton.innerHTML = isFavorite ? "&starf;" : "&star;";
        favoriteButton.addEventListener("click", function () {
          isFavorite = !isFavorite;
          favoriteButton.setAttribute("aria-pressed", isFavorite ? "true" : "false");
          favoriteButton.classList.toggle("is-active", isFavorite);
          favoriteButton.innerHTML = isFavorite ? "&starf;" : "&star;";
          if (favoriteBadge) {
            favoriteBadge.classList.toggle("d-none", !isFavorite);
          }
          if (!apiId) {
            return;
          }

          var indexOfId = favorites.indexOf(apiId);
          if (isFavorite && indexOfId === -1) {
            favorites.push(apiId);
          } else if (!isFavorite && indexOfId >= 0) {
            favorites.splice(indexOfId, 1);
          }

          saveFavorites(favorites);
          applyFilterAndSort();
        });
      }

      return {
        el: card,
        name: name,
        symbol: symbol,
        price: parseNumber(card.dataset.price),
        change: parseNumber(card.dataset.change),
        apiId: apiId,
        isFavorite: function () {
          return apiId && favorites.indexOf(apiId) >= 0;
        },
        index: index,
        favoriteBadge: favoriteBadge
      };
    });

    grid.querySelectorAll(".sparkline").forEach(function (svg) {
      var raw = svg.dataset.sparkline || "";
      if (!raw) {
        return;
      }

      var values = raw.split(",").map(parseNumber).filter(function (value) {
        return Number.isFinite(value);
      });

      renderSparkline(svg, values);
    });

    function updateCount() {
      if (!countElement) {
        return;
      }

      var visibleCount = items.reduce(function (count, item) {
        return count + (item.el.classList.contains("d-none") ? 0 : 1);
      }, 0);

      var label = countElement.dataset.countLabel || "";
      countElement.textContent = formatCount(label, visibleCount);
    }

    function applyFilterAndSort() {
      var query = (searchInput && searchInput.value ? searchInput.value : "").trim().toLowerCase();
      var onlyFavorites = favoritesOnly && favoritesOnly.checked;
      var groupFavorites = favoritesFirst && favoritesFirst.checked;

      items.forEach(function (item) {
        var matchesQuery = !query || item.name.indexOf(query) >= 0 || item.symbol.indexOf(query) >= 0;
        var matchesFavorite = !onlyFavorites || item.isFavorite();
        var visible = matchesQuery && matchesFavorite;
        item.el.classList.toggle("d-none", !visible);
      });

      var sortKey = sortSelect && sortSelect.value ? sortSelect.value : "name";
      var sorted = items.slice().sort(function (a, b) {
        if (groupFavorites) {
          var favoriteDiff = (b.isFavorite() ? 1 : 0) - (a.isFavorite() ? 1 : 0);
          if (favoriteDiff !== 0) {
            return favoriteDiff;
          }
        }

        if (sortKey === "price") {
          return b.price - a.price;
        }

        if (sortKey === "change") {
          return b.change - a.change;
        }

        return a.name.localeCompare(b.name);
      });

      sorted.forEach(function (item) {
        grid.appendChild(item.el);
      });

      updateCount();
      persistUiState();
    }

    var uiState = loadUiState();
    if (searchInput && typeof uiState.query === "string") {
      searchInput.value = uiState.query;
    }
    if (sortSelect && typeof uiState.sort === "string") {
      sortSelect.value = uiState.sort;
    }
    if (favoritesOnly && typeof uiState.favoritesOnly === "boolean") {
      favoritesOnly.checked = uiState.favoritesOnly;
    }
    if (favoritesFirst && typeof uiState.favoritesFirst === "boolean") {
      favoritesFirst.checked = uiState.favoritesFirst;
    }
    if (currencySelect && typeof uiState.currency === "string") {
      currencySelect.value = uiState.currency;
    }
    if (perPageSelect && typeof uiState.perPage === "string") {
      perPageSelect.value = uiState.perPage;
    }

    if (currencySelect) {
      currencySelect.addEventListener("change", function () {
        var url = new URL(window.location.href);
        url.searchParams.set("currency", currencySelect.value);
        if (perPageSelect) {
          url.searchParams.set("perPage", perPageSelect.value);
        }
        saveUiState({
          query: searchInput ? searchInput.value : "",
          sort: sortSelect ? sortSelect.value : "name",
          favoritesOnly: favoritesOnly ? favoritesOnly.checked : false,
          favoritesFirst: favoritesFirst ? favoritesFirst.checked : false,
          currency: currencySelect.value,
          perPage: perPageSelect ? perPageSelect.value : ""
        });
        window.location.href = url.toString();
      });
    }

    if (perPageSelect) {
      if (perPageSelect.dataset.current) {
        perPageSelect.value = perPageSelect.dataset.current;
      }
      perPageSelect.addEventListener("change", function () {
        var url = new URL(window.location.href);
        url.searchParams.set("perPage", perPageSelect.value);
        if (currencySelect) {
          url.searchParams.set("currency", currencySelect.value);
        }
        saveUiState({
          query: searchInput ? searchInput.value : "",
          sort: sortSelect ? sortSelect.value : "name",
          favoritesOnly: favoritesOnly ? favoritesOnly.checked : false,
          favoritesFirst: favoritesFirst ? favoritesFirst.checked : false,
          currency: currencySelect ? currencySelect.value : "",
          perPage: perPageSelect.value
        });
        window.location.href = url.toString();
      });
    }

    function showToast(message) {
      if (!uiToast || !message) {
        return;
      }

      uiToast.textContent = message;
      uiToast.classList.add("is-visible");
      window.clearTimeout(uiToast._hideTimer);
      uiToast._hideTimer = window.setTimeout(function () {
        uiToast.classList.remove("is-visible");
      }, 2200);
    }

    function persistUiState() {
      saveUiState({
        query: searchInput ? searchInput.value : "",
        sort: sortSelect ? sortSelect.value : "name",
        favoritesOnly: favoritesOnly ? favoritesOnly.checked : false,
        favoritesFirst: favoritesFirst ? favoritesFirst.checked : false,
        currency: currencySelect ? currencySelect.value : "",
        perPage: perPageSelect ? perPageSelect.value : ""
      });
    }

    if (searchInput) {
      searchInput.addEventListener("input", applyFilterAndSort);
    }

    if (sortSelect) {
      sortSelect.addEventListener("change", applyFilterAndSort);
    }

    if (favoritesOnly) {
      favoritesOnly.addEventListener("change", applyFilterAndSort);
    }

    if (favoritesFirst) {
      favoritesFirst.addEventListener("change", applyFilterAndSort);
    }

    if (clearFilters) {
      clearFilters.addEventListener("click", function () {
        if (searchInput) {
          searchInput.value = "";
        }
        if (sortSelect) {
          sortSelect.value = "name";
        }
        if (favoritesOnly) {
          favoritesOnly.checked = false;
        }
        if (favoritesFirst) {
          favoritesFirst.checked = false;
        }
        if (perPageSelect) {
          var defaultPerPage = perPageSelect.dataset.default || "10";
          perPageSelect.value = defaultPerPage;
        }
        applyFilterAndSort();
        var filtersMessage = uiToast ? uiToast.dataset.toastFilters : "";
        showToast(filtersMessage);
      });
    }

    if (clearFavorites) {
      clearFavorites.addEventListener("click", function () {
        favorites = [];
        saveFavorites(favorites);
        items.forEach(function (item) {
          var button = item.el.querySelector("[data-favorite-button]");
          if (button) {
            button.setAttribute("aria-pressed", "false");
            button.classList.remove("is-active");
            button.innerHTML = "&star;";
          }
          if (item.favoriteBadge) {
            item.favoriteBadge.classList.add("d-none");
          }
        });
        applyFilterAndSort();
        var favoritesMessage = uiToast ? uiToast.dataset.toastFavorites : "";
        showToast(favoritesMessage);
      });
    }

    applyFilterAndSort();
  });
})();
