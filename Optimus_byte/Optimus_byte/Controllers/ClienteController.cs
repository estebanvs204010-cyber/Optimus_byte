using Microsoft.AspNetCore.Mvc;
using VistaPrincipal.Data;
using VistaPrincipal.Models;

namespace VistaPrincipal.Controllers
{
    public class ClienteController : Controller
    {
        private readonly optimusDBContext _db;

        public ClienteController(optimusDBContext db) => _db = db;

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
