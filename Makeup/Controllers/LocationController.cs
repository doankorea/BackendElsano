using Microsoft.AspNetCore.Mvc;

namespace Makeup.Controllers
{
    public class LocationController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
