using System.Diagnostics;
using BC = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.Models;
using Optimus_byte.DATA;

namespace Optimus_byte.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly DbHelper _db;

        public HomeController(ILogger<HomeController> logger, DbHelper db)
        {
            _logger = logger;
            _db = db;
        }

        // GET: /Home
        public IActionResult Index()
        {
            ViewBag.UsuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
            ViewBag.UsuarioRol    = HttpContext.Session.GetString("UsuarioRol");
            ViewBag.UsuarioId     = HttpContext.Session.GetString("UsuarioId");
            return View();
        }

        // GET: /Home/Perfil
        public IActionResult Perfil()
        {
            var nombre = HttpContext.Session.GetString("UsuarioNombre");
            if (nombre == null) return RedirectToAction("Index", "Login");

            var idStr = HttpContext.Session.GetString("UsuarioId");
            if (!int.TryParse(idStr, out int userId))
                return RedirectToAction("Index", "Login");

            string correo = "", telefono = "";

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT correo, telefono FROM Usuarios WHERE id_usuario = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", userId);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    correo   = reader["correo"]?.ToString()   ?? "";
                    telefono = reader["telefono"]?.ToString() ?? "";
                }
            }

            ViewBag.UsuarioNombre   = nombre;
            ViewBag.UsuarioRol      = HttpContext.Session.GetString("UsuarioRol");
            ViewBag.UsuarioId       = idStr;
            ViewBag.UsuarioCorreo   = correo;
            ViewBag.UsuarioTelefono = telefono;

            return View();
        }

        // POST: /Home/ActualizarPerfil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActualizarPerfil(string NombreCompleto, string Correo,
                                              string Telefono,
                                              string? NuevaContrasena, string? ConfirmarContrasena)
        {
            var idStr = HttpContext.Session.GetString("UsuarioId");
            if (idStr == null) return RedirectToAction("Index", "Login");
            if (!int.TryParse(idStr, out int userId))
                return RedirectToAction("Index", "Login");

            // Validar contraseña si se escribió
            if (!string.IsNullOrWhiteSpace(NuevaContrasena))
            {
                if (NuevaContrasena.Length < 6)
                {
                    TempData["Error"] = "La contraseña debe tener al menos 6 caracteres.";
                    return RedirectToAction("Perfil");
                }
                if (NuevaContrasena != ConfirmarContrasena)
                {
                    TempData["Error"] = "Las contraseñas no coinciden.";
                    return RedirectToAction("Perfil");
                }
            }

            using (var conn = _db.GetConnection())
            {
                // Construir la query dinámicamente según si hay nueva contraseña
                string sql = @"UPDATE Usuarios
                               SET nombre_completo = @nombre,
                                   correo          = @correo,
                                   telefono        = @telefono";

                if (!string.IsNullOrWhiteSpace(NuevaContrasena))
                    sql += ", contrasena_hash = @hash";

                sql += " WHERE id_usuario = @id";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@nombre",  NombreCompleto.Trim());
                cmd.Parameters.AddWithValue("@correo",  Correo.Trim());
                cmd.Parameters.AddWithValue("@telefono", Telefono.Trim());
                cmd.Parameters.AddWithValue("@id",      userId);

                if (!string.IsNullOrWhiteSpace(NuevaContrasena))
                    cmd.Parameters.AddWithValue("@hash", BC.HashPassword(NuevaContrasena));

                cmd.ExecuteNonQuery();
            }

            // Refrescar nombre en sesión
            HttpContext.Session.SetString("UsuarioNombre", NombreCompleto.Trim());

            TempData["Exito"] = "Perfil actualizado correctamente.";
            return RedirectToAction("Perfil");
        }

        // POST: /Home/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
