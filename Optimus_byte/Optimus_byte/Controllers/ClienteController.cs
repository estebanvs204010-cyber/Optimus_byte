using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.DATA;
using Optimus_byte.Models;
using Optimus_byte.Models.ViewModels;
using BC = BCrypt.Net.BCrypt;

namespace Optimus_byte.Controllers
{
    public class ClienteController : Controller
    {
        private readonly DbHelper _db;
        private readonly IConfiguration _config;

        public ClienteController(DbHelper db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        private bool EsCliente() =>
            HttpContext.Session.GetString("UsuarioRol") == "Cliente";

        private int GetIdUsuario() =>
            int.Parse(HttpContext.Session.GetString("UsuarioId") ?? "0");

        // Portal
        public IActionResult Portal()
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");
            ViewBag.Nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Cliente";
            return View("~/Views/Cliente/Portal.cshtml");
        }

        // Mis Vehículos
        public IActionResult MisVehiculos()
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var idUsuario = GetIdUsuario();
            var vehiculos = new List<dynamic>();
            int idCliente = 0;

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand("SELECT id_cliente FROM Clientes WHERE id_usuario = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", idUsuario);
                var result = cmd.ExecuteScalar();
                if (result != null) idCliente = Convert.ToInt32(result);
            }

            if (idCliente > 0)
            {
                using var conn = _db.GetConnection();
                using var cmd = new SqlCommand(@"
                    SELECT id_vehiculo, placa, marca, modelo, anio, color,
                           vin, km_actuales, activo, fecha_registro
                    FROM Vehiculos
                    WHERE id_cliente = @idCliente AND activo = 1
                    ORDER BY fecha_registro DESC", conn);
                cmd.Parameters.AddWithValue("@idCliente", idCliente);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    vehiculos.Add(new
                    {
                        IdVehiculo = Convert.ToInt32(reader["id_vehiculo"]),
                        Placa = reader["placa"].ToString()!,
                        Marca = reader["marca"].ToString()!,
                        Modelo = reader["modelo"].ToString()!,
                        Anio = Convert.ToInt32(reader["anio"]),
                        Color = reader["color"]?.ToString() ?? "",
                        Vin = reader["vin"]?.ToString() ?? "",
                        KmActuales = Convert.ToInt32(reader["km_actuales"]),
                        Activo = Convert.ToBoolean(reader["activo"]),
                        FechaRegistro = Convert.ToDateTime(reader["fecha_registro"])
                    });
                }
            }

            ViewBag.Nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Cliente";
            ViewBag.Vehiculos = vehiculos;
            return View("~/Views/Vehiculo/MisVehiculos.cshtml");
        }

        // Agregar Vehículo GET
        public IActionResult AgregarVehiculo(string? returnUrl)
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");
            if (!string.IsNullOrEmpty(returnUrl))
                HttpContext.Session.SetString("VehiculoReturnUrl", returnUrl);
            return View("~/Views/Vehiculo/AgregarVehiculo.cshtml");
        }

        // Agregar Vehículo POST
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AgregarVehiculo(string Placa, string Marca, string Modelo,
            int Anio, string? Color, string? Vin, int KmActuales, string? returnUrl)
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var idUsuario = GetIdUsuario();
            int idCliente = 0;

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand("SELECT id_cliente FROM Clientes WHERE id_usuario = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", idUsuario);
                var result = cmd.ExecuteScalar();
                if (result != null) idCliente = Convert.ToInt32(result);
            }

            if (idCliente == 0)
            {
                TempData["Error"] = "No se encontró tu perfil de cliente.";
                return RedirectToAction("MisVehiculos");
            }

            bool placaExiste = false;
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand("SELECT COUNT(1) FROM Vehiculos WHERE placa = @placa", conn))
            {
                cmd.Parameters.AddWithValue("@placa", Placa.ToUpper());
                placaExiste = (int)cmd.ExecuteScalar()! > 0;
            }

            if (placaExiste)
            {
                TempData["Error"] = "Ya existe un vehículo con esa placa.";
                ViewBag.Nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Cliente";
                return View("~/Views/Vehiculo/AgregarVehiculo.cshtml");
            }

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                INSERT INTO Vehiculos (id_cliente, placa, marca, modelo, anio,
                                       color, vin, km_actuales, activo, fecha_registro)
                VALUES (@idCliente, @placa, @marca, @modelo, @anio,
                        @color, @vin, @km, 1, GETDATE())", conn))
            {
                cmd.Parameters.AddWithValue("@idCliente", idCliente);
                cmd.Parameters.AddWithValue("@placa", Placa.ToUpper().Trim());
                cmd.Parameters.AddWithValue("@marca", Marca.Trim());
                cmd.Parameters.AddWithValue("@modelo", Modelo.Trim());
                cmd.Parameters.AddWithValue("@anio", Anio);
                cmd.Parameters.AddWithValue("@color", (object?)Color ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@vin", (object?)Vin ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@km", KmActuales);
                cmd.ExecuteNonQuery();
            }

            RegistrarAuditoria($"Registró vehículo placa {Placa.ToUpper()}");
            TempData["Exito"] = $"Vehículo {Placa.ToUpper()} registrado correctamente.";
            if (!string.IsNullOrEmpty(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("MisVehiculos");
        }

        // Editar Vehículo GET
        public IActionResult EditarVehiculo(int id)
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var idUsuario = GetIdUsuario();
            dynamic? vehiculo = null;

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                SELECT v.id_vehiculo, v.placa, v.marca, v.modelo, v.anio,
                       v.color, v.vin, v.km_actuales
                FROM Vehiculos v
                INNER JOIN Clientes c ON v.id_cliente = c.id_cliente
                WHERE v.id_vehiculo = @id AND c.id_usuario = @idUsuario", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@idUsuario", idUsuario);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    vehiculo = new
                    {
                        IdVehiculo = Convert.ToInt32(reader["id_vehiculo"]),
                        Placa = reader["placa"].ToString()!,
                        Marca = reader["marca"].ToString()!,
                        Modelo = reader["modelo"].ToString()!,
                        Anio = Convert.ToInt32(reader["anio"]),
                        Color = reader["color"]?.ToString() ?? "",
                        Vin = reader["vin"]?.ToString() ?? "",
                        KmActuales = Convert.ToInt32(reader["km_actuales"])
                    };
                }
            }

            if (vehiculo == null) return NotFound();
            ViewBag.Nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Cliente";
            ViewBag.Vehiculo = vehiculo;
            return View("~/Views/Vehiculo/EditarVehiculo.cshtml");
        }

        // Editar Vehículo POST
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult EditarVehiculo(int IdVehiculo, string Placa, string Marca,
            string Modelo, int Anio, string? Color, string? Vin, int KmActuales)
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var idUsuario = GetIdUsuario();

            bool placaExiste = false;
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM Vehiculos WHERE placa = @placa AND id_vehiculo != @id", conn))
            {
                cmd.Parameters.AddWithValue("@placa", Placa.ToUpper());
                cmd.Parameters.AddWithValue("@id", IdVehiculo);
                placaExiste = (int)cmd.ExecuteScalar()! > 0;
            }

            if (placaExiste)
            {
                TempData["Error"] = "Ya existe un vehículo con esa placa.";
                return RedirectToAction("EditarVehiculo", new { id = IdVehiculo });
            }

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                UPDATE Vehiculos
                SET placa = @placa, marca = @marca, modelo = @modelo,
                    anio = @anio, color = @color, vin = @vin, km_actuales = @km
                WHERE id_vehiculo = @id
                  AND id_cliente = (SELECT id_cliente FROM Clientes WHERE id_usuario = @idUsuario)", conn))
            {
                cmd.Parameters.AddWithValue("@placa", Placa.ToUpper().Trim());
                cmd.Parameters.AddWithValue("@marca", Marca.Trim());
                cmd.Parameters.AddWithValue("@modelo", Modelo.Trim());
                cmd.Parameters.AddWithValue("@anio", Anio);
                cmd.Parameters.AddWithValue("@color", (object?)Color ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@vin", (object?)Vin ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@km", KmActuales);
                cmd.Parameters.AddWithValue("@id", IdVehiculo);
                cmd.Parameters.AddWithValue("@idUsuario", idUsuario);
                cmd.ExecuteNonQuery();
            }

            RegistrarAuditoria($"Editó vehículo placa {Placa.ToUpper()}");
            TempData["Exito"] = $"Vehículo {Placa.ToUpper()} actualizado.";
            return RedirectToAction("MisVehiculos");
        }

        // Desactivar Vehículo
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult DesactivarVehiculo(int id)
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var idUsuario = GetIdUsuario();
            string placa = "";

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                UPDATE Vehiculos SET activo = 0
                OUTPUT DELETED.placa
                WHERE id_vehiculo = @id
                  AND id_cliente = (SELECT id_cliente FROM Clientes WHERE id_usuario = @idUsuario)", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@idUsuario", idUsuario);
                placa = cmd.ExecuteScalar()?.ToString() ?? "";
            }

            RegistrarAuditoria($"Desactivó vehículo placa {placa}");
            TempData["Exito"] = $"Vehículo {placa} desactivado.";
            return RedirectToAction("MisVehiculos");
        }

        // Eliminar Vehículo
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult EliminarVehiculo(int id)
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var idUsuario = GetIdUsuario();

            using var conn = _db.GetConnection();

            // Verifica primero si el vehículo tiene órdenes de trabajo asociadas.
            // Si las tiene, no se puede eliminar (rompería la integridad referencial
            // y se perdería el historial de órdenes/facturas de ese vehículo).
            int ordenesAsociadas;
            using (var cmdCheck = new SqlCommand(@"
                SELECT COUNT(1)
                FROM OrdenesTrabajo o
                INNER JOIN Vehiculos v ON o.id_vehiculo = v.id_vehiculo
                INNER JOIN Clientes  c ON v.id_cliente  = c.id_cliente
                WHERE v.id_vehiculo = @id AND c.id_usuario = @idUsuario", conn))
            {
                cmdCheck.Parameters.AddWithValue("@id", id);
                cmdCheck.Parameters.AddWithValue("@idUsuario", idUsuario);
                ordenesAsociadas = (int)cmdCheck.ExecuteScalar()!;
            }

            if (ordenesAsociadas > 0)
            {
                TempData["Error"] = "No es posible eliminar este vehículo porque tiene órdenes de trabajo asociadas. Si ya no lo usas, puedes desactivarlo en su lugar.";
                return RedirectToAction("MisVehiculos");
            }

            string placa;
            using (var cmd = new SqlCommand(@"
                DELETE FROM Vehiculos
                OUTPUT DELETED.placa
                WHERE id_vehiculo = @id
                  AND id_cliente = (SELECT id_cliente FROM Clientes WHERE id_usuario = @idUsuario)", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@idUsuario", idUsuario);
                placa = cmd.ExecuteScalar()?.ToString() ?? "";
            }

            RegistrarAuditoria($"Eliminó vehículo placa {placa}");
            TempData["Exito"] = $"Vehículo {placa} eliminado.";
            return RedirectToAction("MisVehiculos");
        }

        // Mis Órdenes
        public IActionResult MisOrdenes()
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");
            var idUsuario = GetIdUsuario();
            var ordenes = new List<dynamic>();

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                SELECT o.id_orden,
                       v.placa, v.marca, v.modelo,
                       o.tipo_servicio, o.descripcion_problema,
                       o.diagnostico, o.observaciones, o.estado,
                       o.fecha_apertura, o.fecha_cierre, o.fecha_entrega_estimada
                FROM OrdenesTrabajo o
                INNER JOIN Vehiculos v ON o.id_vehiculo = v.id_vehiculo
                INNER JOIN Clientes  c ON v.id_cliente  = c.id_cliente
                WHERE c.id_usuario = @idUsuario
                ORDER BY o.fecha_apertura DESC", conn))
            {
                cmd.Parameters.AddWithValue("@idUsuario", idUsuario);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    ordenes.Add(new
                    {
                        IdOrden = Convert.ToInt32(reader["id_orden"]),
                        Placa = reader["placa"].ToString()!,
                        Marca = reader["marca"].ToString()!,
                        Modelo = reader["modelo"].ToString()!,
                        TipoServicio = reader["tipo_servicio"].ToString()!,
                        DescripcionProblema = reader["descripcion_problema"].ToString()!,
                        Diagnostico = reader["diagnostico"]?.ToString() ?? "",
                        Observaciones = reader["observaciones"]?.ToString() ?? "",
                        Estado = reader["estado"].ToString()!,
                        FechaApertura = Convert.ToDateTime(reader["fecha_apertura"]),
                        FechaCierre = reader["fecha_cierre"] == DBNull.Value
                                                ? (DateTime?)null
                                                : Convert.ToDateTime(reader["fecha_cierre"]),
                        FechaEntregaEstimada = reader["fecha_entrega_estimada"] == DBNull.Value
                                                ? (DateTime?)null
                                                : Convert.ToDateTime(reader["fecha_entrega_estimada"])
                    });
                }
            }

            ViewBag.Nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Cliente";
            ViewBag.Ordenes = ordenes;
            return View("~/Views/Cliente/MisOrdenes.cshtml");
        }

        // ── GET: Editar Perfil ─────────────────────────────────────────────────
        public IActionResult EditarPerfil()
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var idUsuario = GetIdUsuario();
            var vm = new PerfilViewModel { IdUsuario = idUsuario };

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT
                    u.nombre_completo,
                    u.correo,
                    c.foto_url,
                    c.id_cliente,
                    c.telefono,
                    c.direccion,
                    c.tipo_documento,
                    c.num_documento
                FROM Usuarios u
                INNER JOIN Clientes c ON c.id_usuario = u.id_usuario
                WHERE u.id_usuario = @id", conn);

            cmd.Parameters.AddWithValue("@id", idUsuario);
            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                vm.NombreCompleto = reader["nombre_completo"].ToString()!;
                vm.Correo = reader["correo"].ToString()!;
                vm.FotoUrl = reader["foto_url"]?.ToString();
                vm.IdCliente = Convert.ToInt32(reader["id_cliente"]);
                vm.Telefono = reader["telefono"]?.ToString() ?? "";
                vm.Direccion = reader["direccion"]?.ToString() ?? "";
                vm.TipoDocumento = reader["tipo_documento"]?.ToString() ?? "";
                vm.NumeroDocumento = reader["num_documento"]?.ToString() ?? "";
            }

            ViewBag.Nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Cliente";
            return View("~/Views/Cliente/perfil_cliente.cshtml", vm);
        }

        // ── POST: Guardar cambios de perfil ────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarPerfil(PerfilViewModel vm)
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var idUsuario = GetIdUsuario();
            ViewBag.Nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Cliente";

            // La foto es opcional, quitar su validación
            ModelState.Remove("FotoArchivo");

            if (!ModelState.IsValid)
            {
                vm.Error = "Revisa los campos marcados en rojo.";
                return View("~/Views/Cliente/perfil_cliente.cshtml", vm);
            }

            // 1. Verificar contraseña antes de guardar
            string hash = "";
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT contrasena_hash FROM Usuarios WHERE id_usuario = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", idUsuario);
                hash = cmd.ExecuteScalar()?.ToString() ?? "";
            }

            if (string.IsNullOrEmpty(hash) || !BC.Verify(vm.ContrasenaActual, hash))
            {
                vm.Error = "La contraseña es incorrecta. Los cambios no fueron guardados.";

                // Recargar datos de solo lectura
                using var connR = _db.GetConnection();
                using var cmdR = new SqlCommand(
                    "SELECT tipo_documento, num_documento, foto_url FROM Clientes WHERE id_usuario = @id", connR);
                cmdR.Parameters.AddWithValue("@id", idUsuario);
                using var r = cmdR.ExecuteReader();
                if (r.Read())
                {
                    vm.TipoDocumento = r["tipo_documento"]?.ToString() ?? "";
                    vm.NumeroDocumento = r["num_documento"]?.ToString() ?? "";
                    vm.FotoUrl = r["foto_url"]?.ToString();
                }
                return View("~/Views/Cliente/perfil_cliente.cshtml", vm);
            }

            // 2. Procesar foto si se subió una nueva
            string? nuevaFotoUrl = null;
            if (vm.FotoArchivo != null && vm.FotoArchivo.Length > 0)
            {
                var ext = Path.GetExtension(vm.FotoArchivo.FileName).ToLowerInvariant();
                var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                if (!extensionesPermitidas.Contains(ext))
                {
                    vm.Error = "Solo se permiten imágenes JPG, PNG o WEBP.";
                    return View("~/Views/Cliente/perfil_cliente.cshtml", vm);
                }
                if (vm.FotoArchivo.Length > 2 * 1024 * 1024)
                {
                    vm.Error = "La imagen no debe superar 2 MB.";
                    return View("~/Views/Cliente/perfil_cliente.cshtml", vm);
                }

        // Mis Facturas
        public IActionResult MisFacturas()
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var facturas = new List<dynamic>();
            var idUsuario = GetIdUsuario();

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT f.id_factura, f.id_orden, f.total, f.estado_pago, f.fecha_emision
                FROM Facturas f
                INNER JOIN OrdenesTrabajo o ON f.id_orden = o.id_orden
                INNER JOIN Vehiculos v ON o.id_vehiculo = v.id_vehiculo
                INNER JOIN Clientes c ON v.id_cliente = c.id_cliente
                WHERE c.id_usuario = @idUsuario
                ORDER BY f.fecha_emision DESC", conn);

            cmd.Parameters.AddWithValue("@idUsuario", idUsuario);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                facturas.Add(new
                {
                    IdFactura = Convert.ToInt32(reader["id_factura"]),
                    IdOrden = Convert.ToInt32(reader["id_orden"]),
                    Total = Convert.ToDecimal(reader["total"]),
                    EstadoPago = reader["estado_pago"].ToString(),
                    FechaEmision = Convert.ToDateTime(reader["fecha_emision"])
                });
            }

            ViewBag.Nombre = HttpContext.Session.GetString("UsuarioNombre");
            ViewBag.Facturas = facturas;
            return View("~/Views/Cliente/MisFacturas.cshtml");
        }

        // Pagar Factura con PayU
        public IActionResult PagarFactura(int id)
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            using var conn = _db.GetConnection();
            decimal total = 0, iva = 0;
            string correo = HttpContext.Session.GetString("UsuarioCorreo") ?? "";

            using (var cmd = new SqlCommand(@"
                SELECT total, iva FROM Facturas
                WHERE id_factura = @id AND estado_pago = 'Pendiente'", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                if (!r.Read())
                {
                    TempData["Error"] = "Factura no encontrada o ya pagada.";
                    return RedirectToAction("MisFacturas");
                }
                total = Convert.ToDecimal(r["total"]);
                iva = Convert.ToDecimal(r["iva"]);
            }

            string apiKey = _config["PayU:ApiKey"]!;
            string merchantId = _config["PayU:MerchantId"]!;
            string accountId = _config["PayU:AccountId"]!;
            string reference = $"OB-{id}-{DateTime.Now:yyyyMMddHHmm}";
            string amount = total.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            string currency = "COP";

            string raw = $"{apiKey}~{merchantId}~{reference}~{amount}~{currency}";
            string signature;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
                signature = string.Concat(hash.Select(b => b.ToString("x2")));
            }

            var vm = new PayUCheckoutViewModel
            {
                IdFactura = id,
                Total = total,
                Iva = iva,
                Base = total - iva,
                MerchantId = merchantId,
                AccountId = accountId,
                Description = $"Servicio de taller - Factura #{id}",
                ReferenceCode = reference,
                Amount = amount,
                Tax = iva.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                TaxReturnBase = (total - iva).ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                Currency = currency,
                Signature = signature,
                Test = _config["PayU:Test"]!,
                BuyerEmail = correo,
                ResponseUrl = _config["PayU:ResponseUrl"]!,
                ConfirmUrl = _config["PayU:ConfirmUrl"]!,
                CheckoutUrl = _config["PayU:CheckoutUrl"]!
            };

            return View("~/Views/Cliente/PagarConPayU.cshtml", vm);
        }
                var carpeta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "img", "Perfiles");
                Directory.CreateDirectory(carpeta);

                var nombreArchivo = $"{Guid.NewGuid()}{ext}";
                var rutaCompleta = Path.Combine(carpeta, nombreArchivo);

                using var stream = new FileStream(rutaCompleta, FileMode.Create);
                await vm.FotoArchivo.CopyToAsync(stream);

                nuevaFotoUrl = $"/img/Perfiles/{nombreArchivo}";
            }

            // 3. Actualizar BD
            using (var conn = _db.GetConnection())
            {
                // Usuarios: solo nombre y correo
                using var cmdU = new SqlCommand(
                    "UPDATE Usuarios SET nombre_completo = @nombre, correo = @correo WHERE id_usuario = @id",
                    conn);
                cmdU.Parameters.AddWithValue("@nombre", vm.NombreCompleto.Trim());
                cmdU.Parameters.AddWithValue("@correo", vm.Correo.Trim().ToLower());
                cmdU.Parameters.AddWithValue("@id", idUsuario);
                cmdU.ExecuteNonQuery();

                // Clientes: teléfono, dirección y foto (si hay nueva)
                var sqlCliente = nuevaFotoUrl != null
                    ? "UPDATE Clientes SET telefono = @tel, direccion = @dir, foto_url = @foto WHERE id_usuario = @id"
                    : "UPDATE Clientes SET telefono = @tel, direccion = @dir WHERE id_usuario = @id";

                using var cmdC = new SqlCommand(sqlCliente, conn);
                cmdC.Parameters.AddWithValue("@tel", vm.Telefono.Trim());
                cmdC.Parameters.AddWithValue("@dir", vm.Direccion.Trim());
                if (nuevaFotoUrl != null)
                    cmdC.Parameters.AddWithValue("@foto", nuevaFotoUrl);
                cmdC.Parameters.AddWithValue("@id", idUsuario);
                cmdC.ExecuteNonQuery();
            }

            // 4. Actualizar nombre en sesión
            HttpContext.Session.SetString("UsuarioNombre", vm.NombreCompleto.Trim());

            RegistrarAuditoria("Actualizó su perfil");
            TempData["Exito"] = "Perfil actualizado correctamente.";
            return RedirectToAction("EditarPerfil");
        }

        // ── Helper: Auditoría ──────────────────────────────────────────────────
        private void RegistrarAuditoria(string accion)
        {
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                INSERT INTO LogAuditoria (id_usuario, accion, modulo)
                VALUES (@id, @accion, @modulo)", conn);
            cmd.Parameters.AddWithValue("@id", GetIdUsuario());
            cmd.Parameters.AddWithValue("@accion", accion);
            cmd.Parameters.AddWithValue("@modulo", "Perfil");
            cmd.ExecuteNonQuery();
        }

        // ── Verificar contraseña via AJAX ──────────────────────────────────────
        [HttpPost]
        public IActionResult VerificarContrasena([FromBody] VerificarContrasenaRequest request)
        {
            if (!EsCliente()) return Json(new { ok = false });

            var idUsuario = GetIdUsuario();
            string hash = "";

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT contrasena_hash FROM Usuarios WHERE id_usuario = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", idUsuario);
                hash = cmd.ExecuteScalar()?.ToString() ?? "";
            }

            bool ok = !string.IsNullOrEmpty(hash) && BC.Verify(request.Contrasena, hash);
            return Json(new { ok });
        }
    }
}

public class VerificarContrasenaRequest
{
    public string Contrasena { get; set; } = "";
}