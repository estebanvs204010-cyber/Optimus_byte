// RepuestosController.cs ─────────────────────────────────────────
using Microsoft.AspNetCore.Mvc;

namespace Optimus_byte.Controllers
{
    public class RepuestosController : Controller
    {
        public IActionResult Index() => View();
    }
}
