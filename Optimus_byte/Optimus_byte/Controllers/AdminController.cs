using BC = BCrypt.Net.BCrypt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.DATA;
using Optimus_byte.Models;
using Optimus_byte.Models.ViewModels;

namespace Optimus_byte.Controllers
{

    public class AdminController : Controller
    {
        private readonly DbHelper _db;
        private readonly EmailService _email;
        private readonly IConfiguration _config;
public AdminController(DbHelper db, EmailService email, IConfiguration config)
{
    _db = db;
    _email = email;
    _config = config;
}


        private bool EsAdmin() =>
            HttpContext.Session.GetString("UsuarioRol") == "Admin";

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // DASHBOARD
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        public IActionResult Dashboard()
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var vm = new DashboardViewModel();
            using var conn = _db.GetConnection();

            vm.TotalUsuarios = EjecutarScalar<int>(conn, "SELECT COUNT(1) FROM Usuarios");
            vm.TotalClientes = EjecutarScalar<int>(conn, "SELECT COUNT(1) FROM Clientes WHERE activo = 1");
            vm.TotalVehiculos = EjecutarScalar<int>(conn, "SELECT COUNT(1) FROM Vehiculos WHERE activo = 1");
            vm.OrdenesAbiertas = EjecutarScalar<int>(conn,
                "SELECT COUNT(1) FROM OrdenesTrabajo WHERE estado NOT IN ('Entregado','Cancelado')");
            vm.OrdenesHoy = EjecutarScalar<int>(conn,
                "SELECT COUNT(1) FROM OrdenesTrabajo WHERE CAST(fecha_apertura AS DATE) = CAST(GETDATE() AS DATE)");
            vm.CitasHoy = CitasController.ContarCitasHoy(conn);
            vm.ProximasCitas = CitasController.ContarProximasCitas(conn);
            vm.VehiculosMantenimiento = EjecutarScalar<int>(conn,
                "SELECT COUNT(DISTINCT id_vehiculo) FROM OrdenesTrabajo WHERE estado NOT IN ('Entregado','Cancelado')");
            vm.RepuestosBajoStock = EjecutarScalar<int>(conn,
                "SELECT COUNT(1) FROM Repuestos WHERE stock_actual <= stock_minimo AND activo = 1");
            vm.OrdenesAbiertas = EjecutarScalar<int>(conn, "SELECT COUNT(1) FROM OrdenesTrabajo WHERE estado NOT IN ('Entregado','Cancelado')");
            vm.OrdenesHoy = EjecutarScalar<int>(conn, "SELECT COUNT(1) FROM OrdenesTrabajo WHERE CAST(fecha_apertura AS DATE) = CAST(GETDATE() AS DATE)");
            vm.RepuestosBajoStock = EjecutarScalar<int>(conn, "SELECT COUNT(1) FROM Repuestos WHERE stock_actual <= stock_minimo AND activo = 1");
            vm.IngresosMes = EjecutarScalar<decimal>(conn,
                @"SELECT ISNULL(SUM(total),0) FROM Facturas
                  WHERE estado_pago = 'Pagado'
                  AND MONTH(fecha_emision) = MONTH(GETDATE())
                  AND YEAR(fecha_emision)  = YEAR(GETDATE())");

            using (var cmd = new SqlCommand(@"
                SELECT TOP 8
                    o.id_orden, o.estado, o.tipo_servicio, o.fecha_apertura,
                    o.fecha_entrega_estimada,
                    v.placa, v.marca, v.modelo,
                    c.nombre_completo AS cliente,
                    ISNULL(u.nombre_completo,'Sin asignar') AS mecanico
                FROM OrdenesTrabajo o
                INNER JOIN Vehiculos v ON o.id_vehiculo = v.id_vehiculo
                INNER JOIN Clientes  c ON v.id_cliente  = c.id_cliente
                LEFT  JOIN Usuarios  u ON o.id_mecanico = u.id_usuario
                ORDER BY o.fecha_apertura DESC", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    vm.OrdenesRecientes.Add(new OrdenResumenViewModel
                    {
                        IdOrden = Convert.ToInt32(r["id_orden"]),
                        Estado = r["estado"].ToString()!,
                        TipoServicio = r["tipo_servicio"].ToString()!,
                        FechaApertura = Convert.ToDateTime(r["fecha_apertura"]),
                        FechaEntregaEstimada = r["fecha_entrega_estimada"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["fecha_entrega_estimada"]),
                        Placa = r["placa"].ToString()!,
                        MarcaModelo = $"{r["marca"]} {r["modelo"]}",
                        Cliente = r["cliente"].ToString()!,
                        Mecanico = r["mecanico"].ToString()!
                    });
            }

            vm.CitasRecientes = CitasController.ObtenerCitasGenerales(conn, 8);

            // Repuestos con bajo stock
            using (var cmd2 = new SqlCommand(@"
                SELECT TOP 5 nombre, referencia, stock_actual, stock_minimo
                FROM Repuestos
                WHERE stock_actual <= stock_minimo AND activo = 1
                ORDER BY stock_actual ASC", conn))
            using (var r2 = cmd2.ExecuteReader())
            {
                while (r2.Read())
                    vm.RepuestosCriticos.Add(new RepuestoCriticoViewModel
                    {
                        Nombre = r2["nombre"].ToString()!,
                        Referencia = r2["referencia"].ToString()!,
                        StockActual = Convert.ToInt32(r2["stock_actual"]),
                        StockMinimo = Convert.ToInt32(r2["stock_minimo"])
                    });
            }

            // Campanita en dashboard también
            ViewBag.SolicitudesPendientes = ObtenerSolicitudesPendientes(conn);

            // CAMBIO 2: RepuestosBajoStockList para el badge/panel del Dashboard
            ViewBag.RepuestosBajoStockList = ObtenerRepuestosBajoStock(conn);
            ViewBag.Mecanicos = ObtenerMecanicos(conn);
            return View("~/Views/Admin/Dashboard.cshtml", vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AprobarCita(int idCita, int idVehiculo, string servicio,
    string observaciones, int? idMecanico, int kmIngreso)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            int idAdmin = int.Parse(HttpContext.Session.GetString("UsuarioId")!);

            using var conn = _db.GetConnection();

            // 1. Marcar cita como Aprobada
            using (var cmd = new SqlCommand(@"
        UPDATE CitasCliente
        SET estado = 'Aprobada'
        WHERE id_cita = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", idCita);
                cmd.ExecuteNonQuery();
            }

            var tipoServicioValido = servicio.ToLower() switch
            {
                var s when s.Contains("aceite") ||
                           s.Contains("filtro") ||
                           s.Contains("alineaci") ||
                           s.Contains("balance") ||
                           s.Contains("revision") => "Preventivo",
                _ => "Correctivo"
            };


            // 2. Crear Orden de Trabajo
            int idOrden;
            using (var cmd = new SqlCommand(@"
        INSERT INTO OrdenesTrabajo
            (id_vehiculo, id_mecanico, id_administrador,
             tipo_servicio, descripcion_problema, km_ingreso)
        OUTPUT INSERTED.id_orden
        VALUES (@veh, @mec, @adm, @tipo, @desc, @km)", conn))
            {
                cmd.Parameters.AddWithValue("@veh", idVehiculo);
                cmd.Parameters.AddWithValue("@mec", (object?)idMecanico ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@adm", idAdmin);
                cmd.Parameters.AddWithValue("@tipo", tipoServicioValido);
                cmd.Parameters.AddWithValue("@desc", observaciones ?? "Cita aprobada");
                cmd.Parameters.AddWithValue("@km", kmIngreso);
                idOrden = (int)cmd.ExecuteScalar();
            }

            // 3. Registrar estado inicial
            RegistrarEstado(conn, idOrden, idAdmin, "Pendiente", "Orden creada desde cita #" + idCita);

            TempData["Exito"] = $"Cita aprobada. Orden de trabajo OT-{idOrden} creada.";
            return RedirectToAction("Dashboard");
        }

        // ════════════════════════════════════════════════
        // ÓRDENES DE TRABAJO — Lista
        // ════════════════════════════════════════════════
        public IActionResult Ordenes(string? estado, string? buscar)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var lista = new List<OrdenResumenViewModel>();

            string where = "WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(estado))
                where += $" AND o.estado = '{estado.Replace("'", "''")}'";
            if (!string.IsNullOrWhiteSpace(buscar))
                where += $@" AND (v.placa LIKE '%{buscar.Replace("'", "''")}%'
                              OR c.nombre_completo LIKE '%{buscar.Replace("'", "''")}%')";

            using var conn = _db.GetConnection();
            using (var cmd = new SqlCommand($@"
                SELECT o.id_orden, o.estado, o.tipo_servicio,
                       o.fecha_apertura, o.fecha_cierre,
                       o.fecha_entrega_estimada,
                       v.placa, v.marca, v.modelo,
                       c.nombre_completo AS cliente,
                       ISNULL(u.nombre_completo,'Sin asignar') AS mecanico,
                       adm.nombre_completo AS administrador
                FROM OrdenesTrabajo o
                INNER JOIN Vehiculos v   ON o.id_vehiculo      = v.id_vehiculo
                INNER JOIN Clientes  c   ON v.id_cliente       = c.id_cliente
                LEFT  JOIN Usuarios  u   ON o.id_mecanico      = u.id_usuario
                INNER JOIN Usuarios  adm ON o.id_administrador = adm.id_usuario
                {where}
                ORDER BY o.fecha_apertura DESC", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    lista.Add(new OrdenResumenViewModel
                    {
                        IdOrden = Convert.ToInt32(r["id_orden"]),
                        Estado = r["estado"].ToString()!,
                        TipoServicio = r["tipo_servicio"].ToString()!,
                        FechaApertura = Convert.ToDateTime(r["fecha_apertura"]),
                        FechaCierre = r["fecha_cierre"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["fecha_cierre"]),
                        FechaEntregaEstimada = r["fecha_entrega_estimada"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["fecha_entrega_estimada"]),
                        Placa = r["placa"].ToString()!,
                        MarcaModelo = $"{r["marca"]} {r["modelo"]}",
                        Cliente = r["cliente"].ToString()!,
                        Mecanico = r["mecanico"].ToString()!,
                        Administrador = r["administrador"].ToString()!
                    });
            }

            ViewBag.Mecanicos = ObtenerMecanicos(conn);
            ViewBag.EstadoFiltro = estado ?? "";
            ViewBag.BuscarFiltro = buscar ?? "";
            ViewBag.SolicitudesPendientes = ObtenerSolicitudesPendientes(conn);

            var vehiculosLista = new List<dynamic>();
            using (var cmdV = new SqlCommand(@"
                SELECT v.id_vehiculo, v.placa, v.marca, v.modelo,
                       u.nombre_completo AS cliente_nombre
                FROM Vehiculos v
                INNER JOIN Clientes c ON v.id_cliente = c.id_cliente
                INNER JOIN Usuarios u ON c.id_usuario = u.id_usuario
                WHERE v.activo = 1
                ORDER BY v.placa", conn))
            using (var rV = cmdV.ExecuteReader())
            {
                while (rV.Read())
                    vehiculosLista.Add(new
                    {
                        IdVehiculo = Convert.ToInt32(rV["id_vehiculo"]),
                        Placa = rV["placa"].ToString()!,
                        Marca = rV["marca"].ToString()!,
                        Modelo = rV["modelo"].ToString()!,
                        ClienteNombre = rV["cliente_nombre"].ToString()!
                    });
            }
            ViewBag.VehiculosLista = vehiculosLista;

            return View("~/Views/Admin/Ordenes.cshtml", lista);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // NOTIFICACIONES â€” Solicitudes de repuesto
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        public IActionResult SolicitudesRepuesto()
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            using var conn = _db.GetConnection();
            var lista = ObtenerSolicitudesPendientes(conn);
            ViewBag.SolicitudesPendientes = lista;
            ViewBag.RepuestosBajoStockList = ObtenerRepuestosBajoStock(conn);
            return View("~/Views/Admin/SolicitudesRepuesto.cshtml", lista);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AtenderSolicitud(int idSolicitud)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(
                "UPDATE SolicitudesRepuesto SET atendida = 1 WHERE id_solicitud = @id", conn);
            cmd.Parameters.AddWithValue("@id", idSolicitud);
            cmd.ExecuteNonQuery();
            TempData["Exito"] = "Solicitud marcada como atendida.";
            return RedirectToAction("SolicitudesRepuesto");
        }

        // CAMBIO 1: Nuevo endpoint de polling para badge (reemplaza SolicitudesPendientesCount)
        public IActionResult NotificacionesCount()
        {
            if (!EsAdmin()) return Json(new { solicitudes = 0, stockBajo = 0 });
            using var conn = _db.GetConnection();

            int solicitudes = EjecutarScalar<int>(conn,
                "SELECT COUNT(1) FROM SolicitudesRepuesto WHERE atendida = 0");

            int stockBajo = EjecutarScalar<int>(conn,
                "SELECT COUNT(1) FROM Repuestos WHERE activo = 1 AND stock_actual < stock_minimo");

            return Json(new { solicitudes, stockBajo });
        }

        // Mantenido por compatibilidad con llamadas existentes
        public IActionResult SolicitudesPendientesCount()
        {
            if (!EsAdmin()) return Json(new { count = 0 });
            using var conn = _db.GetConnection();
            int count = EjecutarScalar<int>(conn,
                "SELECT COUNT(1) FROM SolicitudesRepuesto WHERE atendida = 0");
            return Json(new { count });
        }

        // ════════════════════════════════════════════════
        // ACEPTAR SOLICITUD → descuenta stock + asigna repuesto a la orden
        // ════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AceptarSolicitudRepuesto(int solicitudId, int ordenId,
            string repuestoNombre, int cantidad)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            int idAdmin = int.Parse(HttpContext.Session.GetString("UsuarioId")!);

            using var conn = _db.GetConnection();

            // 1. Buscar el repuesto en inventario por nombre exacto
            int idRepuesto = 0;
            int stockActual = 0;
            decimal precioUnitario = 0;

            using (var cmdBuscar = new SqlCommand(@"
                SELECT TOP 1 id_repuesto, stock_actual, precio_unitario
                FROM Repuestos
                WHERE nombre = @nombre AND activo = 1
                ORDER BY id_repuesto", conn))
            {
                cmdBuscar.Parameters.AddWithValue("@nombre", repuestoNombre);
                using var rBuscar = cmdBuscar.ExecuteReader();
                if (rBuscar.Read())
                {
                    idRepuesto = Convert.ToInt32(rBuscar["id_repuesto"]);
                    stockActual = Convert.ToInt32(rBuscar["stock_actual"]);
                    precioUnitario = Convert.ToDecimal(rBuscar["precio_unitario"]);
                }
            }

            if (idRepuesto == 0)
            {
                TempData["Error"] = $"No se encontró '{repuestoNombre}' en inventario. Verifica el nombre.";
                return RedirectToAction("SolicitudesRepuesto");
            }

            if (stockActual < cantidad)
            {
                TempData["Error"] = $"Stock insuficiente para '{repuestoNombre}'. Disponible: {stockActual}, solicitado: {cantidad}.";
                return RedirectToAction("SolicitudesRepuesto");
            }

            // 2. Descontar stock
            using (var cmdStock = new SqlCommand(@"
                UPDATE Repuestos
                SET stock_actual = stock_actual - @cantidad
                WHERE id_repuesto = @idRep AND stock_actual >= @cantidad", conn))
            {
                cmdStock.Parameters.AddWithValue("@cantidad", cantidad);
                cmdStock.Parameters.AddWithValue("@idRep", idRepuesto);
                int filas = cmdStock.ExecuteNonQuery();
                if (filas == 0)
                {
                    TempData["Error"] = "No se pudo descontar el stock. Inténtalo de nuevo.";
                    return RedirectToAction("SolicitudesRepuesto");
                }
            }

            // 3. Registrar movimiento de inventario
            RegistrarMovimiento(conn, idRepuesto, idAdmin, ordenId,
                "Salida", cantidad, stockActual,
                $"Asignado a OT-{ordenId} por solicitud de mecÃ¡nico");

            // 4. Insertar o actualizar en OrdenRepuestos
            int yaExiste = EjecutarScalar<int>(conn,
                $"SELECT COUNT(1) FROM OrdenRepuestos WHERE id_orden = {ordenId} AND id_repuesto = {idRepuesto}");

            if (yaExiste > 0)
            {
                using var cmdUpd = new SqlCommand(@"
                    UPDATE OrdenRepuestos
                    SET cantidad = cantidad + @cant
                    WHERE id_orden = @ord AND id_repuesto = @rep", conn);
                cmdUpd.Parameters.AddWithValue("@cant", cantidad);
                cmdUpd.Parameters.AddWithValue("@ord", ordenId);
                cmdUpd.Parameters.AddWithValue("@rep", idRepuesto);
                cmdUpd.ExecuteNonQuery();
            }
            else
            {
                using var cmdIns = new SqlCommand(@"
                    INSERT INTO OrdenRepuestos (id_orden, id_repuesto, cantidad, precio_usado)
                    VALUES (@ord, @rep, @cant, @precio)", conn);
                cmdIns.Parameters.AddWithValue("@ord", ordenId);
                cmdIns.Parameters.AddWithValue("@rep", idRepuesto);
                cmdIns.Parameters.AddWithValue("@cant", cantidad);
                cmdIns.Parameters.AddWithValue("@precio", precioUnitario);
                cmdIns.ExecuteNonQuery();
            }

            // 5. Marcar solicitud como atendida
            using (var cmdAten = new SqlCommand(@"
                UPDATE SolicitudesRepuesto
                SET atendida = 1
                WHERE id_solicitud = @id", conn))
            {
                cmdAten.Parameters.AddWithValue("@id", solicitudId);
                cmdAten.ExecuteNonQuery();
            }

            // 6. Si la orden estaba "Esperando repuesto" → cambiar a "En proceso"
            string estadoActual = EjecutarScalar<string>(conn,
                $"SELECT estado FROM OrdenesTrabajo WHERE id_orden = {ordenId}") ?? "";

            if (estadoActual == "Esperando repuesto")
            {
                using var cmdEst = new SqlCommand(@"
                    UPDATE OrdenesTrabajo SET estado = 'En proceso'
                    WHERE id_orden = @id", conn);
                cmdEst.Parameters.AddWithValue("@id", ordenId);
                cmdEst.ExecuteNonQuery();
            }

            // 7. Registrar en EstadosOrden para que el mecánico y cliente lo vean
            string nuevoEstado = estadoActual == "Esperando repuesto" ? "En proceso" : estadoActual;
            string obsEstado = estadoActual == "Esperando repuesto"
                ? $"Repuesto '{repuestoNombre}' × {cantidad} aprobado. Orden retomada — estado cambiado a En proceso."
                : $"Repuesto '{repuestoNombre}' × {cantidad} asignado a esta orden por el administrador.";

            RegistrarEstado(conn, ordenId, idAdmin, nuevoEstado, obsEstado);


            TempData["Exito"] = $"Repuesto '{repuestoNombre}' × {cantidad} asignado a OT-{ordenId}. Stock actualizado.";
            return RedirectToAction("SolicitudesRepuesto");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // RECHAZAR SOLICITUD
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RechazarSolicitudRepuesto(int solicitudId)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            int idAdmin = int.Parse(HttpContext.Session.GetString("UsuarioId")!);

            using var conn = _db.GetConnection();

            int idOrden = 0;
            string nombreRep = "";
            using (var cmdGet = new SqlCommand(@"
                SELECT id_orden, nombre_repuesto
                FROM SolicitudesRepuesto
                WHERE id_solicitud = @id", conn))
            {
                cmdGet.Parameters.AddWithValue("@id", solicitudId);
                using var rGet = cmdGet.ExecuteReader();
                if (rGet.Read())
                {
                    idOrden = Convert.ToInt32(rGet["id_orden"]);
                    nombreRep = rGet["nombre_repuesto"].ToString()!;
                }
            }

            // Marcar como atendida (rechazada = atendida también para limpiar la lista)
            using (var cmdRech = new SqlCommand(@"
                UPDATE SolicitudesRepuesto
                SET atendida = 1
                WHERE id_solicitud = @id", conn))
            {
                cmdRech.Parameters.AddWithValue("@id", solicitudId);
                cmdRech.ExecuteNonQuery();
            }

            // Notificar al mecánico via EstadosOrden
            if (idOrden > 0)
            {
                RegistrarEstado(conn, idOrden, idAdmin,
                    "Esperando repuesto",
                    $"Solicitud de '{nombreRep}' fue rechazada por el administrador. Por favor contactar para alternativa.");
            }

            TempData["Exito"] = "Solicitud rechazada. El mecánico fue notificado.";
            return RedirectToAction("SolicitudesRepuesto");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // DETALLE ORDEN
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        public IActionResult OrdenDetalle(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            OrdenDetalleViewModel? vm = null;
            using var conn = _db.GetConnection();

            using (var cmd = new SqlCommand(@"
                SELECT o.id_orden, o.estado, o.tipo_servicio,
                       o.descripcion_problema, o.diagnostico, o.observaciones,
                       o.km_ingreso, o.fecha_apertura, o.fecha_cierre,
                       o.id_mecanico,
                       v.placa, v.marca, v.modelo, v.anio, v.color, v.km_actuales,
                       c.nombre_completo AS cliente, c.telefono AS tel_cliente,
                       c.correo AS correo_cliente,
                       ISNULL(u.nombre_completo,'Sin asignar') AS mecanico
                FROM OrdenesTrabajo o
                INNER JOIN Vehiculos v ON o.id_vehiculo = v.id_vehiculo
                INNER JOIN Clientes  c ON v.id_cliente  = c.id_cliente
                LEFT  JOIN Usuarios  u ON o.id_mecanico = u.id_usuario
                WHERE o.id_orden = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                    vm = new OrdenDetalleViewModel
                    {
                        IdOrden = Convert.ToInt32(r["id_orden"]),
                        Estado = r["estado"].ToString()!,
                        TipoServicio = r["tipo_servicio"].ToString()!,
                        DescripcionProblema = r["descripcion_problema"].ToString()!,
                        Diagnostico = r["diagnostico"]?.ToString() ?? "",
                        Observaciones = r["observaciones"]?.ToString() ?? "",
                        KmIngreso = Convert.ToInt32(r["km_ingreso"]),
                        FechaApertura = Convert.ToDateTime(r["fecha_apertura"]),
                        FechaCierre = r["fecha_cierre"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["fecha_cierre"]),
                        IdMecanicoActual = r["id_mecanico"] == DBNull.Value ? null : (int?)Convert.ToInt32(r["id_mecanico"]),
                        Placa = r["placa"].ToString()!,
                        MarcaModelo = $"{r["marca"]} {r["modelo"]} {r["anio"]}",
                        Color = r["color"]?.ToString() ?? "",
                        KmActuales = Convert.ToInt32(r["km_actuales"]),
                        Cliente = r["cliente"].ToString()!,
                        TelefonoCliente = r["tel_cliente"].ToString()!,
                        CorreoCliente = r["correo_cliente"].ToString()!,
                        Mecanico = r["mecanico"].ToString()!
                    };
            }

            if (vm == null) return NotFound();

            using (var cmd2 = new SqlCommand(@"
                SELECT rp.nombre, rp.referencia, orep.cantidad, orep.precio_usado
                FROM OrdenRepuestos orep
                INNER JOIN Repuestos rp ON orep.id_repuesto = rp.id_repuesto
                WHERE orep.id_orden = @id", conn))
            {
                cmd2.Parameters.AddWithValue("@id", id);
                using var r2 = cmd2.ExecuteReader();
                while (r2.Read())
                    vm.Repuestos.Add(new RepuestoUsadoViewModel
                    {
                        Nombre = r2["nombre"].ToString()!,
                        Referencia = r2["referencia"].ToString()!,
                        Cantidad = Convert.ToInt32(r2["cantidad"]),
                        PrecioUsado = Convert.ToDecimal(r2["precio_usado"])
                    });
            }

            using (var cmd3 = new SqlCommand(@"
                SELECT e.estado_nuevo, e.observacion, e.fecha_cambio, u.nombre_completo
                FROM EstadosOrden e
                INNER JOIN Usuarios u ON e.id_usuario = u.id_usuario
                WHERE e.id_orden = @id
                ORDER BY e.fecha_cambio ASC", conn))
            {
                cmd3.Parameters.AddWithValue("@id", id);
                using var r3 = cmd3.ExecuteReader();
                while (r3.Read())
                    vm.Historial.Add(new EstadoHistorialViewModel
                    {
                        EstadoNuevo = r3["estado_nuevo"].ToString()!,
                        Observacion = r3["observacion"]?.ToString() ?? "",
                        FechaCambio = Convert.ToDateTime(r3["fecha_cambio"]),
                        Usuario = r3["nombre_completo"].ToString()!
                    });
            }

            ViewBag.Mecanicos = ObtenerMecanicos(conn);
            ViewBag.SolicitudesPendientes = ObtenerSolicitudesPendientes(conn);
            return View("~/Views/Admin/OrdenDetalle.cshtml", vm);
        }

        // POST: Crear orden  ← AQUÍ va el correo
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearOrden(int idVehiculo, int? idMecanico,
            string tipoServicio, string descripcionProblema, int kmIngreso)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            int idAdmin = int.Parse(HttpContext.Session.GetString("UsuarioId")!);
            int idOrden = 0;

            using var conn = _db.GetConnection();
            using (var cmd = new SqlCommand(@"
                INSERT INTO OrdenesTrabajo
                    (id_vehiculo, id_mecanico, id_administrador, tipo_servicio,
                     descripcion_problema, km_ingreso)
                OUTPUT INSERTED.id_orden
                VALUES (@veh, @mec, @adm, @tipo, @desc, @km)", conn))
            {
                cmd.Parameters.AddWithValue("@veh", idVehiculo);
                cmd.Parameters.AddWithValue("@mec", (object?)idMecanico ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@adm", idAdmin);
                cmd.Parameters.AddWithValue("@tipo", tipoServicio);
                cmd.Parameters.AddWithValue("@desc", descripcionProblema);
                cmd.Parameters.AddWithValue("@km", kmIngreso);
                idOrden = (int)cmd.ExecuteScalar();
            }


            using (var cmdKm = new SqlCommand(@"
                UPDATE Vehiculos
                SET km_actuales = @km
                WHERE id_vehiculo = @idVehiculo", conn))
            {
                cmdKm.Parameters.AddWithValue("@km", kmIngreso);
                cmdKm.Parameters.AddWithValue("@idVehiculo", idVehiculo);
                cmdKm.ExecuteNonQuery();
            }

            await VerificarRecordatorioMantenimiento(idVehiculo, kmIngreso);
            RegistrarEstado(conn, idOrden, idAdmin, "Pendiente", "Orden creada");

            // Actualiza el km_actuales del vehículo con el km de ingreso de esta orden,
            // solo si es mayor al registrado (evita retroceder el odómetro por error).
            using (var cmdKm = new SqlCommand(@"
                UPDATE Vehiculos
                SET km_actuales = @km
                WHERE id_vehiculo = @veh AND km_actuales < @km", conn))
            {
                cmdKm.Parameters.AddWithValue("@km", kmIngreso);
                cmdKm.Parameters.AddWithValue("@veh", idVehiculo);
                cmdKm.ExecuteNonQuery();
            }

            string correoCliente = "";
            string nombreCliente = "";
            string placa = "";

            using (var conEmail = _db.GetConnection())
            {
                var cmdEmail = new SqlCommand(@"
                    SELECT u.nombre_completo, u.correo, v.placa
                    FROM OrdenesTrabajo o
                    JOIN Vehiculos v ON o.id_vehiculo = v.id_vehiculo
                    JOIN Clientes  c ON v.id_cliente  = c.id_cliente
                    JOIN Usuarios  u ON c.id_usuario  = u.id_usuario
                    WHERE o.id_orden = @id", conEmail);
                cmdEmail.Parameters.AddWithValue("@id", idOrden);
                using var reader = cmdEmail.ExecuteReader();
                if (reader.Read())
                {
                    nombreCliente = reader["nombre_completo"].ToString()!;
                    correoCliente = reader["correo"].ToString()!;
                    placa = reader["placa"].ToString()!;
                }
            }

            try
            {
                await _email.EnviarCorreoAsync(
                    correoCliente,
                    nombreCliente,
                    "Nueva orden de trabajo â€” Taller Optimus Byte",
                    $@"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;
                            background:#1a1a2e;color:#ffffff;padding:30px;border-radius:10px;'>
                        <h1 style='color:#f0a500;text-align:center;'>Taller Optimus Byte</h1>
                        <h2>Hola {nombreCliente},</h2>
                        <p>Se ha creado una orden de trabajo para tu vehÃ­culo
                           <strong style='color:#f0a500;'>{placa}</strong>.</p>
                        <p>Puedes seguir el estado de tu orden desde tu portal.</p>
                        <hr style='border-color:#f0a500;'>
                        <p style='color:#aaaaaa;font-size:12px;'>
                            Taller Optimus Byte â€” Sistema de gestiÃ³n automotriz
                        </p>
                    </div>"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error correo orden: {ex.Message}");
            }
            // ─────────────────────────────────────────────────────────

            TempData["Exito"] = $"Orden #{idOrden} creada correctamente.";
            return RedirectToAction("OrdenDetalle", new { id = idOrden });
        }

        // POST: Cambiar estado de orden  ← AQUÍ también va correo de estado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarEstado(int idOrden, string nuevoEstado,
            string? observacion, int? idMecanico)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            int idAdmin = int.Parse(HttpContext.Session.GetString("UsuarioId")!);

            using var conn = _db.GetConnection();

            string sql = "UPDATE OrdenesTrabajo SET estado = @estado";
            if (idMecanico.HasValue)
                sql += ", id_mecanico = @mec";
            if (nuevoEstado == "Entregado" || nuevoEstado == "Cancelado")
                sql += ", fecha_cierre = GETDATE()";
            sql += " WHERE id_orden = @id";

            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@estado", nuevoEstado);
                cmd.Parameters.AddWithValue("@id", idOrden);
                if (idMecanico.HasValue)
                    cmd.Parameters.AddWithValue("@mec", idMecanico.Value);
                cmd.ExecuteNonQuery();
            }

            RegistrarEstado(conn, idOrden, idAdmin, nuevoEstado, observacion ?? "");

            // ── Enviar correo al cliente sobre cambio de estado ───────
            string correoCliente = "";
            string nombreCliente = "";
            string placa = "";

            using (var conEmail = _db.GetConnection())
            {
                var cmdEmail = new SqlCommand(@"
                    SELECT u.nombre_completo, u.correo, v.placa
                    FROM OrdenesTrabajo o
                    JOIN Vehiculos v ON o.id_vehiculo = v.id_vehiculo
                    JOIN Clientes  c ON v.id_cliente  = c.id_cliente
                    JOIN Usuarios  u ON c.id_usuario  = u.id_usuario
                    WHERE o.id_orden = @id", conEmail);
                cmdEmail.Parameters.AddWithValue("@id", idOrden);
                using var reader = cmdEmail.ExecuteReader();
                if (reader.Read())
                {
                    nombreCliente = reader["nombre_completo"].ToString()!;
                    correoCliente = reader["correo"].ToString()!;
                    placa = reader["placa"].ToString()!;
                }
            }

            try
            {
                await _email.EnviarCorreoAsync(
                    correoCliente,
                    nombreCliente,
                    $"Estado actualizado: {nuevoEstado} â€” Taller Optimus Byte",
                    $@"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;
                            background:#1a1a2e;color:#ffffff;padding:30px;border-radius:10px;'>
                        <h1 style='color:#f0a500;text-align:center;'>Taller Optimus Byte</h1>
                        <h2>Hola {nombreCliente},</h2>
                        <p>El estado de tu vehÃ­culo
                           <strong style='color:#f0a500;'>{placa}</strong> cambiÃ³ a:</p>
                        <h2 style='color:#f0a500;text-align:center;'>{nuevoEstado}</h2>
                        {(string.IsNullOrEmpty(observacion) ? "" :
                            $"<p><strong>ObservaciÃ³n:</strong> {observacion}</p>")}
                        <hr style='border-color:#f0a500;'>
                        <p style='color:#aaaaaa;font-size:12px;'>
                            Taller Optimus Byte â€” Sistema de gestiÃ³n automotriz
                        </p>
                    </div>"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error correo estado: {ex.Message}");
            }
            // ─────────────────────────────────────────────────────────

            TempData["Exito"] = $"Estado actualizado a: {nuevoEstado}";
            return RedirectToAction("OrdenDetalle", new { id = idOrden });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActualizarDiagnostico(int idOrden, string? diagnostico, string? observaciones)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                UPDATE OrdenesTrabajo
                SET diagnostico = @diag, observaciones = @obs
                WHERE id_orden = @id", conn);
            cmd.Parameters.AddWithValue("@diag", (object?)diagnostico ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@obs", (object?)observaciones ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", idOrden);
            cmd.ExecuteNonQuery();

            TempData["Exito"] = "DiagnÃ³stico guardado.";
            return RedirectToAction("OrdenDetalle", new { id = idOrden });
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // INVENTARIO
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        public IActionResult Inventario(string? buscar, string? categoria)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var lista = new List<RepuestoViewModel>();

            string where = "WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(buscar))
                where += $" AND (nombre LIKE '%{buscar.Replace("'", "''")}%' OR referencia LIKE '%{buscar.Replace("'", "''")}%')";
            if (!string.IsNullOrWhiteSpace(categoria))
                where += $" AND categoria = '{categoria.Replace("'", "''")}'";

            using var conn = _db.GetConnection();

            using (var cmd = new SqlCommand($@"
   SELECT id_repuesto, nombre, referencia, descripcion,
           categoria, precio_unitario, stock_actual, stock_minimo,
           fecha_registro, imagen_url, marca, modelo, activo
    FROM Repuestos
    {where}
    ORDER BY nombre ASC", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    lista.Add(new RepuestoViewModel
                    {
                        IdRepuesto = Convert.ToInt32(r["id_repuesto"]),
                        Nombre = r["nombre"].ToString()!,
                        Referencia = r["referencia"].ToString()!,
                        Descripcion = r["descripcion"]?.ToString() ?? "",
                        Categoria = r["categoria"].ToString()!,
                        PrecioUnitario = Convert.ToDecimal(r["precio_unitario"]),
                        StockActual = Convert.ToInt32(r["stock_actual"]),
                        StockMinimo = Convert.ToInt32(r["stock_minimo"]),
                        FechaRegistro = Convert.ToDateTime(r["fecha_registro"]),
                        ImagenUrl = r["imagen_url"]?.ToString(),
                        marca = r["marca"]?.ToString() ?? "",    // ← nuevo
                        modelo = r["modelo"]?.ToString() ?? "",
                        Activo = Convert.ToBoolean(r["activo"]) // ← nuevo
                    });
            }

            var cats = new List<string>();
            using (var cmd2 = new SqlCommand(
                "SELECT DISTINCT categoria FROM Repuestos WHERE activo = 1 ORDER BY categoria", conn))
            using (var r2 = cmd2.ExecuteReader())
            {
                while (r2.Read()) cats.Add(r2[0].ToString()!);
            }

            ViewBag.SolicitudesPendientes = ObtenerSolicitudesPendientes(conn);
            // CAMBIO 3: Inventario ya tiene SolicitudesPendientes (el stockBajo lo calcula la vista desde el Model)
            ViewBag.Categorias = cats;
            ViewBag.BuscarFiltro = buscar ?? "";
            ViewBag.CatFiltro = categoria ?? "";
            return View("~/Views/Admin/Inventario.cshtml", lista);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarRepuesto(
    int? idRepuesto, string nombre, string referencia,
    string? descripcion, string categoria, decimal precioUnitario,
    int stockActual, int stockMinimo,
    string? marca, string? modelo,
    string? imagenUrlActual,
    IFormFile? imagenRepuesto)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            int idAdmin = int.Parse(HttpContext.Session.GetString("UsuarioId")!);

            // ── Resolver imagen ──────────────────────────────────────
            string? imagenUrl = null;
            if (imagenRepuesto != null && imagenRepuesto.Length > 0)
            {
                // Subió archivo nuevo → guardarlo
                var ext = Path.GetExtension(imagenRepuesto.FileName).ToLowerInvariant();
                var nombreArchivo = $"{Guid.NewGuid()}{ext}";
                var carpeta = Path.Combine("wwwroot", "img", "repuestos");
                Directory.CreateDirectory(carpeta);
                var ruta = Path.Combine(carpeta, nombreArchivo);
                using var stream = System.IO.File.Create(ruta);
                await imagenRepuesto.CopyToAsync(stream);
                imagenUrl = $"/img/repuestos/{nombreArchivo}";
            }
            else if (!string.IsNullOrEmpty(imagenUrlActual))
            {
                // No subió archivo pero había una URL → mantenerla
                imagenUrl = imagenUrlActual;
            }
            // Si ambos vacíos → imagenUrl queda null (sin imagen propia)

            using var conn = _db.GetConnection();

            if (idRepuesto == null || idRepuesto == 0)
            {
                // ── INSERT ───────────────────────────────────────────
                using var cmd = new SqlCommand(@"
            INSERT INTO Repuestos
                (nombre, referencia, descripcion, categoria,
                 precio_unitario, stock_actual, stock_minimo,
                 marca, modelo, imagen_url)
            OUTPUT INSERTED.id_repuesto
            VALUES (@nom, @ref, @desc, @cat, @precio, @stock, @min,
                    @marca, @modelo, @img)", conn);
                cmd.Parameters.AddWithValue("@nom", nombre);
                cmd.Parameters.AddWithValue("@ref", referencia);
                cmd.Parameters.AddWithValue("@desc", (object?)descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cat", categoria);
                cmd.Parameters.AddWithValue("@precio", precioUnitario);
                cmd.Parameters.AddWithValue("@stock", stockActual);
                cmd.Parameters.AddWithValue("@min", stockMinimo);
                cmd.Parameters.AddWithValue("@marca", (object?)marca ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@modelo", (object?)modelo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@img", (object?)imagenUrl ?? DBNull.Value);
                int newId = (int)cmd.ExecuteScalar();
                RegistrarMovimiento(conn, newId, idAdmin, null, "Entrada", stockActual, 0, "Stock inicial");
                TempData["Exito"] = $"Repuesto '{nombre}' creado correctamente.";
            }
            else
            {
                // ── UPDATE ───────────────────────────────────────────
                int stockAnterior = EjecutarScalar<int>(conn,
                    $"SELECT stock_actual FROM Repuestos WHERE id_repuesto = {idRepuesto}");

                // Si imagenUrl es null → no tocar imagen_url en BD (COALESCE la mantiene)
                string sqlImg = imagenUrl != null
                    ? ", imagen_url = @img"
                    : ", imagen_url = COALESCE(@img, imagen_url)";

                using var cmd = new SqlCommand($@"
            UPDATE Repuestos
            SET nombre          = @nom,
                referencia      = @ref,
                descripcion     = @desc,
                categoria       = @cat,
                precio_unitario = @precio,
                stock_actual    = @stock,
                stock_minimo    = @min,
                marca           = @marca,
                modelo          = @modelo
                {sqlImg}
            WHERE id_repuesto   = @id", conn);
                cmd.Parameters.AddWithValue("@nom", nombre);
                cmd.Parameters.AddWithValue("@ref", referencia);
                cmd.Parameters.AddWithValue("@desc", (object?)descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cat", categoria);
                cmd.Parameters.AddWithValue("@precio", precioUnitario);
                cmd.Parameters.AddWithValue("@stock", stockActual);
                cmd.Parameters.AddWithValue("@min", stockMinimo);
                cmd.Parameters.AddWithValue("@marca", (object?)marca ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@modelo", (object?)modelo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@img", (object?)imagenUrl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@id", idRepuesto);
                cmd.ExecuteNonQuery();

                if (stockActual != stockAnterior)
                {
                    string tipo = stockActual > stockAnterior ? "Entrada" : "Salida";
                    int diff = Math.Abs(stockActual - stockAnterior);
                    RegistrarMovimiento(conn, idRepuesto.Value, idAdmin, null,
                        tipo, diff, stockAnterior, "Ajuste manual de stock");
                }
                TempData["Exito"] = $"Repuesto '{nombre}' actualizado.";
            }
            return RedirectToAction("Inventario");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DesactivarRepuesto(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(
                "UPDATE Repuestos SET activo = 0 WHERE id_repuesto = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            TempData["Exito"] = "Repuesto desactivado.";
            return RedirectToAction("Inventario");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ReactivarRepuesto(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(
                "UPDATE Repuestos SET activo = 1 WHERE id_repuesto = @id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            TempData["Exito"] = "Repuesto reactivado.";
            return RedirectToAction("Inventario");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EliminarRepuesto(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            using var conn = _db.GetConnection();

            // Guardar el nombre antes de borrar para el mensaje
            string nombre = EjecutarScalar<string>(conn,
                $"SELECT nombre FROM Repuestos WHERE id_repuesto = {id}") ?? "Repuesto";

            // 1️⃣ Borrar primero los registros hijos que tienen FK hacia este repuesto
            using (var cmdMov = new SqlCommand(
                "DELETE FROM MovimientosInventario WHERE id_repuesto = @id", conn))
            {
                cmdMov.Parameters.AddWithValue("@id", id);
                cmdMov.ExecuteNonQuery();
            }

            // 2️⃣ Ahora sí borrar el repuesto
            using (var cmdRep = new SqlCommand(
                "DELETE FROM Repuestos WHERE id_repuesto = @id", conn))
            {
                cmdRep.Parameters.AddWithValue("@id", id);
                cmdRep.ExecuteNonQuery();
            }

            TempData["Exito"] = $"Repuesto '{nombre}' eliminado permanentemente.";
            return RedirectToAction("Inventario");
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubirImagenCategoria(string categoria, string catKey, IFormFile? imagen)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            if (imagen != null && imagen.Length > 0)
            {
                var ext = Path.GetExtension(imagen.FileName).ToLowerInvariant();
                var extensionesPermitidas = new[] { ".png", ".jpg", ".jpeg", ".webp" };
                if (!extensionesPermitidas.Contains(ext))
                {
                    TempData["Error"] = "Solo se permiten imagenes PNG, JPG, JPEG o WEBP.";
                    return RedirectToAction("Inventario");
                }

                // Siempre guarda como catKey.ext (ej: motor.png, frenos.jpg)
                var nombreArchivo = $"{catKey}{ext}";
                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var carpeta = Path.Combine(webRoot, "img", "defaults");
                Directory.CreateDirectory(carpeta);

                foreach (var oldExt in extensionesPermitidas)
                {
                    var anterior = Path.Combine(carpeta, $"{catKey}{oldExt}");
                    if (System.IO.File.Exists(anterior))
                    {
                        System.IO.File.Delete(anterior);
                    }
                }

                var ruta = Path.Combine(carpeta, nombreArchivo);

                using (var stream = System.IO.File.Create(ruta))
                {
                    await imagen.CopyToAsync(stream);
                }

                // ── ESTO FALTABA: actualizar la BD con la URL ──────────────
                string urlImagen = $"/img/defaults/{nombreArchivo}";

                using var conn = _db.GetConnection();
                using var cmd = new SqlCommand(@"
            UPDATE Repuestos
            SET imagen_url = @url
            WHERE categoria = @cat", conn);
                cmd.Parameters.AddWithValue("@url", urlImagen);
                cmd.Parameters.AddWithValue("@cat", categoria);
                int filas = cmd.ExecuteNonQuery();
                // ────────────────────────────────────────────────────────

                TempData["Exito"] = $"Imagen de '{categoria}' actualizada. {filas} repuesto(s) afectado(s).";
            }
            else
            {
                TempData["Error"] = "No se seleccionó ninguna imagen.";
            }

            return RedirectToAction("Inventario");
        }
        // ════════════════════════════════════════════════
        // FACTURAS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        public IActionResult Facturas(string? estado, string? buscar)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var lista = new List<FacturaViewModel>();
            string where = "WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(estado))
                where += $" AND f.estado_pago = '{estado.Replace("'", "''")}'";
            if (!string.IsNullOrWhiteSpace(buscar))
                where += $@" AND (c.nombre_completo LIKE '%{buscar.Replace("'", "''")}%'
                              OR v.placa LIKE '%{buscar.Replace("'", "''")}%'
                              OR CAST(f.id_factura AS VARCHAR) LIKE '%{buscar.Replace("'", "''")}%')";

            using var conn = _db.GetConnection();
            using (var cmd = new SqlCommand($@"
                SELECT f.id_factura, f.subtotal, f.iva, f.total,
                       f.estado_pago, f.metodo_pago, f.fecha_emision, f.fecha_pago,
                       o.id_orden, v.placa, v.marca, v.modelo,
                       c.nombre_completo AS cliente
                FROM Facturas f
                INNER JOIN OrdenesTrabajo o ON f.id_orden    = o.id_orden
                INNER JOIN Vehiculos      v ON o.id_vehiculo = v.id_vehiculo
                INNER JOIN Clientes       c ON v.id_cliente  = c.id_cliente
                {where}
                ORDER BY f.fecha_emision DESC", conn))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    lista.Add(new FacturaViewModel
                    {
                        IdFactura = Convert.ToInt32(r["id_factura"]),
                        IdOrden = Convert.ToInt32(r["id_orden"]),
                        Subtotal = Convert.ToDecimal(r["subtotal"]),
                        Iva = Convert.ToDecimal(r["iva"]),
                        Total = Convert.ToDecimal(r["total"]),
                        EstadoPago = r["estado_pago"].ToString()!,
                        MetodoPago = r["metodo_pago"]?.ToString() ?? "",
                        FechaEmision = Convert.ToDateTime(r["fecha_emision"]),
                        FechaPago = r["fecha_pago"] == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(r["fecha_pago"]),
                        Placa = r["placa"].ToString()!,
                        MarcaModelo = $"{r["marca"]} {r["modelo"]}",
                        Cliente = r["cliente"].ToString()!
                    });
            }

            ViewBag.EstadoFiltro = estado ?? "";
            ViewBag.BuscarFiltro = buscar ?? "";
            ViewBag.SolicitudesPendientes = ObtenerSolicitudesPendientes(conn);
            // CAMBIO 4: RepuestosBajoStockList en Facturas
            ViewBag.RepuestosBajoStockList = ObtenerRepuestosBajoStock(conn);
            return View("~/Views/Admin/Facturas.cshtml", lista);
        }

        // POST: Crear factura desde orden  ← AQUÍ también va correo de factura
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearFactura(int idOrden, decimal subtotal, decimal iva)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            int idAdmin = int.Parse(HttpContext.Session.GetString("UsuarioId")!);

            using var conn = _db.GetConnection();
            int existe = EjecutarScalar<int>(conn,
                $"SELECT COUNT(1) FROM Facturas WHERE id_orden = {idOrden}");
            if (existe > 0)
            {
                TempData["Error"] = "Esta orden ya tiene una factura generada.";
                return RedirectToAction("OrdenDetalle", new { id = idOrden });
            }

            decimal total = subtotal + iva;
            using var cmd = new SqlCommand(@"
                INSERT INTO Facturas (id_orden, id_administrador, subtotal, iva, total)
                VALUES (@ord, @adm, @sub, @iva, @tot)", conn);
            cmd.Parameters.AddWithValue("@ord", idOrden);
            cmd.Parameters.AddWithValue("@adm", idAdmin);
            cmd.Parameters.AddWithValue("@sub", subtotal);
            cmd.Parameters.AddWithValue("@iva", iva);
            cmd.Parameters.AddWithValue("@tot", total);
            cmd.ExecuteNonQuery();

            // ── Enviar correo de factura al cliente ───────────────────
            string correoCliente = "";
            string nombreCliente = "";
            string placa = "";

            using (var conEmail = _db.GetConnection())
            {
                var cmdEmail = new SqlCommand(@"
                    SELECT u.nombre_completo, u.correo, v.placa
                    FROM OrdenesTrabajo o
                    JOIN Vehiculos v ON o.id_vehiculo = v.id_vehiculo
                    JOIN Clientes  c ON v.id_cliente  = c.id_cliente
                    JOIN Usuarios  u ON c.id_usuario  = u.id_usuario
                    WHERE o.id_orden = @id", conEmail);
                cmdEmail.Parameters.AddWithValue("@id", idOrden);
                using var reader = cmdEmail.ExecuteReader();
                if (reader.Read())
                {
                    nombreCliente = reader["nombre_completo"].ToString()!;
                    correoCliente = reader["correo"].ToString()!;
                    placa = reader["placa"].ToString()!;
                }
            }

            try
            {
                await _email.EnviarCorreoAsync(
                    correoCliente,
                    nombreCliente,
                    "Tu factura estÃ¡ lista â€” Taller Optimus Byte",
                    $@"<div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;
                            background:#1a1a2e;color:#ffffff;padding:30px;border-radius:10px;'>
                        <h1 style='color:#f0a500;text-align:center;'>Taller Optimus Byte</h1>
                        <h2>Hola {nombreCliente},</h2>
                        <p>Se ha generado tu factura para el vehÃ­culo
                           <strong style='color:#f0a500;'>{placa}</strong>.</p>
                        <table style='width:100%;border-collapse:collapse;margin-top:20px;'>
                            <tr style='background:#f0a500;color:#000;'>
                                <td style='padding:10px;'>Subtotal</td>
                                <td style='padding:10px;text-align:right;'>${subtotal.ToString("N0")}</td>
                            </tr>
                            <tr>
                                <td style='padding:10px;'>IVA (19%)</td>
                                <td style='padding:10px;text-align:right;'>${iva.ToString("N0")}</td>
                            </tr>
                            <tr style='background:#f0a500;color:#000;font-weight:bold;'>
                                <td style='padding:10px;'>TOTAL</td>
                                <td style='padding:10px;text-align:right;'>${total.ToString("N0")}</td>
                            </tr>
                        </table>
                        <p style='margin-top:20px;'>Puedes ver el detalle completo desde tu portal.</p>
                        <hr style='border-color:#f0a500;'>
                        <p style='color:#aaaaaa;font-size:12px;'>
                            Taller Optimus Byte â€” Sistema de gestiÃ³n automotriz
                        </p>
                    </div>"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error correo factura: {ex.Message}");
            }
            // ─────────────────────────────────────────────────────────

            TempData["Exito"] = $"Factura creada por ${total:N0}.";
            return RedirectToAction("Facturas");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegistrarPago(int idFactura, decimal monto,
            string metodo, string? referencia, string? observaciones)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            using var conn = _db.GetConnection();

            using (var cmd = new SqlCommand(@"
                INSERT INTO Pagos (id_factura, monto, metodo, referencia_transaccion, observaciones)
                VALUES (@fac, @monto, @met, @ref, @obs)", conn))
            {
                cmd.Parameters.AddWithValue("@fac", idFactura);
                cmd.Parameters.AddWithValue("@monto", monto);
                cmd.Parameters.AddWithValue("@met", metodo);
                cmd.Parameters.AddWithValue("@ref", (object?)referencia ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@obs", (object?)observaciones ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            using (var cmd2 = new SqlCommand(@"
                UPDATE Facturas
                SET estado_pago = 'Pagado', metodo_pago = @met, fecha_pago = GETDATE()
                WHERE id_factura = @id", conn))
            {
                cmd2.Parameters.AddWithValue("@met", metodo);
                cmd2.Parameters.AddWithValue("@id", idFactura);
                cmd2.ExecuteNonQuery();
            }

            TempData["Exito"] = "Pago registrado correctamente.";
            return RedirectToAction("Facturas");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AnularFactura(int idFactura, string motivo)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                UPDATE Facturas
                SET estado_pago = 'Anulado', motivo_anulacion = @motivo
                WHERE id_factura = @id", conn);
            cmd.Parameters.AddWithValue("@motivo", motivo);
            cmd.Parameters.AddWithValue("@id", idFactura);
            cmd.ExecuteNonQuery();
            TempData["Exito"] = "Factura anulada.";
            return RedirectToAction("Facturas");
        }

        // GET: /Admin/PagarConPayU/5
        public IActionResult PagarConPayU(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            using var conn = _db.GetConnection();
            decimal total = 0, iva = 0;
            string cliente = "", correo = "";

            using (var cmd = new SqlCommand(@"
        SELECT f.total, f.iva,
               u.nombre_completo, u.correo
        FROM Facturas f
        JOIN Ordenes_Trabajo o ON o.id_orden = f.id_orden
        JOIN Usuarios u ON u.id_usuario = o.id_cliente
        WHERE f.id_factura = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    total = Convert.ToDecimal(r["total"]);
                    iva = Convert.ToDecimal(r["iva"]);
                    cliente = r["nombre_completo"].ToString()!;
                    correo = r["correo"].ToString()!;
                }
            }

            string apiKey = _config["PayU:ApiKey"]!;
            string merchantId = _config["PayU:MerchantId"]!;
            string accountId = _config["PayU:AccountId"]!;
            string reference = $"OB-{id}-{DateTime.Now:yyyyMMddHHmm}";
            string amount = total.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            string currency = "COP";

            // Firma MD5: apiKey~merchantId~reference~amount~currency
            string rawSignature = $"{apiKey}~{merchantId}~{reference}~{amount}~{currency}";
            string signature;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawSignature));
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

            return View("~/Views/Admin/PagarConPayU.cshtml", vm);
        }

        // GET: /Admin/PayURespuesta  (PayU redirige al usuario aquí)
        public IActionResult PayURespuesta()
        {
            string estado = Request.Query["transactionState"]!;
            string referencia = Request.Query["referenceCode"]!;
            string transaccionId = Request.Query["transactionId"]!;
            string monto = Request.Query["TX_VALUE"]!;
            string mensaje = Request.Query["message"]!;

            ViewBag.Estado = estado;
            ViewBag.Referencia = referencia;
            ViewBag.TransaccionId = transaccionId;
            ViewBag.Monto = monto;
            ViewBag.Mensaje = mensaje;
            ViewBag.Exitoso = estado == "4"; // 4 = Aprobado en PayU

            // Si fue aprobado, actualizar la factura en la DB
            if (estado == "4" && referencia != null)
            {
                // La referencia es "OB-{idFactura}-{fecha}"
                string[] partes = referencia.Split('-');
                if (partes.Length >= 2 && int.TryParse(partes[1], out int idFactura))
                {
                    using var conn = _db.GetConnection();
                    using var cmd = new SqlCommand(@"
                UPDATE Facturas
                SET estado_pago = 'Pagado', metodo_pago = 'PayU', fecha_pago = GETDATE()
                WHERE id_factura = @id AND estado_pago = 'Pendiente'", conn);
                    cmd.Parameters.AddWithValue("@id", idFactura);
                    cmd.ExecuteNonQuery();

                    using var cmd2 = new SqlCommand(@"
                INSERT INTO Pagos (id_factura, monto, metodo, referencia_transaccion, observaciones)
                VALUES (@fac, @monto, 'PayU', @ref, @obs)", conn);
                    cmd2.Parameters.AddWithValue("@fac", idFactura);
                    cmd2.Parameters.AddWithValue("@monto", decimal.TryParse(monto,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal m) ? m : 0);
                    cmd2.Parameters.AddWithValue("@ref", transaccionId ?? "");
                    cmd2.Parameters.AddWithValue("@obs", $"Pago PayU - {mensaje}");
                    cmd2.ExecuteNonQuery();
                }
            }

            return View("~/Views/Admin/PayURespuesta.cshtml");
        }

        // POST: /Admin/PayUConfirmacion  (webhook de PayU, sin respuesta visual)
        [HttpPost]
        public IActionResult PayUConfirmacion()
        {
            return Ok();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // HELPERS PRIVADOS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private async Task VerificarRecordatorioMantenimiento(int idVehiculo, int kmActuales)
        {
            const int intervaloKm = 5000;
            const int avisoAntesKm = 500;

            int kilometrajeRecordatorio;

            if (kmActuales % intervaloKm == 0)
            {
                kilometrajeRecordatorio = kmActuales;
            }
            else
            {
                kilometrajeRecordatorio = ((kmActuales / intervaloKm) + 1) * intervaloKm;
            }

            int kmDesdeAviso = kilometrajeRecordatorio - avisoAntesKm;

            if (kmActuales < kmDesdeAviso)
                return;

            using var conn = _db.GetConnection();

            int yaEnviado;
            using (var cmdExiste = new SqlCommand(@"
                SELECT COUNT(1)
                FROM RecordatoriosMantenimiento
                WHERE id_vehiculo = @idVehiculo
                  AND kilometraje_recordatorio = @kilometraje
                  AND enviado = 1", conn))
            {
                cmdExiste.Parameters.AddWithValue("@idVehiculo", idVehiculo);
                cmdExiste.Parameters.AddWithValue("@kilometraje", kilometrajeRecordatorio);
                yaEnviado = Convert.ToInt32(cmdExiste.ExecuteScalar());
            }

            if (yaEnviado > 0)
                return;

            string correo = "";
            string cliente = "";
            string placa = "";
            string marca = "";
            string modelo = "";

            using (var cmdDatos = new SqlCommand(@"
                SELECT 
                    c.correo,
                    c.nombre_completo,
                    v.placa,
                    v.marca,
                    v.modelo
                FROM Vehiculos v
                INNER JOIN Clientes c ON v.id_cliente = c.id_cliente
                WHERE v.id_vehiculo = @idVehiculo", conn))
            {
                cmdDatos.Parameters.AddWithValue("@idVehiculo", idVehiculo);

                using var reader = cmdDatos.ExecuteReader();

                if (!reader.Read())
                    return;

                correo = reader["correo"]?.ToString() ?? "";
                cliente = reader["nombre_completo"]?.ToString() ?? "";
                placa = reader["placa"]?.ToString() ?? "";
                marca = reader["marca"]?.ToString() ?? "";
                modelo = reader["modelo"]?.ToString() ?? "";
            }

            if (string.IsNullOrWhiteSpace(correo))
                return;

            string servicio = "Mantenimiento preventivo / revision por kilometraje";
            string asunto = $"Recordatorio de mantenimiento - {placa}";

            string cuerpo = $@"
                <div style='font-family:Arial,sans-serif;max-width:600px;margin:auto;
                            background:#1a1a2e;color:#ffffff;padding:30px;border-radius:10px;'>
                    <h1 style='color:#f0a500;text-align:center;'>Optimus Byte</h1>
                    <h2>Hola {cliente},</h2>
                    <p>Tu vehiculo <strong style='color:#f0a500;'>{marca} {modelo}</strong>
                       de placa <strong style='color:#f0a500;'>{placa}</strong>
                       esta proximo a llegar a los <strong>{kilometrajeRecordatorio:N0} km</strong>.</p>
                    <p>Actualmente registra <strong>{kmActuales:N0} km</strong>, por eso te recomendamos agendar la revision con anticipacion.</p>
                    <p><strong>Servicio sugerido:</strong> {servicio}</p>
                    <hr style='border-color:#f0a500;'>
                    <p style='color:#aaaaaa;font-size:12px;'>
                        Taller Optimus Byte - Sistema de gestion automotriz
                    </p>
                </div>";

            bool enviado = false;

            try
            {
                await _email.EnviarCorreoAsync(correo, cliente, asunto, cuerpo);
                enviado = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error recordatorio mantenimiento: {ex.Message}");
            }

            using var cmdGuardar = new SqlCommand(@"
                IF EXISTS (
                    SELECT 1
                    FROM RecordatoriosMantenimiento
                    WHERE id_vehiculo = @idVehiculo
                      AND kilometraje_recordatorio = @kilometraje
                )
                BEGIN
                    UPDATE RecordatoriosMantenimiento
                    SET enviado = @enviado,
                        fecha_envio = CASE WHEN @enviado = 1 THEN GETDATE() ELSE fecha_envio END
                    WHERE id_vehiculo = @idVehiculo
                      AND kilometraje_recordatorio = @kilometraje
                END
                ELSE
                BEGIN
                    INSERT INTO RecordatoriosMantenimiento
                        (id_vehiculo, kilometraje_recordatorio, servicio, enviado, fecha_envio)
                    VALUES
                        (@idVehiculo, @kilometraje, @servicio, @enviado,
                         CASE WHEN @enviado = 1 THEN GETDATE() ELSE NULL END)
                END", conn);

            cmdGuardar.Parameters.AddWithValue("@idVehiculo", idVehiculo);
            cmdGuardar.Parameters.AddWithValue("@kilometraje", kilometrajeRecordatorio);
            cmdGuardar.Parameters.AddWithValue("@servicio", servicio);
            cmdGuardar.Parameters.AddWithValue("@enviado", enviado);

            cmdGuardar.ExecuteNonQuery();
        }
        private T EjecutarScalar<T>(SqlConnection conn, string sql)
        {
            using var cmd = new SqlCommand(sql, conn);
            var result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value) return default!;
            return (T)Convert.ChangeType(result, typeof(T));
        }

        private List<(int Id, string Nombre)> ObtenerMecanicos(SqlConnection conn)
        {
            var lista = new List<(int, string)>();
            using var cmd = new SqlCommand(@"
                SELECT u.id_usuario, u.nombre_completo
                FROM Usuarios u
                INNER JOIN Roles r ON u.id_rol = r.id_rol
                WHERE r.nombre = 'Mecanico' AND u.activo = 1
                ORDER BY u.nombre_completo", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                lista.Add((Convert.ToInt32(r["id_usuario"]), r["nombre_completo"].ToString()!));
            return lista;
        }

        private List<SolicitudRepuestoViewModel> ObtenerSolicitudesPendientes(SqlConnection conn)
        {
            var lista = new List<SolicitudRepuestoViewModel>();
            try
            {
                using var cmd = new SqlCommand(@"
                    SELECT s.id_solicitud, s.id_orden, s.nombre_repuesto,
                           s.cantidad, s.motivo, s.fecha_solicitud, s.atendida,
                           u.nombre_completo AS mecanico_nombre
                    FROM SolicitudesRepuesto s
                    INNER JOIN Usuarios u ON s.id_mecanico = u.id_usuario
                    WHERE s.atendida = 0
                    ORDER BY s.fecha_solicitud DESC", conn);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    lista.Add(new SolicitudRepuestoViewModel
                    {
                        IdSolicitud = Convert.ToInt32(r["id_solicitud"]),
                        IdOrden = Convert.ToInt32(r["id_orden"]),
                        NombreRepuesto = r["nombre_repuesto"].ToString()!,
                        Cantidad = Convert.ToInt32(r["cantidad"]),
                        Motivo = r["motivo"]?.ToString() ?? "",
                        MecanicoNombre = r["mecanico_nombre"].ToString()!,
                        FechaSolicitud = Convert.ToDateTime(r["fecha_solicitud"]),
                        Atendida = Convert.ToBoolean(r["atendida"])
                    });
            }
            catch { /* tabla puede no existir aÃºn */ }
            return lista;
        }

        // CAMBIO 2/4: Helper para repuestos bajo stock (reutilizado en Dashboard y Facturas)
        private List<RepuestoViewModel> ObtenerRepuestosBajoStock(SqlConnection conn)
        {
            var lista = new List<RepuestoViewModel>();
            try
            {
                using var cmd = new SqlCommand(@"
                    SELECT id_repuesto, nombre, referencia, stock_actual, stock_minimo
                    FROM Repuestos
                    WHERE activo == 1 AND stock_actual < stock_minimo
                    ORDER BY stock_actual ASC", conn);
                using var r = cmd.ExecuteReader();  
                while (r.Read())
                    lista.Add(new RepuestoViewModel
                    {
                        IdRepuesto = Convert.ToInt32(r["id_repuesto"]),
                        Nombre = r["nombre"].ToString()!,
                        Referencia = r["referencia"].ToString()!,
                        StockActual = Convert.ToInt32(r["stock_actual"]),
                        StockMinimo = Convert.ToInt32(r["stock_minimo"])
                    });
            }
            catch { /* tabla puede no existir aún */ }
            return lista;
        }

        private void RegistrarEstado(SqlConnection conn, int idOrden, int idUsuario,
            string estado, string observacion)
        {
            using var cmd = new SqlCommand(@"
                INSERT INTO EstadosOrden (id_orden, id_usuario, estado_nuevo, observacion)
                VALUES (@ord, @usr, @est, @obs)", conn);
            cmd.Parameters.AddWithValue("@ord", idOrden);
            cmd.Parameters.AddWithValue("@usr", idUsuario);
            cmd.Parameters.AddWithValue("@est", estado);
            cmd.Parameters.AddWithValue("@obs", (object?)observacion ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        private void RegistrarMovimiento(SqlConnection conn, int idRepuesto, int idUsuario,
            int? idOrden, string tipo, int cantidad, int stockAnterior, string motivo)
        {
            int stockNuevo = tipo == "Entrada"
                ? stockAnterior + cantidad
                : stockAnterior - cantidad;

            using var cmd = new SqlCommand(@"
                INSERT INTO MovimientosInventario
                    (id_repuesto, id_usuario, id_orden, tipo_movimiento,
                     cantidad, stock_anterior, stock_nuevo, motivo)
                VALUES (@rep, @usr, @ord, @tipo, @cant, @ant, @nuevo, @mot)", conn);
            cmd.Parameters.AddWithValue("@rep", idRepuesto);
            cmd.Parameters.AddWithValue("@usr", idUsuario);
            cmd.Parameters.AddWithValue("@ord", (object?)idOrden ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tipo", tipo);
            cmd.Parameters.AddWithValue("@cant", cantidad);
            cmd.Parameters.AddWithValue("@ant", stockAnterior);
            cmd.Parameters.AddWithValue("@nuevo", stockNuevo);
            cmd.Parameters.AddWithValue("@mot", motivo);
            cmd.ExecuteNonQuery();
        }
    }
}


