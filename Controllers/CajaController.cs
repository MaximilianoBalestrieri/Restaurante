using Microsoft.AspNetCore.Mvc;

namespace RestauranteApp.Controllers
{
    public class CajaController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
