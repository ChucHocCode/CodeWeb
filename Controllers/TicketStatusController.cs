using Microsoft.AspNetCore.Mvc;

namespace WNCAirline.Controllers;

public class TicketStatusController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
