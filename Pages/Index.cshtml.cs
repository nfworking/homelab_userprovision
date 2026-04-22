using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ADWebApp.Pages
{
    public class IndexModel : PageModel
    {
        public string Username { get; set; }

        public void OnGet()
        {
            Username = User.Identity?.Name ?? "Unknown";
        }
    }
}