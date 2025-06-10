using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebGenerateImage.Data;
using WebGenerateImage.Models;
using WebGenerateImage.Services;

namespace WebGenerateImage.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _db;
        private readonly EmailService _emailService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public AuthController(AppDbContext db, EmailService emailService,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager)
        {
            _db = db;
            _emailService = emailService;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpGet]
        public IActionResult Register() => View();

        [HttpGet]
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleResponse")
            };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpPost]
        public async Task<IActionResult> SendOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewData["Error"] = "Email không hợp lệ.";
                return View("Register");
            }

            if (await _userManager.FindByEmailAsync(email) != null)
            {
                ViewData["Error"] = "Email đã được đăng ký.";
                return View("Register");
            }

            var code = new Random().Next(100000, 999999).ToString();

            var otp = new OtpCode
            {
                Email = email,
                Code = code,
                ExpirationTime = DateTime.Now.AddMinutes(1)
            };

            _db.OtpCodes.Add(otp);
            await _db.SaveChangesAsync();

            await _emailService.SendOtpAsync(email, code);

            ViewData["Message"] = "Mã OTP đã gửi đến email của bạn.";
            ViewData["Email"] = email;
            return View("Register");
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOtpAndRegister(string email, string otpCode, string password, string confirmPassword, bool enableFaceAuth = false)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                ViewData["Error"] = "Vui lòng nhập đầy đủ thông tin.";
                ViewData["Email"] = email;
                return View("Register");
            }

            if (password != confirmPassword)
            {
                ViewData["Error"] = "Mật khẩu và xác nhận mật khẩu không khớp.";
                ViewData["Email"] = email;
                return View("Register");
            }

            var otp = await _db.OtpCodes
                .Where(o => o.Email == email && o.Code == otpCode)
                .OrderByDescending(o => o.ExpirationTime)
                .FirstOrDefaultAsync();

            if (otp == null || otp.ExpirationTime < DateTime.Now)
            {
                ViewData["Error"] = "Mã OTP không hợp lệ hoặc đã hết hạn.";
                ViewData["Email"] = email;
                return View("Register");
            }

            if (await _userManager.FindByEmailAsync(email) != null)
            {
                ViewData["Error"] = "Email đã được đăng ký.";
                ViewData["Email"] = email;
                return View("Register");
            }

            var user = new IdentityUser
            {
                UserName = email,
                Email = email
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                ViewData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
                ViewData["Email"] = email;
                return View("Register");
            }

            // ✅ Gán quyền mặc định là "User"
            await _userManager.AddToRoleAsync(user, "User");

            // ✅ Xoá OTP sau khi đăng ký
            _db.OtpCodes.RemoveRange(_db.OtpCodes.Where(o => o.Email == email));
            await _db.SaveChangesAsync();

            ViewData["Success"] = "Đăng ký thành công. Mời bạn đăng nhập.";
            return View("Login");
        }


        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                ViewData["Error"] = "Email không tồn tại.";
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(user, password, false, false);
            if (!result.Succeeded)
            {
                ViewData["Error"] = "Mật khẩu không đúng.";
                return View();
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> GoogleResponse()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!result.Succeeded)
                return BadRequest("Xác thực Google thất bại.");

            var claims = result.Principal.Identities.FirstOrDefault()?.Claims;
            var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

            if (email == null || name == null)
                return BadRequest("Không lấy được thông tin người dùng từ Google.");

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new IdentityUser
                {
                    UserName = email,
                    Email = email
                };
                await _userManager.CreateAsync(user);
            }

            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, email),
                new Claim(ClaimTypes.Name, name),
                new Claim(ClaimTypes.Email, email)
            }, CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> LoginByFace(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return BadRequest("Email không tồn tại.");

            // TODO: Xác thực khuôn mặt - nếu đã bật thì xử lý tiếp
            return Ok("Đăng nhập bằng khuôn mặt thành công .");
        }
    }
}
