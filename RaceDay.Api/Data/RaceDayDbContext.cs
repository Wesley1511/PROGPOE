using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Api.Data
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
