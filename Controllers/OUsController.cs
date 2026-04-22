using ADPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ADPortal.Models;

namespace ADPortal.Controllers;

[Authorize]
public class OUsController : Controller
{
    private readonly IActiveDirectoryService _adService;
    private readonly ILogger<OUsController> _logger;
    private readonly IConfiguration _config;

    public OUsController(IActiveDirectoryService adService, ILogger<OUsController> logger, IConfiguration config)
    {
        _adService = adService;
        _logger = logger;
        _config = config;
    }

    // GET /OUs - default to tree view
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Tree));
    }

    // GET /OUs/List - flat list view
    public async Task<IActionResult> List()
    {
        try
        {
            var ous = await _adService.GetAllOUsAsync();
            ViewBag.CurrentUser = User.Identity?.Name;
            return View("Index", ous);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading OUs");
            ViewBag.Error = "Could not load Organisational Units from Active Directory.";
            return View(new List<ADPortal.Models.AdOU>());
        }
    }

    // GET /OUs/Tree - tree-first view
    public async Task<IActionResult> Tree()
    {
        ViewBag.CurrentUser = User.Identity?.Name;
        var model = new OrganisationalUnitsViewModel
        {
            SearchBase = _config["ActiveDirectory:SearchBase"] ?? string.Empty,
            OUs = await _adService.GetAllOUsAsync(),
            Groups = await _adService.GetGroupsAsync()
        };

        return View(model);
    }

    // GET /OUs/Contents?dn=...
    [HttpGet]
    public async Task<IActionResult> Contents(string dn)
    {
        try
        {
            var contents = await _adService.GetOuContentsAsync(dn);
            return Json(contents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading OU contents for {Dn}", dn);
            return StatusCode(500, "Error loading OU contents");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOu(CreateOuViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "OU name and parent OU are required.";
            return RedirectToAction(nameof(Tree));
        }

        try
        {
            await _adService.CreateOuAsync(model);
            TempData["Success"] = $"OU '{model.Name}' created successfully.";
            return RedirectToAction(nameof(Tree));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating OU {Ou} under {Parent}", model.Name, model.ParentOuDn);
            TempData["Error"] = $"Failed to create OU: {ex.Message}";
            return RedirectToAction(nameof(Tree));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroup(CreateGroupViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Group name and parent OU are required.";
            return RedirectToAction(nameof(Tree));
        }

        try
        {
            await _adService.CreateGroupAsync(model);
            TempData["Success"] = $"Group '{model.Name}' created successfully.";
            return RedirectToAction(nameof(Tree));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating group {Group} under {Parent}", model.Name, model.ParentOuDn);
            TempData["Error"] = $"Failed to create group: {ex.Message}";
            return RedirectToAction(nameof(Tree));
        }
    }

    // GET /OUs/TreeData - AJAX endpoint for the OU tree
    [HttpGet]
    public async Task<IActionResult> TreeData()
    {
        try
        {
            var ous = await _adService.GetAllOUsAsync();

            // Format for jsTree
            var nodes = ous.Select(ou => new
            {
                id = ou.DistinguishedName,
                parent = GetParentDn(ou.DistinguishedName, ous.Select(o => o.DistinguishedName).ToList()),
                text = ou.Name,
                icon = "bi bi-folder",
                data = new { dn = ou.DistinguishedName }
            });

            return Json(nodes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading OU tree data");
            return StatusCode(500, "Error loading OU tree");
        }
    }

    private static string GetParentDn(string dn, List<string> allDns)
    {
        var commaIndex = dn.IndexOf(',');
        if (commaIndex < 0) return "#";
        var parentDn = dn[(commaIndex + 1)..];
        return allDns.Contains(parentDn, StringComparer.OrdinalIgnoreCase) ? parentDn : "#";
    }
}
