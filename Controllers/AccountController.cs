using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TwoFactorAuthApp.Models;
using TwoFactorAuthApp.Services;

namespace TwoFactorAuthApp.Controllers;

public class AccountController : Controller
{
    private readonly AuthDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<AccountController> _logger;
    private readonly IConfiguration _configuration;

    public AccountController(
        AuthDbContext context,
        IEmailService emailService,
        ILogger<AccountController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
        _configuration = configuration;
    }

    private bool IsRealEmailEnabled =>
        _configuration.GetValue<bool>("EmailSettings:UseRealEmailService");

    private bool UserLoggedIn()
    {
        var currentUser = HttpContext.Session.GetString("AccountUser");
        var verified = HttpContext.Session.GetString("Account2FAPassed");
        return !string.IsNullOrEmpty(currentUser) && verified == "yes";
    }

    // ================= HOME =================
    public IActionResult Index()
    {
        if (!UserLoggedIn())
            return RedirectToAction("Login");
        ViewBag.Username = HttpContext.Session.GetString("AccountUser");
        return View();
    }

    // ================= REGISTER =================
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Check if email already exists
        var existingEmail = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
        if (existingEmail != null)
        {
            ModelState.AddModelError("", "This email is already registered.");
            return View(model);
        }

        // Check if username already exists
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == model.UserName);
        if (existingUser != null)
        {
            ModelState.AddModelError("", "This username is already taken.");
            return View(model);
        }

        var user = new UserModel
        {
            UserName = model.UserName,
            Email = model.Email,
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(model.Password),
            TwoFactorSecret = "" // not used for email 2FA
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Account created successfully! Please login.";
        return RedirectToAction("Login");
    }

    // ================= LOGIN =================
    [HttpGet]
    public IActionResult Login()
    {
        HttpContext.Session.Clear();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == model.UserName);

        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.HashedPassword))
        {
            ViewBag.Message = "Invalid username or password.";
            return View(model);
        }

        // Check if user has email (for email 2FA)
        if (string.IsNullOrEmpty(user.Email))
        {
            ViewBag.Message = "No email registered. Please register with an email address.";
            return View(model);
        }

        // Generate random 6-digit code
        Random generator = new Random();
        string code = generator.Next(100000, 999999).ToString();

        user.TwoFactorCode = code;
        user.TwoFactorCodeExpiry = DateTime.Now.AddMinutes(5);
        await _context.SaveChangesAsync();

        // Store email in session for the verification step
        HttpContext.Session.SetString("PendingEmail", user.Email);
        HttpContext.Session.SetString("PendingUserName", user.UserName);

        // Send the code (mock will log to console; in prod this sends a real email)
        await _emailService.SendTwoFactorCodeAsync(user.Email, code);

        _logger.LogInformation("Verification code for {User}: {Code}", user.UserName, code);

        return RedirectToAction("VerifyTwoFactor");
    }

    // ================= VERIFY 2FA =================
    [HttpGet]
    public IActionResult VerifyTwoFactor()
    {
        string? pendingEmail = HttpContext.Session.GetString("PendingEmail");

        // Show code on screen only in mock/demo mode
        if (!IsRealEmailEnabled)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == pendingEmail);
            if (user != null && user.TwoFactorCodeExpiry > DateTime.Now)
            {
                ViewBag.DemoCode = user.TwoFactorCode;
            }
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyTwoFactor(Email2FAModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        string? pendingEmail = HttpContext.Session.GetString("PendingEmail");
        if (string.IsNullOrEmpty(pendingEmail))
            return RedirectToAction("Login");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == pendingEmail);
        if (user == null)
            return RedirectToAction("Login");

        // Check code validity
        bool validCode = user.TwoFactorCode == model.Code && user.TwoFactorCodeExpiry > DateTime.Now;

        if (!validCode)
        {
            ModelState.AddModelError("", "Invalid or expired code. Please try again.");

            // Show demo code again for convenience (mock mode only)
            if (!IsRealEmailEnabled && user.TwoFactorCodeExpiry > DateTime.Now)
                ViewBag.DemoCode = user.TwoFactorCode;

            return View(model);
        }

        // Clear the verification code
        user.TwoFactorCode = null;
        user.TwoFactorCodeExpiry = null;
        await _context.SaveChangesAsync();

        // Complete login
        HttpContext.Session.SetString("AccountUser", user.UserName);
        HttpContext.Session.SetString("Account2FAPassed", "yes");
        HttpContext.Session.Remove("PendingEmail");
        HttpContext.Session.Remove("PendingUserName");

        return RedirectToAction("Index");
    }

    // ================= LOGOUT =================
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }
}
