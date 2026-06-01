using BC = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.Models;
using Optimus_byte.Models.ViewModels;
using Optimus_byte.DATA;

namespace Optimus_byte.Controllers
{
    public class RegistroController : Controller
    {
        private readonly DbHelper _db;
        private readonly EmailService _email;

        public RegistroController(DbHelper db, EmailService email)
        {
            _db = db;
            _email = email;
        }

        // GET: /Registro
        [HttpGet]
        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("UsuarioId") != null)
                return RedirectToAction("Index", "Home");

            return View(new RegistroViewModel());
        }

        // POST: /Registro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(RegistroViewModel model)
        {
            using var conn = _db.GetConnection();

            // Verificar correo duplicado
            using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM Usuarios WHERE correo = @correo", conn))
            {
                cmd.Parameters.AddWithValue("@correo", model.Correo);
                if ((int)cmd.ExecuteScalar() > 0)
                    ModelState.AddModelError(nameof(model.Correo),
                        "Este correo ya está registrado en el sistema.");
            }

            // Verificar documento duplicado
            using (var cmd2 = new SqlCommand(
                "SELECT COUNT(1) FROM Clientes WHERE num_documento = @doc", conn))
            {
                cmd2.Parameters.AddWithValue("@doc", model.NumeroDocumento);
                if ((int)cmd2.ExecuteScalar() > 0)
                    ModelState.AddModelError(nameof(model.NumeroDocumento),
                        "Este número de documento ya está registrado.");
            }

            if (!ModelState.IsValid)
                return View(model);

            // Obtener id del rol Cliente
            int idRolCliente = 0;
            using (var cmd3 = new SqlCommand(
                "SELECT id_rol FROM Roles WHERE nombre = 'Cliente'", conn))
            {
                var result = cmd3.ExecuteScalar();
                if (result != null)
                    idRolCliente = Convert.ToInt32(result);
            }

            if (idRolCliente == 0)
            {
                ModelState.AddModelError("", "No existe el rol Cliente en la base de datos.");
                return View(model);
            }

            // Insertar usuario y obtener el id generado
            int nuevoId = 0;
            using (var cmd4 = new SqlCommand(@"
                INSERT INTO Usuarios
                    (nombre_completo, correo, telefono, contrasena_hash, activo, id_rol)
                OUTPUT INSERTED.id_usuario
                VALUES (@nombre, @correo, @tel, @hash, 1, @rol)", conn))
            {
                cmd4.Parameters.AddWithValue("@nombre", model.NombreCompleto);
                cmd4.Parameters.AddWithValue("@correo", model.Correo);
                cmd4.Parameters.AddWithValue("@tel",    model.Telefono);
                cmd4.Parameters.AddWithValue("@hash",   BC.HashPassword(model.Contrasena));
                cmd4.Parameters.AddWithValue("@rol",    idRolCliente);
                nuevoId = (int)cmd4.ExecuteScalar();
            }

            // Insertar cliente
            using (var cmd5 = new SqlCommand(@"
                INSERT INTO Clientes
                    (id_usuario, nombre_completo, tipo_documento, num_documento,
                     telefono, correo, direccion, activo, fecha_registro)
                VALUES (@idu, @nombre, @tipo, @doc, @tel, @correo, @dir, 1, GETDATE())", conn))
            {
                cmd5.Parameters.AddWithValue("@idu",    nuevoId);
                cmd5.Parameters.AddWithValue("@nombre", model.NombreCompleto);
                cmd5.Parameters.AddWithValue("@tipo",   model.TipoDocumento);
                cmd5.Parameters.AddWithValue("@doc",    model.NumeroDocumento);
                cmd5.Parameters.AddWithValue("@tel",    model.Telefono);
                cmd5.Parameters.AddWithValue("@correo", model.Correo);
                cmd5.Parameters.AddWithValue("@dir",    model.Direccion);
                cmd5.ExecuteNonQuery();
            }

            TempData["RegistroExitoso"] =
                $"Cuenta creada. Bienvenido {model.NombreCompleto}, ya puedes iniciar sesión.";
            try
            {
                await _email.EnviarCorreoAsync(
                    model.Correo,
                    model.NombreCompleto,
                    "¡Bienvenido a Optimus Byte!",
                    $@"
            <div style='font-family:Arial,sans-serif;max-width:600px;
                        margin:auto;background:#1a1a2e;color:#ffffff;
                        padding:30px;border-radius:10px;'>
                <h1 style='color:#f0a500;text-align:center;'>
                    Taller Optimus Byte
                </h1>
                <h2>Hola {model.NombreCompleto},</h2>
                <p>Tu cuenta fue creada exitosamente.</p>
                <p><strong>Correo:</strong> {model.Correo}</p>
                <hr style='border-color:#f0a500;'>
                <p style='color:#aaaaaa;font-size:12px;'>
                    Si no solicitaste esta cuenta ignora este mensaje.
                </p>
            </div>"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error correo: {ex.Message}");
            }
            return RedirectToAction("Index", "Login");
        }
    }
}
