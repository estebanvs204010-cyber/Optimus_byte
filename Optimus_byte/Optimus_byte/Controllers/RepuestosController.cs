using Microsoft.AspNetCore.Mvc;

namespace VistaPrincipal.Controllers
{
    public class RepuestosController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
