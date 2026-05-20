using Microsoft.AspNetCore.Mvc;
using Optimus_byte.DATA;

namespace Optimus_byte.Controllers
{
    public class ClienteController : Controller
    {
        private readonly DbHelper _db;

        public ClienteController(DbHelper db) => _db = db;

        private bool EsCliente() =>
            HttpContext.Session.GetString("UsuarioRol") == "Cliente";

        // GET: /Cliente/Portal
        public IActionResult Portal()
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Cliente";
            ViewBag.Nombre = nombre;
            return View("~/Views/Cliente/Portal.cshtml");
        }
    }
}
