using Microsoft.AspNetCore.Mvc;

namespace RestauranteApp.Controllers
{
    public class CocinaController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
