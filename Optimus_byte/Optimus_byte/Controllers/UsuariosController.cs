using BC = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.DATA;
using Optimus_byte.Models;
using Optimus_byte.Models.ViewModels;

namespace Optimus_byte.Controllers
{
    public class UsuariosController : Controller
    {
        private readonly DbHelper _db;

        public UsuariosController(DbHelper db) => _db = db;

        private bool EsAdmin() =>
            HttpContext.Session.GetString("UsuarioRol") == "Admin";

        // ── GET: /Usuarios ────────────────────────────────────────
        public IActionResult Index()
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var usuarios = new List<UsuarioEditarViewModel>();
            var roles    = new List<Rol>();

            using var conn = _db.GetConnection();

            // Cargar usuarios
            using (var cmd = new SqlCommand(@"
                SELECT u.id_usuario, u.nombre_completo, u.correo,
                       u.telefono, u.id_rol, u.activo,
                       r.id_rol AS rol_id, r.nombre AS rol_nombre
                FROM Usuarios u
                INNER JOIN Roles r ON u.id_rol = r.id_rol
                ORDER BY u.id_rol, u.nombre_completo", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    usuarios.Add(new UsuarioEditarViewModel
                    {
                        IdUsuario      = Convert.ToInt32(reader["id_usuario"]),
                        NombreCompleto = reader["nombre_completo"].ToString()!,
                        Correo         = reader["correo"].ToString()!,
                        Telefono       = reader["telefono"]?.ToString() ?? "",
                        IdRol          = Convert.ToInt32(reader["id_rol"]),
                        Activo         = Convert.ToBoolean(reader["activo"]),
                        Rol = new Rol
                        {
                            RolId     = Convert.ToInt32(reader["rol_id"]),
                            NombreRol = reader["rol_nombre"].ToString()!
                        }
                    });
                }
            }

            // Cargar roles para el ViewBag
            using (var cmd2 = new SqlCommand(
                "SELECT id_rol, nombre FROM Roles ORDER BY id_rol", conn))
            using (var r2 = cmd2.ExecuteReader())
            {
                while (r2.Read())
                    roles.Add(new Rol
                    {
                        RolId     = Convert.ToInt32(r2["id_rol"]),
                        NombreRol = r2["nombre"].ToString()!
                    });
            }

            ViewBag.Roles = roles;
            return View("~/Views/Usuarios/Usuarios_Index.cshtml", usuarios);
        }

        // ── GET: /Usuarios/Crear ──────────────────────────────────
        public IActionResult Crear()
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            ViewBag.Roles = ObtenerRoles();
            return View("~/Views/Usuarios/Crear.cshtml", new Usuario());
        }

        // ── POST: /Usuarios/Crear ─────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(Usuario model)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            using var conn = _db.GetConnection();

            // Verificar correo duplicado
            using (var check = new SqlCommand(
                "SELECT COUNT(1) FROM Usuarios WHERE correo = @correo", conn))
            {
                check.Parameters.AddWithValue("@correo", model.Correo);
                if ((int)check.ExecuteScalar() > 0)
                {
                    ModelState.AddModelError("Correo", "Este correo ya está registrado.");
                    ViewBag.Roles = ObtenerRoles();
                    return View("~/Views/Usuarios/Crear.cshtml", model);
                }
            }

            if (string.IsNullOrWhiteSpace(model.Contrasena))
            {
                ModelState.AddModelError("Contrasena", "La contraseña es obligatoria.");
                ViewBag.Roles = ObtenerRoles();
                return View("~/Views/Usuarios/Crear.cshtml", model);
            }

            using (var cmd = new SqlCommand(@"
                INSERT INTO Usuarios
                    (nombre_completo, correo, telefono, contrasena_hash, activo, id_rol)
                VALUES (@nombre, @correo, @tel, @hash, 1, @rol)", conn))
            {
                cmd.Parameters.AddWithValue("@nombre", model.NombreCompleto);
                cmd.Parameters.AddWithValue("@correo", model.Correo);
                cmd.Parameters.AddWithValue("@tel",    model.Telefono ?? "");
                cmd.Parameters.AddWithValue("@hash",   BC.HashPassword(model.Contrasena));
                cmd.Parameters.AddWithValue("@rol",    model.IdRol);
                cmd.ExecuteNonQuery();
            }

            RegistrarAuditoria($"Creó usuario: {model.NombreCompleto} – Rol ID: {model.IdRol}");

            TempData["Exito"] = $"Usuario {model.NombreCompleto} creado correctamente.";
            return RedirectToAction("Index");
        }

        // ── GET: /Usuarios/Editar/5 ───────────────────────────────
        public IActionResult Editar(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            UsuarioEditarViewModel? vm = null;

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                SELECT u.id_usuario, u.nombre_completo, u.correo,
                       u.telefono, u.id_rol, u.activo,
                       r.id_rol AS rol_id, r.nombre AS rol_nombre
                FROM Usuarios u
                INNER JOIN Roles r ON u.id_rol = r.id_rol
                WHERE u.id_usuario = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    vm = new UsuarioEditarViewModel
                    {
                        IdUsuario      = Convert.ToInt32(reader["id_usuario"]),
                        NombreCompleto = reader["nombre_completo"].ToString()!,
                        Correo         = reader["correo"].ToString()!,
                        Telefono       = reader["telefono"]?.ToString() ?? "",
                        IdRol          = Convert.ToInt32(reader["id_rol"]),
                        Activo         = Convert.ToBoolean(reader["activo"]),
                        Rol = new Rol
                        {
                            RolId     = Convert.ToInt32(reader["rol_id"]),
                            NombreRol = reader["rol_nombre"].ToString()!
                        }
                    };
                }
            }

            if (vm == null) return NotFound();

            ViewBag.Roles = ObtenerRoles();
            return View("~/Views/Usuarios/Usuarios_Editar.cshtml", vm);
        }

        // ── POST: /Usuarios/Editar/5 ──────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(int id, Usuario model)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            using var conn = _db.GetConnection();

            // Verificar que el correo no lo use otro usuario
            using (var check = new SqlCommand(@"
                SELECT COUNT(1) FROM Usuarios
                WHERE correo = @correo AND id_usuario <> @id", conn))
            {
                check.Parameters.AddWithValue("@correo", model.Correo);
                check.Parameters.AddWithValue("@id",     id);
                if ((int)check.ExecuteScalar() > 0)
                {
                    ModelState.AddModelError("Correo", "Este correo ya lo usa otro usuario.");
                    ViewBag.Roles = ObtenerRoles();
                    return View("~/Views/Usuarios/Usuarios_Editar.cshtml", model);
                }
            }

            string sql = @"
                UPDATE Usuarios
                SET nombre_completo = @nombre,
                    correo          = @correo,
                    telefono        = @tel,
                    id_rol          = @rol,
                    activo          = @activo";

            if (!string.IsNullOrWhiteSpace(model.Contrasena))
                sql += ", contrasena_hash = @hash";

            sql += " WHERE id_usuario = @id";

            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@nombre", model.NombreCompleto);
                cmd.Parameters.AddWithValue("@correo", model.Correo);
                cmd.Parameters.AddWithValue("@tel",    model.Telefono ?? "");
                cmd.Parameters.AddWithValue("@rol",    model.IdRol);
                cmd.Parameters.AddWithValue("@activo", model.Activo);
                cmd.Parameters.AddWithValue("@id",     id);

                if (!string.IsNullOrWhiteSpace(model.Contrasena))
                    cmd.Parameters.AddWithValue("@hash", BC.HashPassword(model.Contrasena));

                cmd.ExecuteNonQuery();
            }

            RegistrarAuditoria($"Editó usuario ID {id}: {model.NombreCompleto}");

            TempData["Exito"] = $"Usuario {model.NombreCompleto} actualizado.";
            return RedirectToAction("Index");
        }

        // ── POST: /Usuarios/Desactivar/5 ─────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Desactivar(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var idActual = int.Parse(HttpContext.Session.GetString("UsuarioId") ?? "0");
            if (id == idActual)
            {
                TempData["Error"] = "No puedes desactivar tu propia cuenta.";
                return RedirectToAction("Index");
            }

            string nombre = "";
            using (var conn = _db.GetConnection())
            {
                using (var get = new SqlCommand(
                    "SELECT nombre_completo FROM Usuarios WHERE id_usuario = @id", conn))
                {
                    get.Parameters.AddWithValue("@id", id);
                    nombre = get.ExecuteScalar()?.ToString() ?? "";
                }

                using (var cmd = new SqlCommand(
                    "UPDATE Usuarios SET activo = 0 WHERE id_usuario = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            RegistrarAuditoria($"Desactivó usuario ID {id}: {nombre}");
            TempData["Exito"] = $"Usuario {nombre} desactivado.";
            return RedirectToAction("Index");
        }

        // ── POST: /Usuarios/Reactivar/5 ──────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reactivar(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            string nombre = "";
            using (var conn = _db.GetConnection())
            {
                using (var get = new SqlCommand(
                    "SELECT nombre_completo FROM Usuarios WHERE id_usuario = @id", conn))
                {
                    get.Parameters.AddWithValue("@id", id);
                    nombre = get.ExecuteScalar()?.ToString() ?? "";
                }

                using (var cmd = new SqlCommand(
                    "UPDATE Usuarios SET activo = 1 WHERE id_usuario = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            RegistrarAuditoria($"Reactivó usuario ID {id}: {nombre}");
            TempData["Exito"] = $"Usuario {nombre} reactivado.";
            return RedirectToAction("Index");
        }

        // ── Helpers privados ──────────────────────────────────────
        private List<Rol> ObtenerRoles()
        {
            var roles = new List<Rol>();
            using var conn = _db.GetConnection();
            using var cmd  = new SqlCommand(
                "SELECT id_rol, nombre FROM Roles ORDER BY id_rol", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                roles.Add(new Rol
                {
                    RolId     = Convert.ToInt32(reader["id_rol"]),
                    NombreRol = reader["nombre"].ToString()!
                });
            return roles;
        }

        private void RegistrarAuditoria(string accion)
        {
            if (int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int idAdmin))
            {
                using var conn = _db.GetConnection();
                using var cmd  = new SqlCommand(@"
                    INSERT INTO LogAuditoria (id_usuario, accion, modulo)
                    VALUES (@id, @accion, @modulo)", conn);
                cmd.Parameters.AddWithValue("@id",     idAdmin);
                cmd.Parameters.AddWithValue("@accion", accion);
                cmd.Parameters.AddWithValue("@modulo", "Gestión de Usuarios");
                cmd.ExecuteNonQuery();
            }
        }
    }
}
