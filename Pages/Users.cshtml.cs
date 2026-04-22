using Microsoft.AspNetCore.Mvc.RazorPages;
using ADWebApp.Services;

namespace ADWebApp.Pages
{
    public class UsersModel : PageModel
    {
        private readonly ActiveDirectoryService _ad;

        public List<string> Users { get; set; }

        public UsersModel(ActiveDirectoryService ad)
        {
            _ad = ad;
        }

        public void OnGet()
        {
            Users = _ad.GetUsers();
        }
    }
}