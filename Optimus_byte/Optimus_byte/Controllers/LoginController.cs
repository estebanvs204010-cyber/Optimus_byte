using BC = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Mvc;
using VistaPrincipal.Models;
using VistaPrincipal.Models.ViewModels;
using VistaPrincipal.Data;
using Microsoft.EntityFrameworkCore;

namespace VistaPrincipal.Controllers
{
    public class LoginController : Controller
    {
        private readonly optimusDBContext _db;

        public LoginController(optimusDBContext db) => _db = db;

        // GET: /Login
        [HttpGet]
        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("UsuarioId") != null)
                return RedirigirPorRol(HttpContext.Session.GetString("UsuarioRol")!);

            return View("~/Views/Login/Index.cshtml");
        }

        // POST: /Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View("~/Views/Login/Index.cshtml", model);

            var usuario = _db.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefault(u => u.Correo == model.Correo && u.Activo);

            if (usuario == null || !BC.Verify(model.Contrasena, usuario.ContrasenaHash))
            {
                _db.IntentosFallidos.Add(new IntentoFallido { Correo = model.Correo });
                _db.SaveChanges();

                ModelState.AddModelError("", "Correo o contraseña incorrectos.");
                return View("~/Views/Login/Index.cshtml", model);
            }

            // Guardar sesión
            HttpContext.Session.SetString("UsuarioId", usuario.IdUsuario.ToString());
            HttpContext.Session.SetString("UsuarioNombre", usuario.NombreCompleto);
            HttpContext.Session.SetString("UsuarioRol", usuario.Rol!.NombreRol);

            // Log auditoría
            _db.LogAuditoria.Add(new LogAuditoria
            {
                IdUsuario = usuario.IdUsuario,
                Accion = "Inicio de sesión exitoso",
                Modulo = "Autenticación"
            });
            _db.SaveChanges();

            // ── Redirigir según los 3 roles ──────────────────────
            return RedirigirPorRol(usuario.Rol.NombreRol);
        }

        // Helper de redirección
        private IActionResult RedirigirPorRol(string rol) => rol switch
        {
            "Admin" => RedirectToAction("Index", "Usuarios"),
            "Mecanico" => RedirectToAction("MisOrdenes", "Mecanico"),
            "Cliente" => RedirectToAction("Portal", "Cliente"),
            _ => RedirectToAction("Index", "Home")
        };

        // ── REGISTRO ─────────────────────────────────────────
        [HttpGet]
        public IActionResult Registro()
        {
            return View("~/Views/Registro/Index.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Registro(Usuario model)
        {
            if (_db.Usuarios.Any(u => u.Correo == model.Correo))
            {
                ModelState.AddModelError("Correo", "Este correo ya está registrado.");
                return View("~/Views/Registro/Index.cshtml", model);
            }

            if (!ModelState.IsValid)
                return View("~/Views/Registro/Index.cshtml", model);

            var nuevoUsuario = new Usuario
            {
                NombreCompleto = model.NombreCompleto,
                Correo = model.Correo,
                Telefono = model.Telefono,
                ContrasenaHash = BC.HashPassword(model.Contrasena),
                Activo = true,
                IdRol = 3   // ← rol Cliente (id 3)
            };

            _db.Usuarios.Add(nuevoUsuario);
            _db.SaveChanges();

            TempData["RegistroExitoso"] = $"¡Bienvenido {model.NombreCompleto}! Ya puedes iniciar sesión.";
            return RedirectToAction("Index");
        }

        // ── LOGOUT ───────────────────────────────────────────
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }
        public IActionResult Usuarios_Index()
        {
            if (HttpContext.Session.GetString("UsuarioRol") != "Admin")
                return RedirectToAction("Index");

            var usuarios = _db.Usuarios
                .Include(u => u.Rol)
                .OrderBy(u => u.IdRol)
                .ThenBy(u => u.NombreCompleto)
                .Select(u => new UsuarioEditarViewModel
                {
                    IdUsuario = u.IdUsuario,
                    NombreCompleto = u.NombreCompleto,
                    Correo = u.Correo,
                    Telefono = u.Telefono ?? "",
                    IdRol = u.IdRol,
                    Activo = u.Activo,
                    Rol = u.Rol
                })
                .ToList();

            ViewBag.Roles = _db.Roles.OrderBy(r => r.RolId).ToList();
            return RedirectToAction("Index", "Usuarios");
        }
    }
}

