using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FINAPSA.Data;
using FINAPSA.Models;
using FINAPSA.Models.ViewModels;
using System.Security.Claims;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;

    public AccountController(
        UserManager<User> userManager,
        SignInManager<User> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    // ══════════════════════════════════════════════════
    //  AUDIT
    // ══════════════════════════════════════════════════
    private async Task LogAttempt(string? userId, string identifier,
                                   bool success, string? reason)
    {
        using var scope = HttpContext.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FINAPSADbContext>();
        db.LoginAudits.Add(new LoginAudit
        {
            UserId = userId,
            Email = identifier,
            IsSuccessful = success,
            FailureReason = reason,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers["User-Agent"].ToString()
        });
        await db.SaveChangesAsync();
    }

    // ══════════════════════════════════════════════════
    //  LOGIN  (GET)
    // ══════════════════════════════════════════════════
    [HttpGet]
    public IActionResult Login() => View();

    // ══════════════════════════════════════════════════
    //  LOGIN  (POST)
    //
    //  How it works:
    //  1. Find user by Admission Number (username in Identity)
    //  2. Compare the typed Full Name against the stored Full Name
    //     (case-insensitive, trims extra spaces)
    //  3. Admin shortcut: type "ADMIN" as full name to skip name check
    //     (only works if the user is in the Admin role)
    // ══════════════════════════════════════════════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var admNo = (model.AdmissionNumber ?? "").Trim().ToUpper();
        var typedFullName = (model.FullName ?? "").Trim().ToUpper();

        // ── Step 1: find user by admission number ───────────────────
        var user = await _userManager.FindByNameAsync(admNo);

        if (user == null)
        {
            await LogAttempt(null, admNo, false, "Admission number not found");
            TempData["ErrorMessage"] = "Admission number not found. Please check and try again.";
            return View(model);
        }

        if (user.IsSuspended)
        {
            await LogAttempt(user.Id, admNo, false, "Account suspended");
            TempData["ErrorMessage"] = "Your account has been suspended. Contact the admin.";
            return View(model);
        }

        // ── Step 2: Admin shortcut ───────────────────────────────────
        bool isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        if (typedFullName == "ADMIN" && isAdmin)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            await LogAttempt(user.Id, admNo, true, "Admin shortcut login");
            return await RedirectByRole(user);
        }

        // ── Step 3: Verify full name ─────────────────────────────────
        // Get the canonical full name — prefer the Identity user's FullName,
        // fall back to the linked Student record if missing.
        string storedFullName = (user.FullName ?? "").Trim().ToUpper();

        if (string.IsNullOrEmpty(storedFullName))
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FINAPSADbContext>();
            var student = await db.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);
            storedFullName = (student?.FullName ?? "").Trim().ToUpper();
        }

        if (storedFullName != typedFullName)
        {
            await LogAttempt(user.Id, admNo, false, "Full name mismatch");
            TempData["ErrorMessage"] = "Full name does not match. Please check and try again.";
            return View(model);
        }

        // ── Step 4: Sign in ──────────────────────────────────────────
        await _signInManager.SignInAsync(user, isPersistent: false);
        await LogAttempt(user.Id, admNo, true, null);

        return await RedirectByRole(user);
    }

    // ══════════════════════════════════════════════════
    //  REGISTER — disabled, students are added by Admin
    // ══════════════════════════════════════════════════
    [HttpGet]
    public IActionResult Register() => RedirectToAction(nameof(Login));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Register(RegisterViewModel model)
        => RedirectToAction(nameof(Login));

    // ══════════════════════════════════════════════════
    //  GOOGLE OAuth
    // ══════════════════════════════════════════════════
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var props = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(props, provider);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null)
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            TempData["ErrorMessage"] = "Google login failed. Please try again.";
            return RedirectToAction(nameof(Login));
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
        {
            TempData["ErrorMessage"] = "Google did not provide an email.";
            return RedirectToAction(nameof(Login));
        }

        var signInResult = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);

        if (signInResult.Succeeded)
        {
            var existingUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (existingUser is { IsSuspended: true })
            {
                await _signInManager.SignOutAsync();
                TempData["ErrorMessage"] = "Your account has been suspended.";
                return RedirectToAction(nameof(Login));
            }
            return await RedirectByRole(existingUser!);
        }

        var userByEmail = await _userManager.FindByEmailAsync(email);
        if (userByEmail != null)
        {
            await _userManager.AddLoginAsync(userByEmail, info);
            await _signInManager.SignInAsync(userByEmail, isPersistent: false);
            return await RedirectByRole(userByEmail);
        }

        TempData["GoogleEmail"] = email;
        TempData["GoogleName"] = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email;
        TempData["GoogleProvider"] = info.LoginProvider;
        TempData["GoogleProviderKey"] = info.ProviderKey;
        return RedirectToAction(nameof(ExternalLoginConfirmation));
    }

    [HttpGet]
    public IActionResult ExternalLoginConfirmation()
    {
        if (TempData["GoogleEmail"] == null) return RedirectToAction(nameof(Login));
        TempData.Keep();
        return View(new ExternalLoginConfirmationViewModel
        {
            Email = TempData["GoogleEmail"]?.ToString() ?? "",
            FullName = TempData["GoogleName"]?.ToString() ?? ""
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExternalLoginConfirmation(
        ExternalLoginConfirmationViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var email = TempData["GoogleEmail"]?.ToString();
        var name = TempData["GoogleName"]?.ToString();
        var provider = TempData["GoogleProvider"]?.ToString();
        var providerKey = TempData["GoogleProviderKey"]?.ToString();

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(provider))
        {
            TempData["ErrorMessage"] = "Session expired. Please sign in with Google again.";
            return RedirectToAction(nameof(Login));
        }

        if (await _userManager.FindByEmailAsync(email) != null)
        {
            TempData["ErrorMessage"] = "An account with that email already exists.";
            return RedirectToAction(nameof(Login));
        }

        var user = new User
        {
            UserName = email,
            Email = email,
            FullName = name ?? email,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            foreach (var e in createResult.Errors)
                ModelState.AddModelError("", e.Description);
            return View(model);
        }

        var allowed = new[] { "Teacher", "Bursar" }; // Admin + Student not self-assignable
        var roleToAssign = allowed.Contains(model.SelectedRole) ? model.SelectedRole : "Teacher";
        await _userManager.AddToRoleAsync(user, roleToAssign);
        await _userManager.AddLoginAsync(user, new UserLoginInfo(provider, providerKey!, "Google"));
        await _signInManager.SignInAsync(user, isPersistent: false);

        return await RedirectByRole(user);
    }

    // ══════════════════════════════════════════════════
    //  LOGOUT
    // ══════════════════════════════════════════════════
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // ══════════════════════════════════════════════════
    //  PROFILE
    // ══════════════════════════════════════════════════
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        var roles = await _userManager.GetRolesAsync(user!);
        return View(new ProfileViewModel
        {
            FullName = user!.FullName,
            Email = user.Email,
            Role = roles.FirstOrDefault() ?? "User"
        });
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    // ══════════════════════════════════════════════════
    //  ROLE REDIRECT
    // ══════════════════════════════════════════════════
    private async Task<IActionResult> RedirectByRole(User user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Admin")) return RedirectToAction("Index", "Dashboard");
        if (roles.Contains("Bursar")) return RedirectToAction("Index", "Dashboard");
        if (roles.Contains("Teacher")) return RedirectToAction("Index", "Dashboard");
        return RedirectToAction("Index", "Dashboard"); // Student
    }
}