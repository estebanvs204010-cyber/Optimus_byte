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

        private int GetIdAdmin() =>
            int.Parse(HttpContext.Session.GetString("UsuarioId") ?? "0");

        // ══════════════════════════════════════════════════════
        // GET: /Usuarios
        // ══════════════════════════════════════════════════════
        public IActionResult Index()
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var usuarios = new List<UsuarioEditarViewModel>();

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                SELECT u.id_usuario, u.nombre_completo, u.correo, u.telefono,
                       u.id_rol, u.activo, r.nombre AS nombre_rol
                FROM Usuarios u
                INNER JOIN Roles r ON u.id_rol = r.id_rol
                ORDER BY u.id_rol, u.nombre_completo", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    usuarios.Add(new UsuarioEditarViewModel
                    {
                        IdUsuario = Convert.ToInt32(reader["id_usuario"]),
                        NombreCompleto = reader["nombre_completo"].ToString()!,
                        Correo = reader["correo"].ToString()!,
                        Telefono = reader["telefono"].ToString() ?? "",
                        IdRol = Convert.ToInt32(reader["id_rol"]),
                        Activo = Convert.ToBoolean(reader["activo"]),
                        NombreRol = reader["nombre_rol"].ToString()!
                    });
                }
            }

            ViewBag.Roles = ObtenerRoles();
            return View("~/Views/Usuarios/Usuarios_Index.cshtml", usuarios);
        }

        // ══════════════════════════════════════════════════════
        // GET: /Usuarios/Crear
        // ══════════════════════════════════════════════════════
        public IActionResult Crear()
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            ViewBag.Roles = ObtenerRoles();
            return View("~/Views/Usuarios/Crear.cshtml", new Usuario());
        }

        // ══════════════════════════════════════════════════════
        // POST: /Usuarios/Crear
        // ══════════════════════════════════════════════════════
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Crear(Usuario model)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            // Verificar correo duplicado
            bool correoExiste = false;
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM Usuarios WHERE correo = @correo", conn))
            {
                cmd.Parameters.AddWithValue("@correo", model.Correo);
                correoExiste = (int)cmd.ExecuteScalar()! > 0;
            }

            if (correoExiste)
            {
                ModelState.AddModelError("Correo", "Este correo ya está registrado.");
                ViewBag.Roles = ObtenerRoles();
                return View("~/Views/Usuarios/Crear.cshtml", model);
            }

            if (string.IsNullOrWhiteSpace(model.Contrasena))
            {
                ModelState.AddModelError("Contrasena", "La contraseña es obligatoria.");
                ViewBag.Roles = ObtenerRoles();
                return View("~/Views/Usuarios/Crear.cshtml", model);
            }

            var hash = BC.HashPassword(model.Contrasena);
            int nuevoId = 0;

            // Insertar usuario y obtener ID generado
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                INSERT INTO Usuarios (nombre_completo, correo, contrasena_hash, telefono, id_rol, activo)
                OUTPUT INSERTED.id_usuario
                VALUES (@nombre, @correo, @hash, @telefono, @rol, 1)", conn))
            {
                cmd.Parameters.AddWithValue("@nombre", model.NombreCompleto);
                cmd.Parameters.AddWithValue("@correo", model.Correo);
                cmd.Parameters.AddWithValue("@hash", hash);
                cmd.Parameters.AddWithValue("@telefono", model.Telefono ?? "");
                cmd.Parameters.AddWithValue("@rol", model.IdRol);
                nuevoId = (int)cmd.ExecuteScalar()!;
            }

            // Si es Cliente, crear registro en tabla Clientes
            int idRolCliente = ObtenerIdRol("Cliente");
            if (model.IdRol == idRolCliente)
                CrearCliente(nuevoId, model.NombreCompleto, model.Correo, model.Telefono ?? "");

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

            UsuarioEditarViewModel? vm = null;

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                SELECT u.id_usuario, u.nombre_completo, u.correo, u.telefono,
                       u.id_rol, u.activo, r.nombre AS nombre_rol
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
                        IdUsuario = Convert.ToInt32(reader["id_usuario"]),
                        NombreCompleto = reader["nombre_completo"].ToString()!,
                        Correo = reader["correo"].ToString()!,
                        Telefono = reader["telefono"].ToString() ?? "",
                        IdRol = Convert.ToInt32(reader["id_rol"]),
                        Activo = Convert.ToBoolean(reader["activo"]),
                        NombreRol = reader["nombre_rol"].ToString()!
                    };
                }
            }

            if (vm == null) return NotFound();
            ViewBag.Roles = ObtenerRoles();
            return View("~/Views/Usuarios/Usuarios_Editar.cshtml", vm);
        }

        // ══════════════════════════════════════════════════════
        // POST: /Usuarios/Editar/5
        // ══════════════════════════════════════════════════════
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Editar(int id, UsuarioEditarViewModel model)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            // Verificar correo duplicado en otro usuario
            bool correoExiste = false;
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM Usuarios WHERE correo = @correo AND id_usuario != @id", conn))
            {
                cmd.Parameters.AddWithValue("@correo", model.Correo);
                cmd.Parameters.AddWithValue("@id", id);
                correoExiste = (int)cmd.ExecuteScalar()! > 0;
            }

            if (correoExiste)
            {
                ModelState.AddModelError("Correo", "Este correo ya lo usa otro usuario.");
                ViewBag.Roles = ObtenerRoles();
                return View("~/Views/Usuarios/Usuarios_Editar.cshtml", model);
            }

            // Actualizar usuario
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                UPDATE Usuarios
                SET nombre_completo = @nombre,
                    correo          = @correo,
                    telefono        = @telefono,
                    id_rol          = @rol,
                    activo          = @activo
                WHERE id_usuario = @id", conn))
            {
                cmd.Parameters.AddWithValue("@nombre", model.NombreCompleto);
                cmd.Parameters.AddWithValue("@correo", model.Correo);
                cmd.Parameters.AddWithValue("@telefono", model.Telefono ?? "");
                cmd.Parameters.AddWithValue("@rol", model.IdRol);
                cmd.Parameters.AddWithValue("@activo", model.Activo);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }

            // Cambiar contraseña solo si escribió una nueva
            if (!string.IsNullOrWhiteSpace(model.Contrasena))
            {
                var hash = BC.HashPassword(model.Contrasena);
                using var conn = _db.GetConnection();
                using var cmd = new SqlCommand(
                    "UPDATE Usuarios SET contrasena_hash = @hash WHERE id_usuario = @id", conn);
                cmd.Parameters.AddWithValue("@hash", hash);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }

            // Si cambió a Cliente y no tiene registro, crearlo
            int idRolCliente = ObtenerIdRol("Cliente");
            if (model.IdRol == idRolCliente)
            {
                bool clienteExiste = false;
                using (var conn = _db.GetConnection())
                using (var cmd = new SqlCommand(
                    "SELECT COUNT(1) FROM Clientes WHERE id_usuario = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    clienteExiste = (int)cmd.ExecuteScalar()! > 0;
                }
                if (!clienteExiste)
                    CrearCliente(id, model.NombreCompleto, model.Correo, model.Telefono ?? "");
            }

            RegistrarAuditoria($"Editó usuario ID {id}: {model.NombreCompleto}");
            TempData["Exito"] = $"Usuario {model.NombreCompleto} actualizado.";
            return RedirectToAction("Index");
        }

        // ══════════════════════════════════════════════════════
        // POST: /Usuarios/Desactivar/5
        // ══════════════════════════════════════════════════════
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Desactivar(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var idActual = GetIdAdmin();
            if (id == idActual)
            {
                TempData["Error"] = "No puedes desactivar tu propia cuenta.";
                return RedirectToAction("Index");
            }

            string nombre = "";
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                UPDATE Usuarios SET activo = 0 WHERE id_usuario = @id;
                UPDATE Clientes SET activo = 0 WHERE id_usuario = @id;
                SELECT nombre_completo FROM Usuarios WHERE id_usuario = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                nombre = cmd.ExecuteScalar()?.ToString() ?? "";
            }

            RegistrarAuditoria($"Desactivó usuario ID {id}: {nombre}");
            TempData["Exito"] = $"Usuario {nombre} desactivado.";
            return RedirectToAction("Index");
        }

        // ══════════════════════════════════════════════════════
        // POST: /Usuarios/Reactivar/5
        // ══════════════════════════════════════════════════════
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Reactivar(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            string nombre = "";
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                UPDATE Usuarios SET activo = 1 WHERE id_usuario = @id;
                UPDATE Clientes SET activo = 1 WHERE id_usuario = @id;
                SELECT nombre_completo FROM Usuarios WHERE id_usuario = @id;", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                nombre = cmd.ExecuteScalar()?.ToString() ?? "";
            }

            RegistrarAuditoria($"Reactivó usuario ID {id}: {nombre}");
            TempData["Exito"] = $"Usuario {nombre} reactivado.";
            return RedirectToAction("Index");
        }

        // ── Helpers privados ──────────────────────────────────
        private List<Rol> ObtenerRoles()
        {
            var roles = new List<Rol>();
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(
                "SELECT id_rol, nombre FROM Roles ORDER BY id_rol", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                roles.Add(new Rol
                {
                    RolId = Convert.ToInt32(reader["id_rol"]),
                    Nombre = reader["nombre"].ToString()!
                });
            return roles;
        }

        private int ObtenerIdRol(string nombreRol)
        {
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(
                "SELECT id_rol FROM Roles WHERE nombre = @nombre", conn);
            cmd.Parameters.AddWithValue("@nombre", nombreRol);
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }

        private void CrearCliente(int idUsuario, string nombre, string correo, string telefono)
        {
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                INSERT INTO Clientes (id_usuario, nombre_completo, correo, telefono,
                                      tipo_documento, num_documento, direccion, activo)
                VALUES (@idUsuario, @nombre, @correo, @telefono, 'CC', '', '', 1)", conn);
            cmd.Parameters.AddWithValue("@idUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@nombre", nombre);
            cmd.Parameters.AddWithValue("@correo", correo);
            cmd.Parameters.AddWithValue("@telefono", telefono);
            cmd.ExecuteNonQuery();
        }

        private void RegistrarAuditoria(string accion)
        {
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                INSERT INTO LogAuditoria (id_usuario, accion, modulo)
                VALUES (@id, @accion, @modulo)", conn);
            cmd.Parameters.AddWithValue("@id", GetIdAdmin());
            cmd.Parameters.AddWithValue("@accion", accion);
            cmd.Parameters.AddWithValue("@modulo", "Gestión de Usuarios");
            cmd.ExecuteNonQuery();
        }
    }
}