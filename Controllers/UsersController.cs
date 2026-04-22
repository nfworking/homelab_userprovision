using ADPortal.Models;
using ADPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADPortal.Controllers;

[Authorize]
public class UsersController : Controller
{
    private readonly IActiveDirectoryService _adService;
    private readonly ILogger<UsersController> _logger;
    private readonly IConfiguration _config;

    public UsersController(
        IActiveDirectoryService adService,
        ILogger<UsersController> logger,
        IConfiguration config)
    {
        _adService = adService;
        _logger = logger;
        _config = config;
    }

    // GET /Users  or  /Users?search=smith
    public async Task<IActionResult> Index(string? search)
    {
        try
        {
            var users = await _adService.GetAllUsersAsync(search);
            ViewBag.Search = search;
            ViewBag.CurrentUser = User.Identity?.Name;
            return View(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading users list");
            ViewBag.Error = "Could not load users from Active Directory.";
            return View(new List<AdUser>());
        }
    }

    // GET /Users/Create
    public IActionResult Create()
    {
        ViewBag.CurrentUser = User.Identity?.Name;
        return View(new CreateUserViewModel
        {
            TargetOuDn = _config["ActiveDirectory:SearchBase"] ?? string.Empty
        });
    }

    // POST /Users/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.CurrentUser = User.Identity?.Name;
            return View(model);
        }

        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            ViewBag.CurrentUser = User.Identity?.Name;
            return View(model);
        }

        if (model.Password.Length < 8)
        {
            ModelState.AddModelError("Password", "Password must be at least 8 characters.");
            ViewBag.CurrentUser = User.Identity?.Name;
            return View(model);
        }

        try
        {
            var success = await _adService.CreateUserAsync(model);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, "Unable to create user.");
                ViewBag.CurrentUser = User.Identity?.Name;
                return View(model);
            }

            TempData["Success"] = $"User '{model.SamAccountName}' created successfully.";
            _logger.LogInformation("User {User} created by {Admin}",
                model.SamAccountName, User.Identity?.Name);

            return RedirectToAction(nameof(Details), new { id = model.SamAccountName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user {User}", model.SamAccountName);
            ModelState.AddModelError(string.Empty, $"Error creating user: {ex.Message}");
            ViewBag.CurrentUser = User.Identity?.Name;
            return View(model);
        }
    }

    // GET /Users/Provisioning
    public async Task<IActionResult> Provisioning()
    {
        var sourceOuDn =
            _config["ActiveDirectory:ProvisioningSourceOuDn"] ??
            _config["ActiveDirectory:LostAndFoundOuDn"] ??
            _config["ActiveDirectory:SearchBase"] ?? string.Empty;

        var defaultTargetOuDn =
            _config["ActiveDirectory:ProvisioningDefaultTargetOuDn"] ??
            _config["ActiveDirectory:SearchBase"] ?? string.Empty;

        try
        {
            var users = await _adService.GetUsersInOuAsync(sourceOuDn);
            var groups = await _adService.GetGroupsAsync();
            var ous = await _adService.GetAllOUsAsync();
            ViewBag.CurrentUser = User.Identity?.Name;

            return View(new ProvisionUsersViewModel
            {
                SourceOuDn = sourceOuDn,
                TargetOuDn = defaultTargetOuDn,
                AvailableUsers = users,
                AvailableGroups = groups,
                AvailableOUs = ous
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading provisioning page for source OU {OU}", sourceOuDn);
            TempData["Error"] = "Could not load users from the provisioning source OU.";

            ViewBag.CurrentUser = User.Identity?.Name;
            return View(new ProvisionUsersViewModel
            {
                SourceOuDn = sourceOuDn,
                TargetOuDn = defaultTargetOuDn,
                AvailableUsers = new List<AdUser>(),
                AvailableGroups = new List<DirectoryObjectItem>(),
                AvailableOUs = new List<AdOU>()
            });
        }
    }

    // POST /Users/ProvisionSelected
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProvisionSelected(ProvisionUsersViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.TargetOuDn))
        {
            TempData["Error"] = "Target OU is required.";
            return RedirectToAction(nameof(Provisioning));
        }

        var selectedUsers = model.SelectedSamAccountNames
            ?? new List<string>();

        selectedUsers = selectedUsers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!selectedUsers.Any())
        {
            TempData["Error"] = "Select at least one user to provision.";
            return RedirectToAction(nameof(Provisioning));
        }

        try
        {
            var movedCount = await _adService.MoveUsersToOuAsync(selectedUsers, model.TargetOuDn);
            var groupAdds = await _adService.AddUsersToGroupsAsync(selectedUsers, model.SelectedGroupDns);
            if (movedCount == 0)
            {
                TempData["Error"] = "No users were moved. Check OU permissions and target OU DN.";
            }
            else if (movedCount < selectedUsers.Count)
            {
                TempData["Success"] = $"Provisioned {movedCount} of {selectedUsers.Count} user(s) to {model.TargetOuDn}. Added {groupAdds} group membership link(s).";
            }
            else
            {
                TempData["Success"] = $"Provisioned {movedCount} user(s) to {model.TargetOuDn}. Added {groupAdds} group membership link(s).";
            }

            _logger.LogInformation("Provisioning requested by {Admin}. Moved {Count} user(s) to {OU}",
                User.Identity?.Name, movedCount, model.TargetOuDn);

            return RedirectToAction(nameof(Provisioning));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provisioning error for target OU {OU}", model.TargetOuDn);
            TempData["Error"] = $"Provisioning failed: {ex.Message}";
            return RedirectToAction(nameof(Provisioning));
        }
    }

    // GET /Users/Details/jsmith
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest();

        try
        {
            var user = await _adService.GetUserAsync(id);
            if (user == null)
            {
                TempData["Error"] = $"User '{id}' not found in Active Directory.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.CurrentUser = User.Identity?.Name;
            return View(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading user details for {Id}", id);
            TempData["Error"] = "Could not load user details.";
            return RedirectToAction(nameof(Index));
        }
    }

    // GET /Users/ResetPassword/jsmith
    public async Task<IActionResult> ResetPassword(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest();

        var user = await _adService.GetUserAsync(id);
        if (user == null)
        {
            TempData["Error"] = $"User '{id}' not found.";
            return RedirectToAction(nameof(Index));
        }

        var vm = new ResetPasswordViewModel
        {
            SamAccountName = user.SamAccountName,
            DisplayName = user.DisplayName,
            MustChangeOnNextLogon = true
        };

        ViewBag.CurrentUser = User.Identity?.Name;
        return View(vm);
    }

    // POST /Users/ResetPassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.CurrentUser = User.Identity?.Name;
            return View(model);
        }

        if (model.NewPassword != model.ConfirmPassword)
        {
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
            ViewBag.CurrentUser = User.Identity?.Name;
            return View(model);
        }

        // Basic password complexity check
        if (model.NewPassword.Length < 8)
        {
            ModelState.AddModelError("NewPassword", "Password must be at least 8 characters.");
            ViewBag.CurrentUser = User.Identity?.Name;
            return View(model);
        }

        try
        {
            var success = await _adService.ResetPasswordAsync(
                model.SamAccountName,
                model.NewPassword,
                model.MustChangeOnNextLogon);

            if (success)
            {
                TempData["Success"] = $"Password successfully reset for {model.DisplayName}.";
                _logger.LogInformation("Password reset for {User} by {Admin}",
                    model.SamAccountName, User.Identity?.Name);
            }
            else
            {
                TempData["Error"] = "Password reset failed. User may not exist.";
            }

            return RedirectToAction(nameof(Details), new { id = model.SamAccountName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Password reset error for {User}", model.SamAccountName);
            ModelState.AddModelError(string.Empty,
                $"Error resetting password: {ex.Message}");
            ViewBag.CurrentUser = User.Identity?.Name;
            return View(model);
        }
    }
}
