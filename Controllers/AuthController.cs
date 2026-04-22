using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.DirectoryServices.AccountManagement;
using System.Security.Claims;

namespace ADPortal.Controllers;

public class AuthController : Controller
{
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration config, ILogger<AuthController> logger)
    {
        _config = config;
        _logger = logger;
    }

    // GET /Auth/Login
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        // If already authenticated, redirect home
        if (User.Identity?.IsAuthenticated == true)
            return Redirect(returnUrl ?? "/");

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    // POST /Auth/Login — validates credentials against AD manually
    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ViewBag.Error = "Username and password are required.";
            return View();
        }

        var domain = _config["ActiveDirectory:Domain"] ?? "corp.lurking.site";

        // Strip domain prefix if user typed DOMAIN\user or user@domain
        var samAccountName = username;
        if (username.Contains('\\'))
            samAccountName = username.Split('\\', 2)[1];
        else if (username.Contains('@'))
            samAccountName = username.Split('@')[0];

        try
        {
            bool valid;
            using (var ctx = new PrincipalContext(ContextType.Domain, domain))
            {
                valid = ctx.ValidateCredentials(samAccountName, password);
            }

            if (!valid)
            {
                _logger.LogWarning("Failed login attempt for {User} from {IP}",
                    username, HttpContext.Connection.RemoteIpAddress);
                ViewBag.Error = "Invalid username or password.";
                return View();
            }

            // Build claims identity (cookie-based)
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, $"{domain}\\{samAccountName}"),
                new(ClaimTypes.NameIdentifier, samAccountName),
                new("AuthMethod", "Forms")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            _logger.LogInformation("Forms login successful for {User}", samAccountName);
            return Redirect(returnUrl ?? "/");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AD validation error for {User}", username);
            ViewBag.Error = "Could not connect to Active Directory. Please try again or contact IT.";
            return View();
        }
    }

    // GET /Auth/Logout
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}
