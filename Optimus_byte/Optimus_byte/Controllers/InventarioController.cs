using Microsoft.AspNetCore.Mvc;

namespace VistaPrincipal.Controllers
{
    public class InventarioController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Detalles(int id)
        {
            ViewBag.Id = id;
            return View();
        }


    }
}