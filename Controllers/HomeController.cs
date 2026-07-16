using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FINAPSA.Models;

public class HomeController : Controller
{
    [AllowAnonymous]
    public IActionResult Index()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }
}
