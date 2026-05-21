using Google.Authenticator;
using Microsoft.AspNetCore.Mvc;
using TwoFactorAuthApp.Models;

namespace TwoFactorAuthApp.Controllers;

public class HomeController : Controller
{
    private readonly AuthDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly string _secretKey;

    public HomeController(AuthDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
        _secretKey = _configuration["AppSettings:GoogleAuthKey"] ?? "KhizarSecureKey!2026";
    }

    // Check if user completed login + 2FA
    private bool UserLoggedIn()
    {
        var currentUser = HttpContext.Session.GetString("CurrentUser");
        var verified = HttpContext.Session.GetString("TwoFactorPassed");
        return !string.IsNullOrEmpty(currentUser) && verified == "yes";
    }

    // ================= HOME PAGE =================
    public IActionResult Index()
    {
        if (!UserLoggedIn())
            return RedirectToAction("Login");

        ViewBag.Username = HttpContext.Session.GetString("CurrentUser");
        return View();
    }

    // ================= ABOUT =================
    public IActionResult Privacy()
    {
        if (!UserLoggedIn())
            return RedirectToAction("Login");
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
    public IActionResult Register(RegisterModel registerData)
    {
        if (registerData.Password != registerData.ConfirmPassword)
        {
            ViewBag.Message = "Both passwords must be same.";
            return View(registerData);
        }

        bool alreadyExists = _context.Users.Any(x => x.UserName == registerData.UserName);
        if (alreadyExists)
        {
            ViewBag.Message = "This username is already taken.";
            return View(registerData);
        }

        // Generate unique secret key for Google Authenticator
        string generatedSecret = $"{registerData.UserName}-{Guid.NewGuid()}-{_secretKey}";

        UserModel user = new UserModel
        {
            UserName = registerData.UserName,
            HashedPassword = BCrypt.Net.BCrypt.HashPassword(registerData.Password),
            TwoFactorSecret = generatedSecret
        };

        _context.Users.Add(user);
        _context.SaveChanges();

        TempData["Success"] = "Account created successfully.";
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
    public IActionResult Login(LoginModel loginData)
    {
        var existingUser = _context.Users.FirstOrDefault(x => x.UserName == loginData.UserName);

        if (existingUser == null || !BCrypt.Net.BCrypt.Verify(loginData.Password, existingUser.HashedPassword))
        {
            ViewBag.Message = "Incorrect username or password.";
            return View(loginData);
        }

        // Generate QR code for Google Authenticator
        TwoFactorAuthenticator authenticator = new TwoFactorAuthenticator();

        var googleSetup = authenticator.GenerateSetupCode(
            "TwoFactorAuthApp",
            existingUser.UserName,
            existingUser.TwoFactorSecret,
            false,      // secretIsBase32: our secret is raw, not base32
            4           // QR code pixel size
        );

        // Store pending user session
        HttpContext.Session.SetString("PendingUser", existingUser.UserName);
        HttpContext.Session.SetString("UserSecretKey", existingUser.TwoFactorSecret);

        TwoFactorModel tfModel = new TwoFactorModel
        {
            QrCodeImage = googleSetup.QrCodeSetupImageUrl,
            ManualEntryKey = googleSetup.ManualEntryKey
        };

        return View("TwoFactorAuthenticate", tfModel);
    }

    // ================= VERIFY 2FA =================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyTwoFactor(TwoFactorModel tfData)
    {
        string? pendingUser = HttpContext.Session.GetString("PendingUser");

        if (string.IsNullOrEmpty(pendingUser))
            return RedirectToAction("Login");

        var userRecord = _context.Users.FirstOrDefault(x => x.UserName == pendingUser);
        if (userRecord == null)
            return RedirectToAction("Login");

        TwoFactorAuthenticator verifyAuth = new TwoFactorAuthenticator();
        bool codeMatched = verifyAuth.ValidateTwoFactorPIN(
            userRecord.TwoFactorSecret,
            tfData.Code,
            false);  // secretIsBase32: false because our secret is raw

        if (!codeMatched)
        {
            ViewBag.Message = "Wrong authentication code. Please try again.";
            // Regenerate QR code for retry
            TwoFactorAuthenticator authenticator = new TwoFactorAuthenticator();
            var googleSetup = authenticator.GenerateSetupCode(
                "TwoFactorAuthApp", userRecord.UserName, userRecord.TwoFactorSecret, false, 4);
            tfData.QrCodeImage = googleSetup.QrCodeSetupImageUrl;
            tfData.ManualEntryKey = googleSetup.ManualEntryKey;
            return View("TwoFactorAuthenticate", tfData);
        }

        // Login success
        HttpContext.Session.SetString("CurrentUser", pendingUser);
        HttpContext.Session.SetString("TwoFactorPassed", "yes");
        HttpContext.Session.Remove("PendingUser");
        HttpContext.Session.Remove("UserSecretKey");

        return RedirectToAction("Index");
    }

    // ================= LOGOUT =================
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    // ================= GET CURRENT TOTP CODE (for demo purposes) =================
    // This endpoint returns the current valid TOTP code for a user.
    // In production this would never be exposed, but for lab demonstration
    // it shows the actual code generated by the Google Authenticator algorithm.
    [HttpGet]
    public IActionResult GetCurrentCode(string username)
    {
        if (string.IsNullOrEmpty(username))
            return Content("Please provide a username parameter (e.g., ?username=khizar)");

        var user = _context.Users.FirstOrDefault(x => x.UserName == username);
        if (user == null)
            return Content($"User '{username}' not found in database.");

        TwoFactorAuthenticator tfa = new TwoFactorAuthenticator();
        string currentCode = tfa.GetCurrentPIN(user.TwoFactorSecret, false);
        string[] codes = tfa.GetCurrentPINs(user.TwoFactorSecret, false);

        string result = $"Current TOTP codes for '{username}':\n";
        result += $"Primary code: {currentCode}\n";
        result += $"All valid codes (including time drift): {string.Join(", ", codes)}\n";
        result += "\nThis code changes every 30 seconds based on the TOTP algorithm.\n";
        result += "Scan the QR code with Google Authenticator on your phone, or\n";
        result += $"enter '{currentCode}' in the verification field to complete 2FA.";

        return Content(result);
    }
}
