using Microsoft.AspNetCore.Mvc;

namespace ST10389916_CLDV7112_Practicum.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}
