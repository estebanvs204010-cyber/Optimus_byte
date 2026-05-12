using BC = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VistaPrincipal.Data;
using VistaPrincipal.Models;
using VistaPrincipal.Models.ViewModels;

namespace VistaPrincipal.Controllers
{
    public class UsuariosController : Controller
    {
        private readonly optimusDBContext _db;

        public UsuariosController(optimusDBContext db) => _db = db;

        // ── Solo el Administrador puede entrar ────────────────
        private bool EsAdmin() =>
            HttpContext.Session.GetString("UsuarioRol") == "Admin";
        // ══════════════════════════════════════════════════════
        // GET: /Usuarios  →  Lista completa
        // ══════════════════════════════════════════════════════
        public IActionResult Index()
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

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
            return View("~/Views/Usuarios/Usuarios_Index.cshtml", usuarios);
        }

        // ══════════════════════════════════════════════════════
        // GET: /Usuarios/Crear
        // ══════════════════════════════════════════════════════
        public IActionResult Crear()
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            ViewBag.Roles = _db.Roles.OrderBy(r => r.RolId).ToList();
            return View("~/Views/Usuarios/Crear.cshtml", new Usuario());
        }

        // POST: /Usuarios/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(Usuario model)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            if (_db.Usuarios.Any(u => u.Correo == model.Correo))
            {
                ModelState.AddModelError("Correo", "Este correo ya está registrado.");
                ViewBag.Roles = _db.Roles.ToList();
                return View("~/Views/Usuarios/Crear.cshtml", model);
            }

            if (string.IsNullOrWhiteSpace(model.Contrasena))
            {
                ModelState.AddModelError("Contrasena", "La contraseña es obligatoria.");
                ViewBag.Roles = _db.Roles.ToList();
                return View("~/Views/Usuarios/Crear.cshtml", model);
            }

            model.ContrasenaHash = BC.HashPassword(model.Contrasena);
            model.Activo = true;

            _db.Usuarios.Add(model);
            _db.SaveChanges();

            RegistrarAuditoria($"Creó usuario: {model.NombreCompleto} — Rol ID: {model.IdRol}");

            TempData["Exito"] = $"Usuario {model.NombreCompleto} creado correctamente.";
            return RedirectToAction("Index");
        }

        // ══════════════════════════════════════════════════════
        // GET: /Usuarios/Editar/5
        // ══════════════════════════════════════════════════════
        public IActionResult Editar(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var usuario = _db.Usuarios.Include(u => u.Rol)
                                      .FirstOrDefault(u => u.IdUsuario == id);
            if (usuario == null) return NotFound();

            var vm = new UsuarioEditarViewModel
            {
                IdUsuario = usuario.IdUsuario,
                NombreCompleto = usuario.NombreCompleto,
                Correo = usuario.Correo,
                Telefono = usuario.Telefono ?? "",
                IdRol = usuario.IdRol,
                Activo = usuario.Activo,
                Rol = usuario.Rol
            };
            ViewBag.Roles = _db.Roles.OrderBy(r => r.RolId).ToList();
            return View("~/Views/Usuarios/Usuarios_Editar.cshtml", vm);
        }

        // POST: /Usuarios/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(int id, Usuario model)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var usuario = _db.Usuarios.Find(id);
            if (usuario == null) return NotFound();

            if (_db.Usuarios.Any(u => u.Correo == model.Correo && u.IdUsuario != id))
            {
                ModelState.AddModelError("Correo", "Este correo ya lo usa otro usuario.");
                ViewBag.Roles = _db.Roles.ToList();
                return View("~/Views/Usuarios/Usuarios_Editar.cshtml", model);
            }

            usuario.NombreCompleto = model.NombreCompleto;
            usuario.Correo = model.Correo;
            usuario.Telefono = model.Telefono;
            usuario.IdRol = model.IdRol;
            usuario.Activo = model.Activo;

            // Solo cambiar contraseña si escribió una nueva
            if (!string.IsNullOrWhiteSpace(model.Contrasena))
                usuario.ContrasenaHash = BC.HashPassword(model.Contrasena);

            _db.SaveChanges();
            RegistrarAuditoria($"Editó usuario ID {id}: {usuario.NombreCompleto}");

            TempData["Exito"] = $"Usuario {usuario.NombreCompleto} actualizado.";
            return RedirectToAction("Index");
        }

        // ══════════════════════════════════════════════════════
        // POST: /Usuarios/Desactivar/5
        // ══════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Desactivar(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var usuario = _db.Usuarios.Find(id);
            if (usuario == null) return NotFound();

            // No puede desactivarse a sí mismo
            var idActual = int.Parse(HttpContext.Session.GetString("UsuarioId") ?? "0");
            if (usuario.IdUsuario == idActual)
            {
                TempData["Error"] = "No puedes desactivar tu propia cuenta.";
                return RedirectToAction("Index");
            }

            usuario.Activo = false;
            _db.SaveChanges();
            RegistrarAuditoria($"Desactivó usuario ID {id}: {usuario.NombreCompleto}");

            TempData["Exito"] = $"Usuario {usuario.NombreCompleto} desactivado.";
            return RedirectToAction("Index");
        }

        // ══════════════════════════════════════════════════════
        // POST: /Usuarios/Reactivar/5
        // ══════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reactivar(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var usuario = _db.Usuarios.Find(id);
            if (usuario == null) return NotFound();

            usuario.Activo = true;
            _db.SaveChanges();
            RegistrarAuditoria($"Reactivó usuario ID {id}: {usuario.NombreCompleto}");

            TempData["Exito"] = $"Usuario {usuario.NombreCompleto} reactivado.";
            return RedirectToAction("Index");
        }

        // ── Helper auditoría ──────────────────────────────────
        private void RegistrarAuditoria(string accion)
        {
            if (int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int idAdmin))
            {
                _db.LogAuditoria.Add(new LogAuditoria
                {
                    IdUsuario = idAdmin,
                    Accion = accion,
                    Modulo = "Gestión de Usuarios"
                });
                _db.SaveChanges();
            }
        }
    }
}