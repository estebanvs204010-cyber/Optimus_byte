// CitasController.cs  ─────────────────────────────────────────────
using Microsoft.AspNetCore.Mvc;

namespace Optimus_byte.Controllers
{
    public class CitasController : Controller
    {
        public IActionResult Index() => View();
    }
}
