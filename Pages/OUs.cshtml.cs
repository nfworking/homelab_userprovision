using Microsoft.AspNetCore.Mvc.RazorPages;
using ADWebApp.Services;

namespace ADWebApp.Pages
{
    public class OUsModel : PageModel
    {
        private readonly ActiveDirectoryService _ad;

        public List<string> OUs { get; set; }

        public OUsModel(ActiveDirectoryService ad)
        {
            _ad = ad;
        }

        public void OnGet()
        {
            OUs = _ad.GetOUs();
        }
    }
}