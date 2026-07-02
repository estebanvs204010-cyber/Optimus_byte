using Microsoft.AspNetCore.Authorization;
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
        public IActionResult AgregarVehiculo()
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");
            ViewBag.Nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Cliente";
            return View("~/Views/Vehiculo/AgregarVehiculo.cshtml");
        }

        // Agregar Vehículo POST
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AgregarVehiculo(string Placa, string Marca, string Modelo,
            int Anio, string? Color, string? Vin, int KmActuales)
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
            string placa = "";

            using (var conn = _db.GetConnection())
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

        // ─── Mis Órdenes (con fecha_entrega_estimada) ─────────────────────────────
        public IActionResult MisOrdenes()
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var idUsuario = GetIdUsuario();
            var ordenes = new List<dynamic>();

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                SELECT o.id_orden,
                v.placa,
                v.marca,
                v.modelo,
                o.tipo_servicio,
                o.descripcion_problema,
                o.diagnostico,
                o.observaciones,
                 o.estado,
                 o.fecha_apertura,
                o.fecha_cierre,
                 o.fecha_entrega_estimada,
                f.id_factura,
                f.total,
                 f.estado_pago,
                 f.fecha_emision
    
                FROM OrdenesTrabajo o
                INNER JOIN Vehiculos v ON o.id_vehiculo = v.id_vehiculo
                INNER JOIN Clientes c ON v.id_cliente = c.id_cliente
                LEFT JOIN Facturas f ON o.id_orden = f.id_orden
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

        // ── Pagar Catálogo (compra de repuestos) con PayU ──────────────────────
        // Recibe el carrito como JSON (lista de {idRepuesto, cantidad}).
        // Recalcula los precios SIEMPRE desde la BD (nunca confía en el precio
        // que mande el navegador) y registra la compra como 'Pendiente'.
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult PagarCatalogo(string itemsJson)
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var idUsuario = GetIdUsuario();

            List<CarritoItemDto>? items;
            try
            {
                items = System.Text.Json.JsonSerializer.Deserialize<List<CarritoItemDto>>(
                    itemsJson ?? "[]",
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                items = null;
            }

            if (items == null || items.Count == 0)
            {
                TempData["Error"] = "Tu cotización está vacía.";
                return RedirectToAction("Index", "Inventario");
            }

            // 1. Obtener id_cliente
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
                return RedirectToAction("Index", "Inventario");
            }

            // 2. Releer cada repuesto desde la BD: precio real + valida stock
            var detalle = new List<(int IdRepuesto, int Cantidad, decimal PrecioUnitario)>();

            using (var conn = _db.GetConnection())
            {
                foreach (var item in items)
                {
                    if (item.Cantidad <= 0) continue;

                    using var cmd = new SqlCommand(@"
                        SELECT precio_unitario, stock_actual
                        FROM Repuestos
                        WHERE id_repuesto = @id AND activo = 1", conn);
                    cmd.Parameters.AddWithValue("@id", item.IdRepuesto);
                    using var r = cmd.ExecuteReader();
                    if (!r.Read()) continue;

                    var precio = Convert.ToDecimal(r["precio_unitario"]);
                    var stock = Convert.ToInt32(r["stock_actual"]);
                    var cantidad = Math.Min(item.Cantidad, stock);
                    if (cantidad <= 0) continue;

                    detalle.Add((item.IdRepuesto, cantidad, precio));
                }
            }

            if (detalle.Count == 0)
            {
                TempData["Error"] = "Los productos seleccionados ya no están disponibles.";
                return RedirectToAction("Index", "Inventario");
            }

            decimal subtotal = detalle.Sum(d => d.Cantidad * d.PrecioUnitario);
            decimal iva = Math.Round(subtotal * 0.19m, 2);
            decimal total = subtotal + iva;
            string reference = $"OB-CAT-{DateTime.Now:yyyyMMddHHmmss}";

            // 3. Insertar cabecera + detalle como 'Pendiente'
            int idCompra;
            using (var conn = _db.GetConnection())
            {
                using (var cmdHead = new SqlCommand(@"
                    INSERT INTO ComprasCatalogo
                        (id_cliente, subtotal, iva, total, estado_pago, referencia_payu, fecha_compra)
                    OUTPUT INSERTED.id_compra
                    VALUES (@idCliente, @subtotal, @iva, @total, 'Pendiente', @ref, GETDATE())", conn))
                {
                    cmdHead.Parameters.AddWithValue("@idCliente", idCliente);
                    cmdHead.Parameters.AddWithValue("@subtotal", subtotal);
                    cmdHead.Parameters.AddWithValue("@iva", iva);
                    cmdHead.Parameters.AddWithValue("@total", total);
                    cmdHead.Parameters.AddWithValue("@ref", reference);
                    idCompra = (int)cmdHead.ExecuteScalar()!;
                }

                foreach (var d in detalle)
                {
                    using var cmdDet = new SqlCommand(@"
                        INSERT INTO DetalleCompraCatalogo
                            (id_compra, id_repuesto, cantidad, precio_unitario, subtotal)
                        VALUES (@idCompra, @idRepuesto, @cantidad, @precio, @sub)", conn);
                    cmdDet.Parameters.AddWithValue("@idCompra", idCompra);
                    cmdDet.Parameters.AddWithValue("@idRepuesto", d.IdRepuesto);
                    cmdDet.Parameters.AddWithValue("@cantidad", d.Cantidad);
                    cmdDet.Parameters.AddWithValue("@precio", d.PrecioUnitario);
                    cmdDet.Parameters.AddWithValue("@sub", d.Cantidad * d.PrecioUnitario);
                    cmdDet.ExecuteNonQuery();
                }
            }

            RegistrarAuditoria($"Generó compra de catálogo #{idCompra} por {total:C0}");

            // 4. Armar firma y datos para el checkout de PayU
            string correo = "";
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand("SELECT correo FROM Usuarios WHERE id_usuario = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", idUsuario);
                correo = cmd.ExecuteScalar()?.ToString() ?? "cliente@optimusbyte.com";
            }

            string apiKey = _config["PayU:ApiKey"]!;
            string merchantId = _config["PayU:MerchantId"]!;
            string accountId = _config["PayU:AccountId"]!;
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
                IdCompra = idCompra,
                Total = total,
                Iva = iva,
                Base = subtotal,
                MerchantId = merchantId,
                AccountId = accountId,
                Description = $"Compra de repuestos - Optimus Byte #{idCompra}",
                ReferenceCode = reference,
                Amount = amount,
                Tax = iva.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                TaxReturnBase = subtotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                Currency = currency,
                Signature = signature,
                Test = _config["PayU:Test"] ?? "1",
                BuyerEmail = correo,
                ResponseUrl = _config["PayU:ResponseUrl"]!,
                ConfirmUrl = _config["PayU:ConfirmUrl"]!,
                CheckoutUrl = _config["PayU:CheckoutUrl"]!
            };

            return View("~/Views/Cliente/PagarConPayU.cshtml", vm);
        }

        // ── Confirmación de pago (PayU llama esta URL server-to-server) ────────
        // PayU envía: reference_sale, state_pol, value, sign, transaction_id, etc.
        // state_pol: 4 = aprobada, 6 = rechazada, 5 = expirada, 7 = pendiente
        [HttpPost, AllowAnonymous]
        public IActionResult ConfirmarPagoCatalogo(
            [FromForm] string reference_sale,
            [FromForm] string state_pol,
            [FromForm] string value,
            [FromForm] string sign,
            [FromForm] string transaction_id)
        {
            // 1. Validar firma de confirmación de PayU para evitar fraude
            string apiKey = _config["PayU:ApiKey"]!;
            string merchantId = _config["PayU:MerchantId"]!;
            decimal valorDecimal = decimal.TryParse(value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
            string amountStr = valorDecimal.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

            string raw = $"{apiKey}~{merchantId}~{reference_sale}~{amountStr}~COP~{state_pol}";
            string signature;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
                signature = string.Concat(hash.Select(b => b.ToString("x2")));
            }

            if (!string.Equals(signature, sign, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(); // firma inválida: se ignora silenciosamente, no se confirma el pago
            }

            string nuevoEstado = state_pol switch
            {
                "4" => "Pagado",
                "6" => "Rechazado",
                "5" => "Rechazado",
                _ => "Pendiente"
            };

            using var conn = _db.GetConnection();

            int idCompra = 0;
            using (var cmdGet = new SqlCommand(
                "SELECT id_compra FROM ComprasCatalogo WHERE referencia_payu = @ref", conn))
            {
                cmdGet.Parameters.AddWithValue("@ref", reference_sale);
                var r = cmdGet.ExecuteScalar();
                if (r != null) idCompra = Convert.ToInt32(r);
            }

            if (idCompra == 0) return Ok();

            using (var cmdUpd = new SqlCommand(@"
                UPDATE ComprasCatalogo
                SET estado_pago = @estado,
                    transaccion_payu = @trans,
                    fecha_pago = CASE WHEN @estado = 'Pagado' THEN GETDATE() ELSE fecha_pago END
                WHERE id_compra = @id AND estado_pago = 'Pendiente'", conn))
            {
                cmdUpd.Parameters.AddWithValue("@estado", nuevoEstado);
                cmdUpd.Parameters.AddWithValue("@trans", (object?)transaction_id ?? DBNull.Value);
                cmdUpd.Parameters.AddWithValue("@id", idCompra);
                cmdUpd.ExecuteNonQuery();
            }

            // 2. Si quedó pagado, descontar stock y dejar trazabilidad en inventario
            if (nuevoEstado == "Pagado")
            {
                using var cmdDet = new SqlCommand(
                    "SELECT id_repuesto, cantidad FROM DetalleCompraCatalogo WHERE id_compra = @id", conn);
                cmdDet.Parameters.AddWithValue("@id", idCompra);

                var items = new List<(int IdRepuesto, int Cantidad)>();
                using (var r = cmdDet.ExecuteReader())
                {
                    while (r.Read())
                        items.Add((Convert.ToInt32(r["id_repuesto"]), Convert.ToInt32(r["cantidad"])));
                }

                foreach (var (idRepuesto, cantidad) in items)
                {
                    int stockAnterior = 0, stockNuevo = 0;
                    using (var cmdStock = new SqlCommand(
                        "SELECT stock_actual FROM Repuestos WHERE id_repuesto = @id", conn))
                    {
                        cmdStock.Parameters.AddWithValue("@id", idRepuesto);
                        var sa = cmdStock.ExecuteScalar();
                        stockAnterior = sa != null ? Convert.ToInt32(sa) : 0;
                    }
                    stockNuevo = Math.Max(0, stockAnterior - cantidad);

                    using (var cmdUpdStock = new SqlCommand(
                        "UPDATE Repuestos SET stock_actual = @nuevo WHERE id_repuesto = @id", conn))
                    {
                        cmdUpdStock.Parameters.AddWithValue("@nuevo", stockNuevo);
                        cmdUpdStock.Parameters.AddWithValue("@id", idRepuesto);
                        cmdUpdStock.ExecuteNonQuery();
                    }

                    using (var cmdMov = new SqlCommand(@"
                        INSERT INTO MovimientosInventario
                            (id_repuesto, id_usuario, tipo_movimiento, cantidad,
                             stock_anterior, stock_nuevo, motivo, fecha_hora)
                        VALUES (@idRepuesto, NULL, 'Salida', @cantidad,
                                @anterior, @nuevo, @motivo, GETDATE())", conn))
                    {
                        cmdMov.Parameters.AddWithValue("@idRepuesto", idRepuesto);
                        cmdMov.Parameters.AddWithValue("@cantidad", cantidad);
                        cmdMov.Parameters.AddWithValue("@anterior", stockAnterior);
                        cmdMov.Parameters.AddWithValue("@nuevo", stockNuevo);
                        cmdMov.Parameters.AddWithValue("@motivo", $"Venta catálogo - compra #{idCompra}");
                        cmdMov.ExecuteNonQuery();
                    }
                }
            }

            return Ok();
        }

        // ── Página a la que PayU redirige al navegador tras el pago ────────────
        public IActionResult RespuestaPagoCatalogo(string referenceCode)
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            dynamic? compra = null;
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                SELECT id_compra, total, estado_pago, fecha_compra
                FROM ComprasCatalogo WHERE referencia_payu = @ref", conn))
            {
                cmd.Parameters.AddWithValue("@ref", referenceCode);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    compra = new
                    {
                        IdCompra = Convert.ToInt32(r["id_compra"]),
                        Total = Convert.ToDecimal(r["total"]),
                        EstadoPago = r["estado_pago"].ToString(),
                        FechaCompra = Convert.ToDateTime(r["fecha_compra"])
                    };
                }
            }

            ViewBag.Nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Cliente";
            ViewBag.Compra = compra;
            return View("~/Views/Cliente/RespuestaPagoCatalogo.cshtml");
        }

        // Helper auditoría
        private void RegistrarAuditoria(string accion)
        {
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                INSERT INTO LogAuditoria (id_usuario, accion, modulo)
                VALUES (@id, @accion, @modulo)", conn);
            cmd.Parameters.AddWithValue("@id", GetIdUsuario());
            cmd.Parameters.AddWithValue("@accion", accion);
            cmd.Parameters.AddWithValue("@modulo", "Vehículos");
            cmd.ExecuteNonQuery();
        }

        // Verificar contraseña via AJAX
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
