using BC = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.Models;
using Optimus_byte.Models.ViewModels;
using Optimus_byte.DATA;

namespace Optimus_byte.Controllers
{
    public class LoginController : Controller
    {
        private readonly DbHelper _db;

        public LoginController(DbHelper db) => _db = db;

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

            int    idUsuario    = 0;
            string nombreCompleto = "";
            string contrasenaHash = "";
            string nombreRol    = "";
            bool   activo       = false;

            // Buscar usuario con su rol
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                SELECT u.id_usuario, u.nombre_completo, u.contrasena_hash, u.activo, r.nombre
                FROM Usuarios u
                INNER JOIN Roles r ON u.id_rol = r.id_rol
                WHERE u.correo = @correo", conn))
            {
                cmd.Parameters.AddWithValue("@correo", model.Correo);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    idUsuario     = Convert.ToInt32(reader["id_usuario"]);
                    nombreCompleto = reader["nombre_completo"].ToString()!;
                    contrasenaHash = reader["contrasena_hash"].ToString()!;
                    activo        = Convert.ToBoolean(reader["activo"]);
                    nombreRol     = reader["nombre"].ToString()!;
                }
            }

            // Validar credenciales
            if (idUsuario == 0 || !activo || !BC.Verify(model.Contrasena, contrasenaHash))
            {
                // Registrar intento fallido
                using (var conn = _db.GetConnection())
                using (var cmd = new SqlCommand(
                    "INSERT INTO IntentosFallidos (correo, bloqueado) VALUES (@correo, 0)", conn))
                {
                    cmd.Parameters.AddWithValue("@correo", model.Correo);
                    cmd.ExecuteNonQuery();
                }

                ModelState.AddModelError("", "Correo o contraseña incorrectos.");
                return View("~/Views/Login/Index.cshtml", model);
            }

            // Guardar sesión
            HttpContext.Session.SetString("UsuarioId",     idUsuario.ToString());
            HttpContext.Session.SetString("UsuarioNombre", nombreCompleto);
            HttpContext.Session.SetString("UsuarioRol",    nombreRol);

            // Log auditoría
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                INSERT INTO LogAuditoria (id_usuario, accion, modulo)
                VALUES (@id, @accion, @modulo)", conn))
            {
                cmd.Parameters.AddWithValue("@id",     idUsuario);
                cmd.Parameters.AddWithValue("@accion", "Inicio de sesión exitoso");
                cmd.Parameters.AddWithValue("@modulo", "Autenticación");
                cmd.ExecuteNonQuery();
            }

            return RedirigirPorRol(nombreRol);
        }

        // Helper de redirección
        private IActionResult RedirigirPorRol(string rol) => rol switch
        {
            "Admin"    => RedirectToAction("Index",     "Usuarios"),
            "Mecanico" => RedirectToAction("MisOrdenes","Mecanico"),
            "Cliente"  => RedirectToAction("Portal",    "Cliente"),
            _          => RedirectToAction("Index",     "Home")
        };

        // GET: /Login/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index");
        }
    }
}
