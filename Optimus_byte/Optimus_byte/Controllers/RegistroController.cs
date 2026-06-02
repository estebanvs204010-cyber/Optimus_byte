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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(RegistroViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            using var conn = _db.GetConnection();
    

            // Verificar correo duplicado
            var cmdCheck = new SqlCommand(
                "SELECT COUNT(1) FROM Usuarios WHERE correo = @correo", conn);
            cmdCheck.Parameters.AddWithValue("@correo", model.Correo);
            if ((int)cmdCheck.ExecuteScalar() > 0)
            {
                model.Error = "Este correo ya está registrado.";
                return View(model);
            }

            // Verificar documento duplicado
            var cmdDoc = new SqlCommand(
                "SELECT COUNT(1) FROM Clientes WHERE num_documento = @doc", conn);
            cmdDoc.Parameters.AddWithValue("@doc", model.NumeroDocumento);
            if ((int)cmdDoc.ExecuteScalar() > 0)
            {
                model.Error = "Este número de documento ya está registrado.";
                return View(model);
            }

            // Generar código OTP de 6 dígitos
            string codigo = new Random().Next(100000, 999999).ToString();
            DateTime expiracion = DateTime.Now.AddMinutes(10);

            // Guardar datos temporalmente en sesión
            HttpContext.Session.SetString("RegNombre", model.NombreCompleto);
            HttpContext.Session.SetString("RegCorreo", model.Correo);
            HttpContext.Session.SetString("RegTipo", model.TipoDocumento);
            HttpContext.Session.SetString("RegDoc", model.NumeroDocumento);
            HttpContext.Session.SetString("RegTel", model.Telefono);
            HttpContext.Session.SetString("RegDir", model.Direccion ?? "");
            HttpContext.Session.SetString("RegPassword", model.Contrasena);
            HttpContext.Session.SetString("RegCodigo", codigo);
            HttpContext.Session.SetString("RegExpira", expiracion.ToString());

            // Enviar correo con el código
            try
            {
                await _email.EnviarCorreoAsync(
                    model.Correo,
                    model.NombreCompleto,
                    "Código de verificación — Taller Optimus Byte",
                    $@"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;
                    background:#1a1a2e;color:#ffffff;padding:30px;border-radius:10px;'>
                <h1 style='color:#f0a500;text-align:center;'>Taller Optimus Byte</h1>
                <h2>Hola {model.NombreCompleto},</h2>
                <p>Tu código de verificación es:</p>
                <h1 style='color:#f0a500;text-align:center;
                           font-size:48px;letter-spacing:10px;'>
                    {codigo}
                </h1>
                <p style='text-align:center;color:#aaaaaa;'>
                    Este código expira en <strong>10 minutos</strong>.
                </p>
                <hr style='border-color:#f0a500;'>
                <p style='color:#aaaaaa;font-size:12px;'>
                    Si no solicitaste este registro, ignora este mensaje.
                </p>
            </div>"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando código: {ex.Message}");
                model.Error = "No se pudo enviar el código al correo. Verifica que sea válido.";
                return View(model);
            }

            // Redirigir a pantalla de verificación
            return RedirectToAction("Verificar");
        }

        // GET: Pantalla de verificación
        public IActionResult Verificar()
        {
            string? correo = HttpContext.Session.GetString("RegCorreo");
            if (string.IsNullOrEmpty(correo))
                return RedirectToAction("Index");

            return View(new VerificacionViewModel { Correo = correo });
        }

        // POST: Validar código ingresado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerificarCodigo(VerificacionViewModel model)
        {
            string? codigoGuardado = HttpContext.Session.GetString("RegCodigo");
            string? expiraStr = HttpContext.Session.GetString("RegExpira");

            // Verificar que el código no expiró
            if (string.IsNullOrEmpty(codigoGuardado) || string.IsNullOrEmpty(expiraStr))
            {
                model.Error = "La sesión expiró. Por favor regístrate de nuevo.";
                return View("Verificar", model);
            }

            if (DateTime.Parse(expiraStr) < DateTime.Now)
            {
                model.Error = "El código expiró. Por favor regístrate de nuevo.";
                HttpContext.Session.Clear();
                return View("Verificar", model);
            }

            // Verificar que el código es correcto
            if (model.Codigo != codigoGuardado)
            {
                model.Error = "Código incorrecto. Inténtalo de nuevo.";
                model.Correo = HttpContext.Session.GetString("RegCorreo") ?? "";
                return View("Verificar", model);
            }

            // Código correcto — crear la cuenta
            string nombre = HttpContext.Session.GetString("RegNombre")!;
            string correo = HttpContext.Session.GetString("RegCorreo")!;
            string tipo = HttpContext.Session.GetString("RegTipo")!;
            string doc = HttpContext.Session.GetString("RegDoc")!;
            string tel = HttpContext.Session.GetString("RegTel")!;
            string dir = HttpContext.Session.GetString("RegDir")!;
            string password = HttpContext.Session.GetString("RegPassword")!;
            string hash = BC.HashPassword(password);

            using var conn = _db.GetConnection();
           
            // Insertar usuario
            var cmdU = new SqlCommand(@"
        INSERT INTO Usuarios (nombre_completo, correo, telefono, contrasena_hash, id_rol, correo_verificado)
        OUTPUT INSERTED.id_usuario
        VALUES (@nom, @correo, @tel, @hash, 
                (SELECT id_rol FROM Roles WHERE nombre = 'Cliente'), 1)", conn);
            cmdU.Parameters.AddWithValue("@nom", nombre);
            cmdU.Parameters.AddWithValue("@correo", correo);
            cmdU.Parameters.AddWithValue("@tel", tel);
            cmdU.Parameters.AddWithValue("@hash", hash);
            int idUsuario = (int)cmdU.ExecuteScalar();

            // Insertar cliente
            var cmdC = new SqlCommand(@"
        INSERT INTO Clientes 
            (id_usuario, nombre_completo, tipo_documento, 
             num_documento, telefono, correo, direccion)
        VALUES (@uid, @nom, @tipo, @doc, @tel, @correo, @dir)", conn);
            cmdC.Parameters.AddWithValue("@uid", idUsuario);
            cmdC.Parameters.AddWithValue("@nom", nombre);
            cmdC.Parameters.AddWithValue("@tipo", tipo);
            cmdC.Parameters.AddWithValue("@doc", doc);
            cmdC.Parameters.AddWithValue("@tel", tel);
            cmdC.Parameters.AddWithValue("@correo", correo);
            cmdC.Parameters.AddWithValue("@dir", dir);
            cmdC.ExecuteNonQuery();


            try
            {
                await _email.EnviarCorreoAsync(
                    correo,
                    nombre,
                    "¡Bienvenido a Optimus Byte!",
                    $@"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;
                background:#1a1a2e;color:#ffffff;padding:30px;border-radius:10px;'>
            <h1 style='color:#f0a500;text-align:center;'>Taller Optimus Byte</h1>
            <h2>Hola {nombre},</h2>
            <p>Tu cuenta fue creada y tu correo verificado exitosamente.</p>
            <p><strong>Correo:</strong> {correo}</p>
            <hr style='border-color:#f0a500;'>
            <p style='color:#aaaaaa;font-size:12px;'>
                Si no solicitaste esta cuenta ignora este mensaje.
            </p>
        </div>"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error correo bienvenida: {ex.Message}");
            }

            // Limpiar sesión de registro
            HttpContext.Session.Remove("RegNombre");
            HttpContext.Session.Remove("RegCorreo");
            HttpContext.Session.Remove("RegTipo");
            HttpContext.Session.Remove("RegDoc");
            HttpContext.Session.Remove("RegTel");
            HttpContext.Session.Remove("RegDir");
            HttpContext.Session.Remove("RegPassword");
            HttpContext.Session.Remove("RegCodigo");
            HttpContext.Session.Remove("RegExpira");

            TempData["Exito"] = "¡Cuenta creada exitosamente! Ya puedes iniciar sesión.";
            return RedirectToAction("Index", "Login");
        }
    }
}
