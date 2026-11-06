using Microsoft.AspNetCore.Mvc;
using ST10389916_CLDV7112_Practicum.Models;

namespace ST10389916_CLDV7112_Practicum.Controllers;

public sealed class AccountController(UserAccountStore store) : Controller
{
    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(string.Empty, "Username and password are required.");
            return View(model);
        }

        if (model.Role is not ("Farmer" or "Agronomist" or "Admin"))
        {
            ModelState.AddModelError(string.Empty, "Role must be Farmer, Agronomist, or Admin.");
            return View(model);
        }

        var id = await store.CreateUserAsync(model.Username.Trim(), model.Password, model.Role, cancellationToken);
        ViewBag.Message = $"User created: {id}";
        return View(new RegisterViewModel { Role = model.Role });
    }

    [HttpGet]
    public IActionResult Login() => View(new LoginViewModel());

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        var user = await store.ValidateLoginAsync(model.Username, model.Password, cancellationToken);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View(model);
        }

        ViewBag.Message = $"Login successful. Role: {user.Role}";
        return View(new LoginViewModel());
    }
}
