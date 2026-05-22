using Microsoft.AspNetCore.Mvc;

namespace WNCAirline.Controllers;

public class ContactController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
