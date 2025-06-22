using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebGenerateImage.Models;

namespace WebGenerateImage.Areas.Identity.Pages.Account.Manage
{
    public class ProfileModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly AppDbContext _context;

        public ProfileModel(UserManager<IdentityUser> userManager, AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public bool IsFaceVerified { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Account/Login");

            Email = user.Email ?? "";
            IsFaceVerified = _context.FaceAuthentications
                .Any(f => f.UserId == user.Id && f.IsFaceVerified);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                ViewData["Error"] = "Không tìm thấy người dùng.";
                return Page();
            }

            Email = user.Email ?? "";

            var existing = _context.FaceAuthentications.FirstOrDefault(f => f.UserId == user.Id);

            if (existing == null)
            {
                if (IsFaceVerified)
                {
                    _context.FaceAuthentications.Add(new FaceAuthentication
                    {
                        UserId = user.Id,
                        IsFaceVerified = true
                    });
                    await _context.SaveChangesAsync();

                    return RedirectToPage("/Account/RegisterFace", new { area = "Identity" });
                }
            }
            else
            {
                // Nếu có bản ghi rồi => chỉ cập nhật
                existing.IsFaceVerified = IsFaceVerified;
                _context.FaceAuthentications.Update(existing);
                await _context.SaveChangesAsync();
            }

            ViewData["Message"] = "Cập nhật thành công!";
            return Page();
        }


    }
}