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
        public AdminController(DbHelper db) => _db = db;

        private bool EsAdmin() =>
            HttpContext.Session.GetString("UsuarioRol") == "Admin";

        // ════════════════════════════════════════════════
        // DASHBOARD
        // ════════════════════════════════════════════════
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
            vm.IngresosMes = EjecutarScalar<decimal>(conn,
                @"SELECT ISNULL(SUM(total),0) FROM Facturas
                  WHERE estado_pago = 'Pagado'
                  AND MONTH(fecha_emision) = MONTH(GETDATE())
                  AND YEAR(fecha_emision) = YEAR(GETDATE())");

            using (var cmd = new SqlCommand(@"
                SELECT TOP 8
                    o.id_orden, o.estado, o.tipo_servicio, o.fecha_apertura,
                    v.placa, v.marca, v.modelo,
                    c.nombre_completo AS cliente,
                    ISNULL(u.nombre_completo,'Sin asignar') AS mecanico
                FROM OrdenesTrabajo o
                INNER JOIN Vehiculos v ON o.id_vehiculo = v.id_vehiculo
                INNER JOIN Clientes c ON v.id_cliente = c.id_cliente
                LEFT  JOIN Usuarios u ON o.id_mecanico = u.id_usuario
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

            return View("~/Views/Admin/Dashboard.cshtml", vm);
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

            // Dropdown de vehículos para el modal de crear orden
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

        // GET: Detalle orden
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
                {
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
            return View("~/Views/Admin/OrdenDetalle.cshtml", vm);
        }

        // POST: Crear orden
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CrearOrden(int idVehiculo, int? idMecanico,
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

            RegistrarEstado(conn, idOrden, idAdmin, "Pendiente", "Orden creada");

            TempData["Exito"] = $"Orden #{idOrden} creada correctamente.";
            return RedirectToAction("OrdenDetalle", new { id = idOrden });
        }

        // POST: Cambiar estado de orden
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarEstado(int idOrden, string nuevoEstado,
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

            TempData["Exito"] = $"Estado actualizado a: {nuevoEstado}";
            return RedirectToAction("OrdenDetalle", new { id = idOrden });
        }

        // POST: Actualizar diagnóstico
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

            TempData["Exito"] = "Diagnóstico guardado.";
            return RedirectToAction("OrdenDetalle", new { id = idOrden });
        }

        // ════════════════════════════════════════════════
        // INVENTARIO — Repuestos
        // ════════════════════════════════════════════════
        public IActionResult Inventario(string? buscar, string? categoria)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var lista = new List<RepuestoViewModel>();

            string where = "WHERE activo = 1";
            if (!string.IsNullOrWhiteSpace(buscar))
                where += $" AND (nombre LIKE '%{buscar.Replace("'", "''")}%' OR referencia LIKE '%{buscar.Replace("'", "''")}%')";
            if (!string.IsNullOrWhiteSpace(categoria))
                where += $" AND categoria = '{categoria.Replace("'", "''")}'";

            using var conn = _db.GetConnection();

            using (var cmd = new SqlCommand($@"
                SELECT id_repuesto, nombre, referencia, descripcion,
                       categoria, precio_unitario, stock_actual, stock_minimo, fecha_registro
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
                        FechaRegistro = Convert.ToDateTime(r["fecha_registro"])
                    });
            }

            var cats = new List<string>();
            using (var cmd2 = new SqlCommand(
                "SELECT DISTINCT categoria FROM Repuestos WHERE activo = 1 ORDER BY categoria", conn))
            using (var r2 = cmd2.ExecuteReader())
            {
                while (r2.Read()) cats.Add(r2[0].ToString()!);
            }

            ViewBag.Categorias = cats;
            ViewBag.BuscarFiltro = buscar ?? "";
            ViewBag.CatFiltro = categoria ?? "";
            return View("~/Views/Admin/Inventario.cshtml", lista);
        }

        // POST: Crear/Editar repuesto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GuardarRepuesto(int? idRepuesto, string nombre, string referencia,
            string? descripcion, string categoria, decimal precioUnitario,
            int stockActual, int stockMinimo)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            int idAdmin = int.Parse(HttpContext.Session.GetString("UsuarioId")!);

            using var conn = _db.GetConnection();

            if (idRepuesto == null || idRepuesto == 0)
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO Repuestos
                        (nombre, referencia, descripcion, categoria,
                         precio_unitario, stock_actual, stock_minimo)
                    OUTPUT INSERTED.id_repuesto
                    VALUES (@nom, @ref, @desc, @cat, @precio, @stock, @min)", conn);
                cmd.Parameters.AddWithValue("@nom", nombre);
                cmd.Parameters.AddWithValue("@ref", referencia);
                cmd.Parameters.AddWithValue("@desc", (object?)descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cat", categoria);
                cmd.Parameters.AddWithValue("@precio", precioUnitario);
                cmd.Parameters.AddWithValue("@stock", stockActual);
                cmd.Parameters.AddWithValue("@min", stockMinimo);
                int newId = (int)cmd.ExecuteScalar();

                RegistrarMovimiento(conn, newId, idAdmin, null, "Entrada",
                    stockActual, 0, "Stock inicial");

                TempData["Exito"] = $"Repuesto '{nombre}' creado correctamente.";
            }
            else
            {
                int stockAnterior = EjecutarScalar<int>(conn,
                    $"SELECT stock_actual FROM Repuestos WHERE id_repuesto = {idRepuesto}");

                using var cmd = new SqlCommand(@"
                    UPDATE Repuestos
                    SET nombre = @nom, referencia = @ref, descripcion = @desc,
                        categoria = @cat, precio_unitario = @precio,
                        stock_actual = @stock, stock_minimo = @min
                    WHERE id_repuesto = @id", conn);
                cmd.Parameters.AddWithValue("@nom", nombre);
                cmd.Parameters.AddWithValue("@ref", referencia);
                cmd.Parameters.AddWithValue("@desc", (object?)descripcion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cat", categoria);
                cmd.Parameters.AddWithValue("@precio", precioUnitario);
                cmd.Parameters.AddWithValue("@stock", stockActual);
                cmd.Parameters.AddWithValue("@min", stockMinimo);
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

        // POST: Desactivar repuesto
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

        // ════════════════════════════════════════════════
        // FACTURAS
        // ════════════════════════════════════════════════
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
                INNER JOIN OrdenesTrabajo o ON f.id_orden     = o.id_orden
                INNER JOIN Vehiculos      v ON o.id_vehiculo  = v.id_vehiculo
                INNER JOIN Clientes       c ON v.id_cliente   = c.id_cliente
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
            return View("~/Views/Admin/Facturas.cshtml", lista);
        }

        // POST: Crear factura desde orden
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CrearFactura(int idOrden, decimal subtotal, decimal iva)
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

            TempData["Exito"] = $"Factura creada por ${total:N0}.";
            return RedirectToAction("Facturas");
        }

        // POST: Registrar pago
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

        // POST: Anular factura
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

        // ════════════════════════════════════════════════
        // HELPERS PRIVADOS
        // ════════════════════════════════════════════════
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
