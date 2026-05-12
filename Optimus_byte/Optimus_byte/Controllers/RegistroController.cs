using BC = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Mvc;
using VistaPrincipal.Models;
using VistaPrincipal.Data;
using Microsoft.EntityFrameworkCore;
using VistaPrincipal.Models.ViewModels;

namespace VistaPrincipal.Controllers
{
    public class RegistroController : Controller
    {


        private readonly optimusDBContext _db;

        public RegistroController(optimusDBContext db) => _db = db;

        [HttpGet]
        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("UsuarioId") != null)
                return RedirectToAction("Index", "Home");

            return View(new RegistroViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(RegistroViewModel model)
        {
            bool correoExiste = _db.Usuarios.Any(u => u.Correo == model.Correo);
            if (correoExiste)
                ModelState.AddModelError(nameof(model.Correo), "Este correo ya está registrado en el sistema.");

            bool documentoExiste = _db.Clientes.Any(c => c.NumeroDocumento == model.NumeroDocumento);
            if (documentoExiste)
                ModelState.AddModelError(nameof(model.NumeroDocumento), "Este número de documento ya está registrado.");

            if (!ModelState.IsValid)
                return View(model);

            int idRolCliente = _db.Roles
                .Where(r => r.NombreRol == "Cliente")
                .Select(r => r.RolId)
                .FirstOrDefault();

            if (idRolCliente == 0)
            {
                ModelState.AddModelError("", "No existe el rol Cliente en la base de datos.");
                return View(model);
            }

            var nuevoUsuario = new Usuario
            {
                NombreCompleto = model.NombreCompleto,
                Correo = model.Correo,
                Telefono = model.Telefono,
                ContrasenaHash = BC.HashPassword(model.Contrasena),
                Activo = true,
                IdRol = idRolCliente
            };

            _db.Usuarios.Add(nuevoUsuario);
            _db.SaveChanges();

            var nuevoCliente = new Cliente
            {
                IdUsuario = nuevoUsuario.IdUsuario,
                NombreCompleto = model.NombreCompleto,
                TipoDocumento = model.TipoDocumento,
                NumeroDocumento = model.NumeroDocumento,
                Telefono = model.Telefono,
                Correo = model.Correo,
                Direccion = model.Direccion,
                Activo = true,
                FechaRegistro = DateTime.Now
            };

            _db.Clientes.Add(nuevoCliente);
            _db.SaveChanges();

            TempData["RegistroExitoso"] = $"Cuenta creada. Bienvenido {model.NombreCompleto}, ya puedes iniciar sesion.";
            return RedirectToAction("Index", "Login");
        }
    }
}
