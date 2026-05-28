// InventarioController.cs ─────────────────────────────────────────
using Microsoft.AspNetCore.Mvc;

namespace Optimus_byte.Controllers
{
    public class InventarioController : Controller
    {
        public IActionResult Index() => View();

        public IActionResult Detalles(int id)
        {
            ViewBag.Id = id;
            return View();
        }
    }
}
