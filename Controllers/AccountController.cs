using Microsoft.AspNetCore.Mvc;
using WNCAirline.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace WNCAirline.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        public IActionResult Register([FromBody] User model)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Dữ liệu nhập vào chưa hợp lệ!" });

            var isExist = _context.Users.Any(u => u.Email == model.Email);
            if (isExist)
                return Json(new { success = false, message = "Email này đã được đăng ký trên hệ thống!" });

            _context.Users.Add(model);
            _context.SaveChanges();

            return Json(new { success = true, message = "Đăng ký tài khoản SkyBlue thành công! Hãy đăng nhập." });
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequest model)
        {
            if (model == null || string.IsNullOrEmpty(model.Username) || string.IsNullOrEmpty(model.Password))
            {
                return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin!" });
            }

            var user = _context.Users.FirstOrDefault(u => u.Email == model.Username && u.Password == model.Password);

            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.Name)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                return Json(new { success = true, message = "Đăng nhập thành công!" });
            }

            return Json(new { success = false, message = "Tài khoản hoặc mật khẩu không chính xác." });
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }
}
