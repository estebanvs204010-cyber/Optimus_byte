using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.DATA;
using Optimus_byte.Models.ViewModels;

namespace Optimus_byte.Controllers
{
    public class MecanicoController : Controller
    {
        private readonly DbHelper _db;
        private readonly EmailService _email;

        public MecanicoController(DbHelper db, EmailService email)
        {
            _db = db;
            _email = email;
        }

        private bool EsMecanico() =>
            HttpContext.Session.GetString("UsuarioRol") == "Mecanico";

        public IActionResult Index() => RedirectToAction("MisOrdenes");

        public IActionResult MisOrdenes()
        {
            if (!EsMecanico()) return RedirectToAction("Index", "Login");

            var idMecanico = ObtenerIdUsuarioActual();
            var nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Mecanico";
            var hoy = DateTime.Today;

            using var conn = _db.GetConnection();

            var ordenesAsignadas = ObtenerOrdenesAsignadas(conn, idMecanico);
            var ordenesIds = ordenesAsignadas.Select(o => o.IdOrden).ToList();
            var historialBase = ObtenerHistorial(conn, idMecanico);
            var historialIds = historialBase.Select(o => o.IdOrden).ToList();
            var partesHistorial = ObtenerPartesPorOrden(conn, historialIds);
            var cambiosOrden = ObtenerCambiosOrden(conn, ordenesIds);

            // ── Cargar repuestos disponibles del inventario real ──────────
            var repuestosDisponibles = ObtenerRepuestosDisponibles(conn);

            // ── Alerta de stock bajo (visible para el mecánico) ───────────
            var repsBajoStock = repuestosDisponibles
                .Where(r => r.StockActual < r.StockMinimo)
                .ToList();
            ViewBag.RepuetosBajoStock = repsBajoStock;

            var modelo = new MecanicoDashboardViewModel
            {
                NombreMecanico = nombre,
                RepuestosDisponibles = repuestosDisponibles,

                // ── FRAGMENTO 1: CitasAsignadas con MarcaVehiculo ─────────
                CitasAsignadas = ordenesAsignadas.Select(orden => new CitaAsignadaViewModel
                {
                    Id = orden.IdOrden,
                    FechaHora = orden.FechaApertura,
                    Cliente = string.IsNullOrWhiteSpace(orden.Cliente) ? "Cliente sin registrar" : orden.Cliente,
                    Vehiculo = FormatearVehiculo(orden),
                    Placa = string.IsNullOrWhiteSpace(orden.Placa) ? "Sin placa" : orden.Placa,
                    MarcaVehiculo = orden.Marca,   // ← NUEVO
                    Servicio = $"{orden.TipoServicio}: {Recortar(orden.DescripcionProblema, 55)}",
                    Estado = orden.Estado,
                    TiempoEstimadoMinutos = EstimarMinutos(orden.TipoServicio),
                    Diagnostico = orden.Diagnostico,
                    FechaEntregaEstimada = orden.FechaEntregaEstimada
                }).ToList(),
                CitasClientes = CitasController.ObtenerCitasGenerales(conn, 12),

                HistorialServicios = historialBase.Select(orden => new ServicioVehiculoViewModel
                {
                    Fecha = orden.FechaCierre ?? orden.FechaApertura,
                    Vehiculo = $"{FormatearVehiculo(orden)} - {(string.IsNullOrWhiteSpace(orden.Placa) ? "Sin placa" : orden.Placa)}",
                    Diagnostico = string.IsNullOrWhiteSpace(orden.Diagnostico)
                                            ? Recortar(orden.DescripcionProblema, 100)
                                            : orden.Diagnostico,
                    PartesReemplazadas = partesHistorial.TryGetValue(orden.IdOrden, out var partes)
                                            ? partes
                                            : "Sin repuestos registrados",
                    Tecnico = string.IsNullOrWhiteSpace(orden.Mecanico) ? "Sin mecanico asignado" : orden.Mecanico
                }).ToList(),
                Checklists = CrearChecklists(ordenesAsignadas),
                Evidencias = new List<EvidenciaViewModel>
                {
                    new()
                    {
                        Orden         = ordenesAsignadas.FirstOrDefault() is { } oe ? $"OT-{oe.IdOrden}" : "OT",
                        Tipo          = "Antes",
                        Nota          = "Pendiente de cargar evidencia fotografica inicial.",
                        FechaRegistro = hoy.AddHours(8).AddMinutes(20)
                    }
                },
                Mensajes = cambiosOrden.Any()
                    ? cambiosOrden.Select(cambio => new MensajeInternoViewModel
                    {
                        De = string.IsNullOrWhiteSpace(cambio.Usuario) ? "Sistema" : cambio.Usuario,
                        Mensaje = $"OT-{cambio.IdOrden}: {cambio.Observacion}".Trim(),
                        FechaHora = cambio.FechaCambio,
                        Prioridad = cambio.EstadoNuevo.Equals("Esperando repuesto", StringComparison.OrdinalIgnoreCase)
                                        ? "Alta" : "Media"
                    }).ToList()
                    : new List<MensajeInternoViewModel>
                    {
                        new()
                        {
                            De        = "Sistema",
                            Mensaje   = "No hay notificaciones registradas para tus ordenes asignadas.",
                            FechaHora = DateTime.Now,
                            Prioridad = "Baja"
                        }
                    },
                Manuales = new List<ManualTecnicoViewModel>
                {
                    new() { Titulo = "Torque de ruedas y frenos",   Categoria = "Frenos",        Url = "https://www.autodata-group.com/" },
                    new() { Titulo = "Guia interna de inspeccion",  Categoria = "Taller",        Url = "#" },
                    new() { Titulo = "Intervalos de mantenimiento", Categoria = "Mantenimiento", Url = "https://www.boschaftermarket.com/" }
                },
                Herramientas = new List<HerramientaViewModel>
                {
                    new() { Nombre = "Escaner OBD2",     Codigo = "HER-014", Estado = "Disponible",       UltimoUso = ordenesIds.ElementAtOrDefault(0) > 0 ? $"OT-{ordenesIds[0]}" : "Sin uso" },
                    new() { Nombre = "Torquimetro 1/2",  Codigo = "HER-022", Estado = "En uso",           UltimoUso = ordenesIds.ElementAtOrDefault(1) > 0 ? $"OT-{ordenesIds[1]}" : "Sin uso" },
                    new() { Nombre = "Elevador 2",       Codigo = "EQ-002",  Estado = "Requiere revision",UltimoUso = ordenesIds.ElementAtOrDefault(2) > 0 ? $"OT-{ordenesIds[2]}" : "Sin uso" }
                }
            };

            return View("~/Views/Mecanico/MisOrdenes.cshtml", modelo);
        }

        // ─── Cambiar estado ───────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(
        int ordenId, string nuevoEstado, string observacion)
        {
            if (!EsMecanico()) return RedirectToAction("Index", "Login");

            if (ordenId <= 0 || string.IsNullOrWhiteSpace(nuevoEstado))
            {
                TempData["Error"] = "Selecciona una orden y un estado válido.";
                return RedirectToAction("MisOrdenes");
            }

            var idMecanico = ObtenerIdUsuarioActual();
            using var conn = _db.GetConnection();

            if (!OrdenPerteneceAlMecanico(conn, ordenId, idMecanico))
            {
                TempData["Error"] = "La orden seleccionada no está asignada a tu usuario.";
                return RedirectToAction("MisOrdenes");
            }

            // ── Actualizar estado de la orden ────────────────────────
            using (var cmd = new SqlCommand(@"
        UPDATE OrdenesTrabajo
        SET estado = @estado,
            fecha_cierre = CASE
                WHEN @estado IN ('Entregado','Cancelado')
                THEN GETDATE()
                ELSE fecha_cierre
            END
        WHERE id_orden = @ordenId
          AND id_mecanico = @idMecanico", conn))
            {
                cmd.Parameters.AddWithValue("@estado", nuevoEstado);
                cmd.Parameters.AddWithValue("@ordenId", ordenId);
                cmd.Parameters.AddWithValue("@idMecanico", idMecanico);
                cmd.ExecuteNonQuery();
            }

            // ── Log de cambio de estado ──────────────────────────────
            if (TieneColumnas(conn, "EstadosOrden",
                "id_orden", "id_usuario", "estado_nuevo", "observacion", "fecha_cambio"))
            {
                using var cmdLog = new SqlCommand(@"
            INSERT INTO EstadosOrden
                (id_orden, id_usuario, estado_nuevo, observacion, fecha_cambio)
            VALUES
                (@ordenId, @idUsuario, @estado, @obs, GETDATE())", conn);
                cmdLog.Parameters.AddWithValue("@ordenId", ordenId);
                cmdLog.Parameters.AddWithValue("@idUsuario", idMecanico);
                cmdLog.Parameters.AddWithValue("@estado", nuevoEstado);
                cmdLog.Parameters.AddWithValue("@obs", observacion ?? string.Empty);
                cmdLog.ExecuteNonQuery();
            }

            // ── Obtener datos del cliente para notificar ─────────────
            string correoCliente = "";
            string nombreCliente = "";
            string placa = "";
            string tipoServicio = "";

            using (var cmdInfo = new SqlCommand(@"
        SELECT
            u.correo,
            u.nombre_completo,
            v.placa,
            o.tipo_servicio
        FROM OrdenesTrabajo o
        JOIN Vehiculos v ON v.id_vehiculo = o.id_vehiculo
        JOIN Clientes  c ON c.id_cliente  = v.id_cliente
        JOIN Usuarios  u ON u.id_usuario  = c.id_usuario
        WHERE o.id_orden = @ordenId", conn))
            {
                cmdInfo.Parameters.AddWithValue("@ordenId", ordenId);
                using var reader = cmdInfo.ExecuteReader();
                if (reader.Read())
                {
                    correoCliente = reader["correo"].ToString()!;
                    nombreCliente = reader["nombre_completo"].ToString()!;
                    placa = reader["placa"].ToString()!;
                    tipoServicio = reader["tipo_servicio"].ToString()!;
                }
            }

            // ── Enviar correo de notificación al cliente ─────────────
            if (!string.IsNullOrEmpty(correoCliente))
            {
                string colorEstado = nuevoEstado switch
                {
                    "En proceso" => "#2563eb",
                    "Esperando repuesto" => "#d97706",
                    "Listo para entrega" => "#16a34a",
                    "Entregado" => "#64748b",
                    "Cancelado" => "#dc2626",
                    _ => "#1e3a8a"
                };

                string iconoEstado = nuevoEstado switch
                {
                    "En proceso" => "🔧",
                    "Esperando repuesto" => "📦",
                    "Listo para entrega" => "✅",
                    "Entregado" => "🎉",
                    "Cancelado" => "❌",
                    _ => "🔔"
                };

                string cuerpoHtml = $@"
        <div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;
                    background:#f5f3ef;padding:0;border-radius:10px;overflow:hidden;'>

            <!-- Header -->
            <div style='background:#1e3a8a;padding:24px 32px;text-align:center;'>
                <h1 style='color:#ffffff;margin:0;font-size:1.4rem;letter-spacing:2px;'>
                    ⚙️ OPTIMUS BYTE
                </h1>
                <p style='color:rgba(255,255,255,0.7);margin:6px 0 0;font-size:.85rem;'>
                    Taller Automotriz
                </p>
            </div>

            <!-- Cuerpo -->
            <div style='background:#ffffff;padding:32px;'>
                <p style='color:#1a2332;font-size:1rem;margin:0 0 8px;'>
                    Hola, <strong>{nombreCliente}</strong>
                </p>
                <p style='color:#64748b;font-size:.9rem;margin:0 0 24px;'>
                    Te informamos que el estado de tu orden de trabajo ha sido actualizado.
                </p>

                <!-- Estado -->
                <div style='background:#f8faff;border:2px solid {colorEstado};
                            border-radius:8px;padding:20px;text-align:center;margin-bottom:24px;'>
                    <div style='font-size:2.5rem;margin-bottom:8px;'>{iconoEstado}</div>
                    <div style='font-size:.75rem;color:#64748b;text-transform:uppercase;
                                letter-spacing:.1em;margin-bottom:6px;'>
                        Nuevo estado
                    </div>
                    <div style='font-size:1.3rem;font-weight:700;color:{colorEstado};'>
                        {nuevoEstado}
                    </div>
                </div>

                <!-- Detalles de la orden -->
                <table style='width:100%;border-collapse:collapse;margin-bottom:24px;'>
                    <tr>
                        <td style='padding:10px;background:#f8fafc;border:1px solid #e2e8f0;
                                   font-size:.78rem;color:#64748b;font-weight:600;width:40%;'>
                            Orden de Trabajo
                        </td>
                        <td style='padding:10px;border:1px solid #e2e8f0;font-size:.85rem;
                                   color:#1a2332;font-weight:700;'>
                            OT-{ordenId}
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:10px;background:#f8fafc;border:1px solid #e2e8f0;
                                   font-size:.78rem;color:#64748b;font-weight:600;'>
                            Vehículo (Placa)
                        </td>
                        <td style='padding:10px;border:1px solid #e2e8f0;font-size:.85rem;
                                   color:#1a2332;'>
                            {placa}
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:10px;background:#f8fafc;border:1px solid #e2e8f0;
                                   font-size:.78rem;color:#64748b;font-weight:600;'>
                            Servicio
                        </td>
                        <td style='padding:10px;border:1px solid #e2e8f0;font-size:.85rem;
                                   color:#1a2332;'>
                            {tipoServicio}
                        </td>
                    </tr>
                    {(string.IsNullOrWhiteSpace(observacion) ? "" : $@"
                    <tr>
                        <td style='padding:10px;background:#f8fafc;border:1px solid #e2e8f0;
                                   font-size:.78rem;color:#64748b;font-weight:600;'>
                            Observación
                        </td>
                        <td style='padding:10px;border:1px solid #e2e8f0;font-size:.85rem;
                                   color:#1a2332;'>
                            {observacion}
                        </td>
                    </tr>")}
                </table>

                {(nuevoEstado == "Listo para entrega" ? @"
                <div style='background:#d1fae5;border:1px solid #a7f3d0;border-radius:6px;
                            padding:14px;text-align:center;margin-bottom:24px;'>
                    <p style='color:#065f46;font-weight:700;margin:0;font-size:.9rem;'>
                        ✅ ¡Tu vehículo está listo para ser recogido!
                    </p>
                    <p style='color:#065f46;margin:4px 0 0;font-size:.82rem;'>
                        Por favor acércate al taller para retirar tu vehículo.
                    </p>
                </div>" : "")}

                <p style='color:#94a3b8;font-size:.78rem;text-align:center;margin:0;'>
                    Si tienes alguna pregunta, no dudes en contactarnos.
                </p>
            </div>

            <!-- Footer -->
            <div style='background:#f1f5f9;padding:16px 32px;text-align:center;
                        border-top:1px solid #e2e8f0;'>
                <p style='color:#94a3b8;font-size:.72rem;margin:0;'>
                    Este es un mensaje automático de <strong>Optimus Byte</strong>.
                    Por favor no respondas a este correo.
                </p>
            </div>
        </div>";

                try
                {
                    await _email.EnviarCorreoAsync(
                        correoCliente,
                        nombreCliente,
                        $"[Optimus Byte] Actualización de tu orden OT-{ordenId}: {nuevoEstado}",
                        cuerpoHtml
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al enviar correo de cambio de estado: {ex.Message}");
                }
            }

            RegistrarAuditoria(conn, idMecanico,
                $"Cambio estado OT-{ordenId} a '{nuevoEstado}'.");
            TempData["Exito"] = $"Estado de OT-{ordenId} actualizado a '{nuevoEstado}'.";
            return RedirectToAction("MisOrdenes");
        }

        // ─── Registrar diagnostico ────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegistrarDiagnostico(int ordenId, string diagnostico, string trabajosRealizados)
        {
            if (!EsMecanico()) return RedirectToAction("Index", "Login");

            if (ordenId <= 0 || string.IsNullOrWhiteSpace(diagnostico))
            {
                TempData["Error"] = "Selecciona una orden y escribe el diagnostico.";
                return RedirectToAction("MisOrdenes");
            }

            var idMecanico = ObtenerIdUsuarioActual();
            using var conn = _db.GetConnection();

            if (!OrdenPerteneceAlMecanico(conn, ordenId, idMecanico))
            {
                TempData["Error"] = "La orden seleccionada no esta asignada a tu usuario.";
                return RedirectToAction("MisOrdenes");
            }

            var textoTrabajo = string.IsNullOrWhiteSpace(trabajosRealizados)
                ? string.Empty
                : $"\nTrabajos realizados: {trabajosRealizados.Trim()}";

            using (var cmd = new SqlCommand(@"
                UPDATE OrdenesTrabajo
                SET diagnostico = @diagnostico,
                    observaciones = CONCAT(COALESCE(observaciones + CHAR(13) + CHAR(10), ''), @trabajos)
                WHERE id_orden = @ordenId AND id_mecanico = @idMecanico", conn))
            {
                cmd.Parameters.AddWithValue("@diagnostico", diagnostico.Trim());
                cmd.Parameters.AddWithValue("@trabajos", textoTrabajo);
                cmd.Parameters.AddWithValue("@ordenId", ordenId);
                cmd.Parameters.AddWithValue("@idMecanico", idMecanico);
                cmd.ExecuteNonQuery();
            }

            RegistrarAuditoria(conn, idMecanico, $"Diagnostico registrado en OT-{ordenId}.");
            TempData["Exito"] = $"Diagnostico guardado para OT-{ordenId}.";
            return RedirectToAction("MisOrdenes");
        }

        // ─── Guardar tiempo estimado ──────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GuardarTiempoEstimado(int ordenId, int? horas, DateTime? fechaEntrega)
        {
            if (!EsMecanico()) return RedirectToAction("Index", "Login");

            if (ordenId <= 0 || (horas == null && fechaEntrega == null))
            {
                TempData["Error"] = "Define las horas o una fecha de entrega.";
                return RedirectToAction("MisOrdenes");
            }

            var idMecanico = ObtenerIdUsuarioActual();
            using var conn = _db.GetConnection();

            if (!OrdenPerteneceAlMecanico(conn, ordenId, idMecanico))
            {
                TempData["Error"] = "La orden no esta asignada a tu usuario.";
                return RedirectToAction("MisOrdenes");
            }

            var fechaFinal = fechaEntrega ?? DateTime.Now.AddHours(horas!.Value);

            using (var cmd = new SqlCommand(@"
                UPDATE OrdenesTrabajo
                SET fecha_entrega_estimada = @fecha
                WHERE id_orden = @ordenId AND id_mecanico = @idMecanico", conn))
            {
                cmd.Parameters.AddWithValue("@fecha", fechaFinal);
                cmd.Parameters.AddWithValue("@ordenId", ordenId);
                cmd.Parameters.AddWithValue("@idMecanico", idMecanico);
                cmd.ExecuteNonQuery();
            }

            RegistrarAuditoria(conn, idMecanico,
                $"Tiempo estimado OT-{ordenId}: entrega {fechaFinal:dd/MM/yyyy HH:mm}");
            TempData["Exito"] = $"Entrega estimada para OT-{ordenId}: {fechaFinal:dd/MM/yyyy HH:mm}.";
            return RedirectToAction("MisOrdenes");
        }

        // ─── Solicitar repuesto — inventario real + texto libre ───────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SolicitarRepuesto(int ordenId, int? repuestoId,
            string? nombreLibre, int cantidad, string? motivo)
        {
            if (!EsMecanico()) return RedirectToAction("Index", "Login");

            if (ordenId <= 0)
            {
                TempData["Error"] = "Selecciona una orden válida.";
                return RedirectToAction("MisOrdenes");
            }

            bool usaInventario = repuestoId.HasValue && repuestoId > 0;
            bool usaLibre = !string.IsNullOrWhiteSpace(nombreLibre);

            if (!usaInventario && !usaLibre)
            {
                TempData["Error"] = "Selecciona un repuesto del inventario o escribe el nombre del repuesto.";
                return RedirectToAction("MisOrdenes");
            }

            var idMecanico = ObtenerIdUsuarioActual();
            using var conn = _db.GetConnection();

            if (!OrdenPerteneceAlMecanico(conn, ordenId, idMecanico))
            {
                TempData["Error"] = "La orden no está asignada a tu usuario.";
                return RedirectToAction("MisOrdenes");
            }

            string nombreRepuesto;

            if (usaInventario)
            {
                using var cmdRep = new SqlCommand(@"
                    SELECT nombre FROM Repuestos
                    WHERE id_repuesto = @id AND activo = 1", conn);
                cmdRep.Parameters.AddWithValue("@id", repuestoId!.Value);
                var resultado = cmdRep.ExecuteScalar();

                if (resultado == null || resultado == DBNull.Value)
                {
                    TempData["Error"] = "El repuesto seleccionado no existe en el inventario.";
                    return RedirectToAction("MisOrdenes");
                }
                nombreRepuesto = resultado.ToString()!;
            }
            else
            {
                nombreRepuesto = $"[Sin inventario] {nombreLibre!.Trim()}";
            }

            int cantidadFinal = cantidad <= 0 ? 1 : cantidad;

            using (var cmd = new SqlCommand(@"
                INSERT INTO SolicitudesRepuesto
                    (id_orden, id_mecanico, nombre_repuesto, cantidad, motivo)
                VALUES (@ord, @mec, @nombre, @cant, @motivo)", conn))
            {
                cmd.Parameters.AddWithValue("@ord", ordenId);
                cmd.Parameters.AddWithValue("@mec", idMecanico);
                cmd.Parameters.AddWithValue("@nombre", nombreRepuesto);
                cmd.Parameters.AddWithValue("@cant", cantidadFinal);
                cmd.Parameters.AddWithValue("@motivo", (object?)motivo?.Trim() ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            using (var cmdEstado = new SqlCommand(@"
                UPDATE OrdenesTrabajo
                SET estado = 'Esperando repuesto'
                WHERE id_orden = @ord AND id_mecanico = @mec", conn))
            {
                cmdEstado.Parameters.AddWithValue("@ord", ordenId);
                cmdEstado.Parameters.AddWithValue("@mec", idMecanico);
                cmdEstado.ExecuteNonQuery();
            }

            if (TieneColumnas(conn, "EstadosOrden",
                "id_orden", "id_usuario", "estado_nuevo", "observacion", "fecha_cambio"))
            {
                using var cmdLog = new SqlCommand(@"
                    INSERT INTO EstadosOrden
                        (id_orden, id_usuario, estado_nuevo, observacion, fecha_cambio)
                    VALUES (@ord, @usr, 'Esperando repuesto', @obs, GETDATE())", conn);
                cmdLog.Parameters.AddWithValue("@ord", ordenId);
                cmdLog.Parameters.AddWithValue("@usr", idMecanico);
                cmdLog.Parameters.AddWithValue("@obs",
                    $"Mecánico solicitó '{nombreRepuesto}' × {cantidadFinal}. {motivo}".Trim());
                cmdLog.ExecuteNonQuery();
            }

            RegistrarAuditoria(conn, idMecanico,
                $"Solicitud de repuesto '{nombreRepuesto}' x{cantidadFinal} para OT-{ordenId}");

            TempData["Exito"] = $"Solicitud enviada al administrador: {nombreRepuesto} × {cantidadFinal}.";
            return RedirectToAction("MisOrdenes");
        }

        // ─── Registrar tiempo ─────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegistrarTiempo(int ordenId, int minutos, string observacion)
        {
            if (!EsMecanico()) return RedirectToAction("Index", "Login");

            if (ordenId <= 0 || minutos <= 0)
            {
                TempData["Error"] = "Selecciona una orden y registra minutos validos.";
                return RedirectToAction("MisOrdenes");
            }

            var idMecanico = ObtenerIdUsuarioActual();
            var nota = $"Tiempo registrado: {minutos} min. {observacion ?? string.Empty}".Trim();

            using var conn = _db.GetConnection();
            if (!OrdenPerteneceAlMecanico(conn, ordenId, idMecanico))
            {
                TempData["Error"] = "La orden seleccionada no esta asignada a tu usuario.";
                return RedirectToAction("MisOrdenes");
            }

            using (var cmd = new SqlCommand(@"
                UPDATE OrdenesTrabajo
                SET observaciones = CONCAT(COALESCE(observaciones + CHAR(13) + CHAR(10), ''), @nota)
                WHERE id_orden = @ordenId AND id_mecanico = @idMecanico", conn))
            {
                cmd.Parameters.AddWithValue("@nota", nota);
                cmd.Parameters.AddWithValue("@ordenId", ordenId);
                cmd.Parameters.AddWithValue("@idMecanico", idMecanico);
                cmd.ExecuteNonQuery();
            }

            RegistrarAuditoria(conn, idMecanico, $"Registro tiempo en OT-{ordenId}: {minutos} minutos");
            TempData["Exito"] = $"Tiempo registrado para OT-{ordenId}: {minutos} minutos.";
            return RedirectToAction("MisOrdenes");
        }

        // ─── Enviar mensaje ───────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EnviarMensaje(string para, string mensaje)
        {
            if (!EsMecanico()) return RedirectToAction("Index", "Login");

            if (string.IsNullOrWhiteSpace(mensaje))
            {
                TempData["Error"] = "Escribe un mensaje antes de enviarlo.";
                return RedirectToAction("MisOrdenes");
            }

            var idMecanico = ObtenerIdUsuarioActual();
            using var conn = _db.GetConnection();
            RegistrarAuditoria(conn, idMecanico, $"Mensaje interno para {para}: {mensaje.Trim()}");
            TempData["Exito"] = $"Mensaje enviado a {para}.";
            return RedirectToAction("MisOrdenes");
        }

        // ─── Cargar evidencia ─────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CargarEvidencia(string orden, string tipo, string nota, List<IFormFile> archivos)
        {
            if (!EsMecanico()) return RedirectToAction("Index", "Login");

            var idOrden = ExtraerIdOrden(orden);
            var idMecanico = ObtenerIdUsuarioActual();

            using var conn = _db.GetConnection();
            if (idOrden <= 0 || !OrdenPerteneceAlMecanico(conn, idOrden, idMecanico))
            {
                TempData["Error"] = "La evidencia debe pertenecer a una orden asignada.";
                return RedirectToAction("MisOrdenes");
            }

            RegistrarAuditoria(conn, idMecanico,
                $"Evidencia {tipo} registrada para OT-{idOrden}. Archivos: {archivos?.Count ?? 0}. Nota: {nota}");
            TempData["Exito"] = $"Evidencia registrada para OT-{idOrden}. Archivos recibidos: {archivos?.Count ?? 0}.";
            return RedirectToAction("MisOrdenes");
        }

        // ═════════════════════════════════════════════════════════════════════════
        // PRIVADOS — DATOS
        // ═════════════════════════════════════════════════════════════════════════

        // ── FRAGMENTO 2: ObtenerRepuestosDisponibles con columna Marca ───────────
        private List<RepuestoDisponibleViewModel> ObtenerRepuestosDisponibles(SqlConnection conn)
        {
            var lista = new List<RepuestoDisponibleViewModel>();
            using var cmd = new SqlCommand(@"
                SELECT id_repuesto, nombre, referencia, categoria,
                       precio_unitario, stock_actual, stock_minimo,
                       ISNULL(marca, 'General') AS marca
                FROM Repuestos
                WHERE activo = 1 AND stock_actual > 0
                ORDER BY nombre ASC", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                lista.Add(new RepuestoDisponibleViewModel
                {
                    IdRepuesto = Convert.ToInt32(r["id_repuesto"]),
                    Nombre = r["nombre"].ToString()!,
                    Referencia = r["referencia"].ToString()!,
                    Categoria = r["categoria"].ToString()!,
                    Marca = r["marca"].ToString()!,   // ← NUEVO
                    PrecioUnitario = Convert.ToDecimal(r["precio_unitario"]),
                    StockActual = Convert.ToInt32(r["stock_actual"]),
                    StockMinimo = Convert.ToInt32(r["stock_minimo"])
                });
            return lista;
        }

        private List<OrdenMecanicoData> ObtenerOrdenesAsignadas(SqlConnection conn, int idMecanico)
        {
            var ordenes = new List<OrdenMecanicoData>();
            using var cmd = new SqlCommand(@"
                SELECT TOP 12
                       o.id_orden, o.id_vehiculo, o.estado, o.tipo_servicio,
                       o.descripcion_problema, o.diagnostico, o.fecha_apertura, o.fecha_cierre,
                       o.fecha_entrega_estimada,
                       v.placa, v.marca, v.modelo, v.anio,
                       c.nombre_completo AS cliente_nombre,
                       m.nombre_completo AS mecanico_nombre
                FROM OrdenesTrabajo o
                LEFT JOIN Vehiculos v ON v.id_vehiculo = o.id_vehiculo
                LEFT JOIN Clientes  c ON c.id_cliente  = v.id_cliente
                LEFT JOIN Usuarios  m ON m.id_usuario  = o.id_mecanico
                WHERE o.id_mecanico = @idMecanico
                  AND COALESCE(o.estado,'') NOT IN ('Entregado','Cancelado')
                ORDER BY o.fecha_apertura", conn);

            cmd.Parameters.AddWithValue("@idMecanico", idMecanico);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) ordenes.Add(MapearOrden(reader));
            return ordenes;
        }

        private List<OrdenMecanicoData> ObtenerHistorial(SqlConnection conn, int idMecanico)
        {
            var ordenes = new List<OrdenMecanicoData>();
            using var cmd = new SqlCommand(@"
                WITH VehiculosAsignados AS (
                    SELECT DISTINCT id_vehiculo FROM OrdenesTrabajo
                    WHERE id_mecanico = @idMecanico
                      AND COALESCE(estado,'') NOT IN ('Entregado','Cancelado')
                ),
                OrdenesActivas AS (
                    SELECT id_orden FROM OrdenesTrabajo
                    WHERE id_mecanico = @idMecanico
                      AND COALESCE(estado,'') NOT IN ('Entregado','Cancelado')
                )
                SELECT TOP 10
                       o.id_orden, o.id_vehiculo, o.estado, o.tipo_servicio,
                       o.descripcion_problema, o.diagnostico, o.fecha_apertura, o.fecha_cierre,
                       o.fecha_entrega_estimada,
                       v.placa, v.marca, v.modelo, v.anio,
                       c.nombre_completo AS cliente_nombre,
                       m.nombre_completo AS mecanico_nombre
                FROM OrdenesTrabajo o
                LEFT JOIN Vehiculos v ON v.id_vehiculo = o.id_vehiculo
                LEFT JOIN Clientes  c ON c.id_cliente  = v.id_cliente
                LEFT JOIN Usuarios  m ON m.id_usuario  = o.id_mecanico
                WHERE (o.id_mecanico = @idMecanico
                       OR o.id_vehiculo IN (SELECT id_vehiculo FROM VehiculosAsignados))
                  AND o.id_orden NOT IN (SELECT id_orden FROM OrdenesActivas)
                ORDER BY COALESCE(o.fecha_cierre, o.fecha_apertura) DESC", conn);

            cmd.Parameters.AddWithValue("@idMecanico", idMecanico);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) ordenes.Add(MapearOrden(reader));
            return ordenes;
        }

        private Dictionary<int, string> ObtenerPartesPorOrden(SqlConnection conn, List<int> ordenesIds)
        {
            var partes = new Dictionary<int, List<string>>();
            if (!ordenesIds.Any()) return new Dictionary<int, string>();

            var parametros = ordenesIds.Select((_, i) => $"@id{i}").ToList();
            using var cmd = new SqlCommand($@"
                SELECT ore.id_orden, COALESCE(r.nombre,'Repuesto') AS nombre, ore.cantidad
                FROM OrdenRepuestos ore
                LEFT JOIN Repuestos r ON r.id_repuesto = ore.id_repuesto
                WHERE ore.id_orden IN ({string.Join(", ", parametros)})
                ORDER BY ore.id_orden, r.nombre", conn);

            for (var i = 0; i < ordenesIds.Count; i++)
                cmd.Parameters.AddWithValue(parametros[i], ordenesIds[i]);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var idOrden = Convert.ToInt32(reader["id_orden"]);
                var nombre = ObtenerString(reader, "nombre", "Repuesto");
                var cantidad = ObtenerInt(reader, "cantidad");
                if (!partes.ContainsKey(idOrden)) partes[idOrden] = new List<string>();
                partes[idOrden].Add($"{nombre} x{cantidad}");
            }
            return partes.ToDictionary(p => p.Key, p => string.Join(", ", p.Value));
        }

        private List<CambioOrdenData> ObtenerCambiosOrden(SqlConnection conn, List<int> ordenesIds)
        {
            var cambios = new List<CambioOrdenData>();
            if (!ordenesIds.Any() ||
                !TieneColumnas(conn, "EstadosOrden", "id_orden", "id_usuario", "estado_nuevo", "observacion", "fecha_cambio"))
                return cambios;

            var parametros = ordenesIds.Select((_, i) => $"@id{i}").ToList();
            using var cmd = new SqlCommand($@"
                SELECT TOP 6
                       e.id_orden, e.estado_nuevo, e.observacion, e.fecha_cambio,
                       u.nombre_completo AS usuario_nombre
                FROM EstadosOrden e
                LEFT JOIN Usuarios u ON u.id_usuario = e.id_usuario
                WHERE e.id_orden IN ({string.Join(", ", parametros)})
                ORDER BY e.fecha_cambio DESC", conn);

            for (var i = 0; i < ordenesIds.Count; i++)
                cmd.Parameters.AddWithValue(parametros[i], ordenesIds[i]);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                cambios.Add(new CambioOrdenData
                {
                    IdOrden = Convert.ToInt32(reader["id_orden"]),
                    EstadoNuevo = ObtenerString(reader, "estado_nuevo", "Sin estado"),
                    Observacion = ObtenerString(reader, "observacion", string.Empty),
                    FechaCambio = ObtenerDateTime(reader, "fecha_cambio", DateTime.Now),
                    Usuario = ObtenerString(reader, "usuario_nombre", "Sistema")
                });
            return cambios;
        }

        private bool OrdenPerteneceAlMecanico(SqlConnection conn, int ordenId, int idMecanico)
        {
            using var cmd = new SqlCommand(@"
                SELECT COUNT(1) FROM OrdenesTrabajo
                WHERE id_orden = @ordenId AND id_mecanico = @idMecanico", conn);
            cmd.Parameters.AddWithValue("@ordenId", ordenId);
            cmd.Parameters.AddWithValue("@idMecanico", idMecanico);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private void RegistrarAuditoria(SqlConnection conn, int idUsuario, string accion)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO LogAuditoria (id_usuario, accion, modulo)
                VALUES (@id, @accion, @modulo)", conn);
            cmd.Parameters.AddWithValue("@id", idUsuario);
            cmd.Parameters.AddWithValue("@accion", accion);
            cmd.Parameters.AddWithValue("@modulo", "Panel de Mecanico");
            cmd.ExecuteNonQuery();
        }

        private bool TieneColumnas(SqlConnection conn, string tabla, params string[] columnas)
        {
            using var cmd = new SqlCommand(@"
                SELECT COUNT(1) FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @tabla
                  AND COLUMN_NAME IN (" +
                string.Join(", ", columnas.Select((_, i) => $"@col{i}")) + ")", conn);
            cmd.Parameters.AddWithValue("@tabla", tabla);
            for (var i = 0; i < columnas.Length; i++)
                cmd.Parameters.AddWithValue($"@col{i}", columnas[i]);
            return Convert.ToInt32(cmd.ExecuteScalar()) == columnas.Length;
        }

        private int ObtenerIdUsuarioActual() =>
            int.TryParse(HttpContext.Session.GetString("UsuarioId"), out var id) ? id : 0;

        private static int ExtraerIdOrden(string orden)
        {
            if (string.IsNullOrWhiteSpace(orden)) return 0;
            var valor = orden.Replace("OT-", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            return int.TryParse(valor, out var idOrden) ? idOrden : 0;
        }

        private static OrdenMecanicoData MapearOrden(SqlDataReader reader) => new()
        {
            IdOrden = Convert.ToInt32(reader["id_orden"]),
            IdVehiculo = Convert.ToInt32(reader["id_vehiculo"]),
            Estado = ObtenerString(reader, "estado", "Sin estado"),
            TipoServicio = ObtenerString(reader, "tipo_servicio", "Servicio"),
            DescripcionProblema = ObtenerString(reader, "descripcion_problema", "Sin descripcion"),
            Diagnostico = ObtenerString(reader, "diagnostico", string.Empty),
            FechaApertura = ObtenerDateTime(reader, "fecha_apertura", DateTime.Now),
            FechaCierre = ObtenerDateTimeNullable(reader, "fecha_cierre"),
            FechaEntregaEstimada = ObtenerDateTimeNullable(reader, "fecha_entrega_estimada"),
            Placa = ObtenerString(reader, "placa", string.Empty),
            Marca = ObtenerString(reader, "marca", string.Empty),
            Modelo = ObtenerString(reader, "modelo", string.Empty),
            Anio = ObtenerInt(reader, "anio"),
            Cliente = ObtenerString(reader, "cliente_nombre", string.Empty),
            Mecanico = ObtenerString(reader, "mecanico_nombre", string.Empty)
        };

        private static List<ChecklistTecnicoViewModel> CrearChecklists(List<OrdenMecanicoData> ordenes) =>
            new()
            {
                new()
                {
                    Nombre = "Inspeccion inicial",
                    Orden  = ordenes.FirstOrDefault() is { } p ? $"OT-{p.IdOrden}" : "OT",
                    Pasos  = new()
                    {
                        "Verificar kilometraje y nivel de combustible",
                        "Registrar estado exterior del vehiculo",
                        "Inspeccionar llantas y presion",
                        "Revisar luces, pito y limpiaparabrisas",
                        "Confirmar sintomas reportados por el cliente"
                    }
                },
                new()
                {
                    Nombre = "Mantenimiento preventivo",
                    Orden  = ordenes.Skip(1).FirstOrDefault() is { } s ? $"OT-{s.IdOrden}" : "OT",
                    Pasos  = new()
                    {
                        "Drenar aceite usado",
                        "Cambiar filtros",
                        "Revisar niveles de fluidos",
                        "Escanear codigos de falla",
                        "Probar el vehiculo antes de entregar"
                    }
                }
            };

        private static int EstimarMinutos(string tipoServicio) =>
            tipoServicio.Equals("Preventivo", StringComparison.OrdinalIgnoreCase) ? 120 : 90;

        private static string FormatearVehiculo(OrdenMecanicoData o)
        {
            var v = $"{o.Marca} {o.Modelo} {(o.Anio > 0 ? o.Anio.ToString() : string.Empty)}".Trim();
            return string.IsNullOrWhiteSpace(v) ? "Vehiculo sin registrar" : v;
        }

        private static string Recortar(string? texto, int max)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "Sin descripcion";
            return texto.Length <= max ? texto : texto[..max] + "...";
        }

        private static string ObtenerString(SqlDataReader r, string col, string def)
        {
            var ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? def : r.GetValue(ord).ToString() ?? def;
        }

        private static int ObtenerInt(SqlDataReader r, string col)
        {
            var ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? 0 : Convert.ToInt32(r.GetValue(ord));
        }

        private static DateTime ObtenerDateTime(SqlDataReader r, string col, DateTime def)
        {
            var ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? def : Convert.ToDateTime(r.GetValue(ord));
        }

        private static DateTime? ObtenerDateTimeNullable(SqlDataReader r, string col)
        {
            var ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? null : Convert.ToDateTime(r.GetValue(ord));
        }

        // ── Clases internas de datos ──────────────────────────────────────────────
        private sealed class OrdenMecanicoData
        {
            public int IdOrden { get; set; }
            public int IdVehiculo { get; set; }
            public string Estado { get; set; } = string.Empty;
            public string TipoServicio { get; set; } = string.Empty;
            public string DescripcionProblema { get; set; } = string.Empty;
            public string Diagnostico { get; set; } = string.Empty;
            public DateTime FechaApertura { get; set; }
            public DateTime? FechaCierre { get; set; }
            public DateTime? FechaEntregaEstimada { get; set; }
            public string Placa { get; set; } = string.Empty;
            public string Marca { get; set; } = string.Empty;
            public string Modelo { get; set; } = string.Empty;
            public int Anio { get; set; }
            public string Cliente { get; set; } = string.Empty;
            public string Mecanico { get; set; } = string.Empty;
        }

        private sealed class CambioOrdenData
        {
            public int IdOrden { get; set; }
            public string EstadoNuevo { get; set; } = string.Empty;
            public string Observacion { get; set; } = string.Empty;
            public DateTime FechaCambio { get; set; }
            public string Usuario { get; set; } = string.Empty;
        }
    }
}
