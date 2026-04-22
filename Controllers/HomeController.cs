using ADPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ADPortal.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IActiveDirectoryService _adService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(IActiveDirectoryService adService, ILogger<HomeController> logger)
    {
        _adService = adService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var stats = await _adService.GetDashboardStatsAsync();

            // Pass logged-in Windows user to view
            ViewBag.CurrentUser = User.Identity?.Name ?? "Unknown";
            return View(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard");
            ViewBag.Error = "Unable to connect to Active Directory. Check server connectivity.";
            ViewBag.CurrentUser = User.Identity?.Name ?? "Unknown";
            return View(null);
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
