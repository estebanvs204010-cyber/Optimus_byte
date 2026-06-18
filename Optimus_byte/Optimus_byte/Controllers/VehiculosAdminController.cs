using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.DATA;
using Optimus_byte.Models.ViewModels;

namespace Optimus_byte.Controllers
{
    public class VehiculosAdminController : Controller
    {
        private readonly DbHelper _db;
        public VehiculosAdminController(DbHelper db) => _db = db;

        private bool EsAdmin() =>
            HttpContext.Session.GetString("UsuarioRol") == "Admin";

        private int GetIdAdmin() =>
            int.Parse(HttpContext.Session.GetString("UsuarioId") ?? "0");

        // ── Lista todos los vehículos ─────────────────────────
        public IActionResult Index()
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var vehiculos = new List<dynamic>();

            using var conn = _db.GetConnection();

            using (var cmd = new SqlCommand(@"
                SELECT v.id_vehiculo, v.placa, v.marca, v.modelo, v.anio,
                       v.color, v.vin, v.km_actuales, v.activo, v.fecha_registro,
                       u.nombre_completo AS cliente_nombre
                FROM Vehiculos v
                INNER JOIN Clientes c ON v.id_cliente = c.id_cliente
                INNER JOIN Usuarios u ON c.id_usuario = u.id_usuario
                ORDER BY u.nombre_completo, v.placa", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    vehiculos.Add(new
                    {
                        IdVehiculo = Convert.ToInt32(reader["id_vehiculo"]),
                        Placa = reader["placa"].ToString()!,
                        Marca = reader["marca"].ToString()!,
                        Modelo = reader["modelo"].ToString()!,
                        Anio = Convert.ToInt32(reader["anio"]),
                        Color = reader["color"]?.ToString() ?? "—",
                        Vin = reader["vin"]?.ToString() ?? "—",
                        KmActuales = Convert.ToInt32(reader["km_actuales"]),
                        Activo = Convert.ToBoolean(reader["activo"]),
                        FechaRegistro = Convert.ToDateTime(reader["fecha_registro"]),
                        ClienteNombre = reader["cliente_nombre"].ToString()!
                    });
                }
            }

            ViewBag.Vehiculos = vehiculos;
            ViewBag.SolicitudesPendientes = ObtenerSolicitudesPendientes(conn);
            ViewBag.RepuestosBajoStockList = ObtenerRepuestosBajoStock(conn);

            return View("~/Views/Admin/Vehiculos.cshtml");
        }

        // ── Editar vehículo ───────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Editar(int IdVehiculo, string Placa, string Marca,
            string Modelo, int Anio, string? Color, string? Vin, int KmActuales)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            bool placaExiste = false;
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM Vehiculos WHERE placa = @placa AND id_vehiculo != @id", conn))
            {
                cmd.Parameters.AddWithValue("@placa", Placa.ToUpper().Trim());
                cmd.Parameters.AddWithValue("@id", IdVehiculo);
                placaExiste = (int)cmd.ExecuteScalar()! > 0;
            }

            if (placaExiste)
            {
                TempData["Error"] = $"Ya existe un vehículo con la placa {Placa.ToUpper()}.";
                return RedirectToAction("Index");
            }

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                UPDATE Vehiculos
                SET placa = @placa, marca = @marca, modelo = @modelo,
                    anio = @anio, color = @color, vin = @vin, km_actuales = @km
                WHERE id_vehiculo = @id", conn))
            {
                cmd.Parameters.AddWithValue("@placa", Placa.ToUpper().Trim());
                cmd.Parameters.AddWithValue("@marca", Marca.Trim());
                cmd.Parameters.AddWithValue("@modelo", Modelo.Trim());
                cmd.Parameters.AddWithValue("@anio", Anio);
                cmd.Parameters.AddWithValue("@color", (object?)Color ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@vin", (object?)Vin ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@km", KmActuales);
                cmd.Parameters.AddWithValue("@id", IdVehiculo);
                cmd.ExecuteNonQuery();
            }

            RegistrarAuditoria($"Editó vehículo placa {Placa.ToUpper()}");
            TempData["Exito"] = $"Vehículo {Placa.ToUpper()} actualizado correctamente.";
            return RedirectToAction("Index");
        }

        // ── Desactivar ────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Desactivar(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            string placa = EjecutarYObtenerPlaca(
                "UPDATE Vehiculos SET activo = 0 OUTPUT DELETED.placa WHERE id_vehiculo = @id", id);

            RegistrarAuditoria($"Desactivó vehículo placa {placa}");
            TempData["Exito"] = $"Vehículo {placa} desactivado.";
            return RedirectToAction("Index");
        }

        // ── Reactivar ─────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Reactivar(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            string placa = EjecutarYObtenerPlaca(
                "UPDATE Vehiculos SET activo = 1 OUTPUT INSERTED.placa WHERE id_vehiculo = @id", id);

            RegistrarAuditoria($"Reactivó vehículo placa {placa}");
            TempData["Exito"] = $"Vehículo {placa} reactivado.";
            return RedirectToAction("Index");
        }

        // ── Eliminar ──────────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Eliminar(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            string placa = EjecutarYObtenerPlaca(
                "DELETE FROM Vehiculos OUTPUT DELETED.placa WHERE id_vehiculo = @id", id);

            RegistrarAuditoria($"Eliminó vehículo placa {placa}");
            TempData["Exito"] = $"Vehículo {placa} eliminado.";
            return RedirectToAction("Index");
        }

        // ── Helpers ───────────────────────────────────────────
        private string EjecutarYObtenerPlaca(string sql, int id)
        {
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteScalar()?.ToString() ?? "";
        }

        private void RegistrarAuditoria(string accion)
        {
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                INSERT INTO LogAuditoria (id_usuario, accion, modulo)
                VALUES (@id, @accion, @modulo)", conn);
            cmd.Parameters.AddWithValue("@id", GetIdAdmin());
            cmd.Parameters.AddWithValue("@accion", accion);
            cmd.Parameters.AddWithValue("@modulo", "Gestión de Vehículos");
            cmd.ExecuteNonQuery();
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
            catch { /* tabla puede no existir aún */ }
            return lista;
        }

        private List<RepuestoViewModel> ObtenerRepuestosBajoStock(SqlConnection conn)
        {
            var lista = new List<RepuestoViewModel>();
            try
            {
                using var cmd = new SqlCommand(@"
                    SELECT id_repuesto, nombre, referencia, stock_actual, stock_minimo
                    FROM Repuestos
                    WHERE activo = 1 AND stock_actual < stock_minimo
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
    }
}
