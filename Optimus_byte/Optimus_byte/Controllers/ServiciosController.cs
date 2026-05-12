using Microsoft.AspNetCore.Mvc;

namespace VistaPrincipal.Controllers
{
    public class ServiciosController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
