using Microsoft.AspNetCore.Mvc;

namespace Makeup.Controllers
{
    public class CalendarController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
