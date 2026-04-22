using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ADWebApp.Services;

namespace ADWebApp.Pages
{
    public class ResetPasswordModel : PageModel
    {
        private readonly ActiveDirectoryService _ad;

        [BindProperty]
        public string Username { get; set; }

        [BindProperty]
        public string NewPassword { get; set; }

        public string Message { get; set; }

        public ResetPasswordModel(ActiveDirectoryService ad)
        {
            _ad = ad;
        }

        public void OnPost()
        {
            _ad.ResetPassword(Username, NewPassword);
            Message = "Password reset successful";
        }
    }
}