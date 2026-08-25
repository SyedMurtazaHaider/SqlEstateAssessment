using Microsoft.AspNetCore.Mvc;

namespace SqlEstatePortal.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "Dashboard");

    public IActionResult Error() => View();
}
