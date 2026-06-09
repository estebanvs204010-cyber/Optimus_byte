using BC = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.DATA;
using Optimus_byte.Models.ViewModels;

namespace Optimus_byte.Controllers
{
    public class RecuperarController : Controller
    {
        private readonly DbHelper _db;
        private readonly EmailService _email;

        public RecuperarController(DbHelper db, EmailService email)
        {
            _db = db;
            _email = email;
        }

        // GET: Pantalla inicial — pedir correo
        public IActionResult Index()
        {
            return View(new RecuperarViewModel { Paso = "correo" });
        }

        // POST: Validar correo y enviar código
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarCodigo(RecuperarViewModel model)
        {
            using var conn = _db.GetConnection();

            // Verificar que el correo existe
            var cmd = new SqlCommand(
                "SELECT id_usuario, nombre_completo FROM Usuarios WHERE correo = @correo AND activo = 1",
                conn);
            cmd.Parameters.AddWithValue("@correo", model.Correo);

            string nombreUsuario = "";
            int idUsuario = 0;

            using (var reader = cmd.ExecuteReader())
            {
                if (!reader.Read())
                {
                    model.Error = "No existe una cuenta activa con ese correo.";
                    model.Paso = "correo";
                    return View("Index", model);
                }
                idUsuario = Convert.ToInt32(reader["id_usuario"]);
                nombreUsuario = reader["nombre_completo"].ToString()!;
            }

            // Generar código de 6 dígitos
            string codigo = new Random().Next(100000, 999999).ToString();
            DateTime expiracion = DateTime.Now.AddMinutes(10);

            // Guardar código en BD
            var cmdUpdate = new SqlCommand(@"
                UPDATE Usuarios
                SET reset_codigo = @codigo, reset_expiracion = @exp
                WHERE id_usuario = @id", conn);
            cmdUpdate.Parameters.AddWithValue("@codigo", codigo);
            cmdUpdate.Parameters.AddWithValue("@exp", expiracion);
            cmdUpdate.Parameters.AddWithValue("@id", idUsuario);
            cmdUpdate.ExecuteNonQuery();

            // Enviar correo con el código
            try
            {
                await _email.EnviarCorreoAsync(
                    model.Correo,
                    nombreUsuario,
                    "Código para restablecer contraseña — Optimus Byte",
                    $@"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;
                            background:#0f0f1a;color:#ffffff;padding:30px;border-radius:10px;'>
                        <h1 style='color:#1e3a8a;text-align:center;'>Taller Optimus Byte</h1>
                        <h2>Hola {nombreUsuario},</h2>
                        <p>Recibimos una solicitud para restablecer tu contraseña.</p>
                        <p>Tu código de verificación es:</p>
                        <h1 style='color:#2563eb;text-align:center;
                                   font-size:48px;letter-spacing:10px;'>
                            {codigo}
                        </h1>
                        <p style='text-align:center;color:#aaaaaa;'>
                            Este código expira en <strong>10 minutos</strong>.
                        </p>
                        <p style='color:#aaaaaa;font-size:12px;'>
                            Si no solicitaste esto, ignora este mensaje.
                            Tu contraseña no cambiará.
                        </p>
                    </div>"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error correo recuperación: {ex.Message}");
            }

            // Guardar correo en sesión para los siguientes pasos
            HttpContext.Session.SetString("RecCorreo", model.Correo);

            return View("Index", new RecuperarViewModel
            {
                Correo = model.Correo,
                Paso = "codigo"
            });
        }

        // POST: Validar código ingresado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ValidarCodigo(RecuperarViewModel model)
        {
            string? correo = HttpContext.Session.GetString("RecCorreo");
            if (string.IsNullOrEmpty(correo))
                return RedirectToAction("Index");

            using var conn = _db.GetConnection();
            var cmd = new SqlCommand(@"
                SELECT reset_codigo, reset_expiracion
                FROM Usuarios
                WHERE correo = @correo AND activo = 1", conn);
            cmd.Parameters.AddWithValue("@correo", correo);

            string codigoBD = "";
            DateTime expiracion = DateTime.MinValue;

            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    codigoBD = reader["reset_codigo"]?.ToString() ?? "";
                    expiracion = reader["reset_expiracion"] == DBNull.Value
                        ? DateTime.MinValue
                        : Convert.ToDateTime(reader["reset_expiracion"]);
                }
            }

            // Verificar expiración
            if (expiracion < DateTime.Now)
            {
                return View("Index", new RecuperarViewModel
                {
                    Correo = correo,
                    Paso = "correo",
                    Error = "El código expiró. Solicita uno nuevo."
                });
            }

            // Verificar código
            if (model.Codigo != codigoBD)
            {
                return View("Index", new RecuperarViewModel
                {
                    Correo = correo,
                    Paso = "codigo",
                    Error = "Código incorrecto. Inténtalo de nuevo."
                });
            }

            // Código correcto — pasar a nueva contraseña
            HttpContext.Session.SetString("RecVerificado", "si");

            return View("Index", new RecuperarViewModel
            {
                Correo = correo,
                Paso = "nueva"
            });
        }

        // POST: Guardar nueva contraseña
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GuardarContrasena(RecuperarViewModel model)
        {
            string? correo = HttpContext.Session.GetString("RecCorreo");
            string? verificado = HttpContext.Session.GetString("RecVerificado");

            if (string.IsNullOrEmpty(correo) || verificado != "si")
                return RedirectToAction("Index");

            // Validar contraseña
            if (model.NuevaContrasena.Length < 6)
            {
                return View("Index", new RecuperarViewModel
                {
                    Correo = correo,
                    Paso = "nueva",
                    Error = "La contraseña debe tener al menos 6 caracteres."
                });
            }

            if (model.NuevaContrasena != model.ConfirmarContrasena)
            {
                return View("Index", new RecuperarViewModel
                {
                    Correo = correo,
                    Paso = "nueva",
                    Error = "Las contraseñas no coinciden."
                });
            }

            // Guardar nueva contraseña hasheada y limpiar código
            string hash = BC.HashPassword(model.NuevaContrasena);

            using var conn = _db.GetConnection();
            var cmd = new SqlCommand(@"
                UPDATE Usuarios
                SET contrasena_hash = @hash,
                    reset_codigo = NULL,
                    reset_expiracion = NULL
                WHERE correo = @correo", conn);
            cmd.Parameters.AddWithValue("@hash", hash);
            cmd.Parameters.AddWithValue("@correo", correo);
            cmd.ExecuteNonQuery();

            // Limpiar sesión
            HttpContext.Session.Remove("RecCorreo");
            HttpContext.Session.Remove("RecVerificado");

            TempData["Exito"] = "¡Contraseña actualizada! Ya puedes iniciar sesión.";
            return RedirectToAction("Index", "Login");
        }
    }
}