using Microsoft.AspNetCore.Mvc;
using System;

namespace AiStockTradeApp.Controllers
{
    // Deprecated legacy controller kept only for backward-compatible routes.
    // All functionality moved to UserStockController (session + user watchlists unified).
    [ApiExplorerSettings(IgnoreApi = true)]
    [Obsolete("Use UserStockController. This controller now only performs redirects.")]
    public sealed class StockController : Controller
    {
        private IActionResult RedirectToUser(string action = "Dashboard") => RedirectToAction(action, "UserStock");

        [HttpGet]
        public IActionResult Dashboard() => RedirectToUser();

        [HttpPost]
        public IActionResult AddStock() => RedirectToUser("AddStock");

        [HttpPost]
        public IActionResult RemoveStock() => RedirectToUser("RemoveStock");

        [HttpPost]
        public IActionResult ClearWatchlist() => RedirectToUser("ClearWatchlist");
    }
}
