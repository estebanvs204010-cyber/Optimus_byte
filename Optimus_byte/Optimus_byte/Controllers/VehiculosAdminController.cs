using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.DATA;
using Optimus_byte.Services;

namespace Optimus_byte.Controllers
{
    public class VehiculosAdminController : Controller
    {
      private readonly DbHelper _db;
private readonly CorreoService _correoService;

public VehiculosAdminController(DbHelper db, CorreoService correoService)
{
    _db = db;
    _correoService = correoService;
}
        private bool EsAdmin() =>
            HttpContext.Session.GetString("UsuarioRol") == "Admin";

        private int GetIdAdmin() =>
            int.Parse(HttpContext.Session.GetString("UsuarioId") ?? "0");

        // ── Lista todos los vehículos ─────────────────────────
        public IActionResult Index()
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var vehiculos = new List<dynamic>();

            using (var conn = _db.GetConnection())
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
            return View("~/Views/Admin/Vehiculos.cshtml");
        }

        // ── Editar vehículo ───────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(int IdVehiculo, string Placa, string Marca,
            string Modelo, int Anio, string? Color, string? Vin, int KmActuales)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            // Verificar placa duplicada excluyendo este vehículo
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

            await VerificarRecordatorioMantenimiento(IdVehiculo, KmActuales);

            RegistrarAuditoria($"Editó vehículo placa {Placa.ToUpper()}");
            TempData["Exito"] = $"Vehículo {Placa.ToUpper()} actualizado correctamente. Km recibido: {KmActuales}";
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

        private async Task VerificarRecordatorioMantenimiento(int idVehiculo, int kmActuales)
        {
            const int intervaloKm = 5000;
            const int avisoAntesKm = 500;


            int siguienteMantenimiento = ((kmActuales / intervaloKm) + 1) * intervaloKm;
            int kmDesdeAviso = siguienteMantenimiento - avisoAntesKm;

            if (kmActuales < kmDesdeAviso)
                return;

            int kilometrajeRecordatorio = siguienteMantenimiento;
            using var conn = _db.GetConnection();

            int yaExiste;
            using (var cmdExiste = new SqlCommand(@"
                SELECT COUNT(1)
                FROM RecordatoriosMantenimiento
                WHERE id_vehiculo = @idVehiculo
                AND kilometraje_recordatorio = @kilometraje", conn))
            {
                cmdExiste.Parameters.AddWithValue("@idVehiculo", idVehiculo);
                cmdExiste.Parameters.AddWithValue("@kilometraje", kilometrajeRecordatorio);

                yaExiste = Convert.ToInt32(cmdExiste.ExecuteScalar());
            }

            if (yaExiste > 0)
                return;

            string? correo = null;
            string? cliente = null;
            string? placa = null;
            string? marca = null;
            string? modelo = null;

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

                correo = reader["correo"]?.ToString();
                cliente = reader["nombre_completo"]?.ToString();
                placa = reader["placa"]?.ToString();
                marca = reader["marca"]?.ToString();
                modelo = reader["modelo"]?.ToString();
            }

            if (string.IsNullOrWhiteSpace(correo))
                return;

            string servicio = "Mantenimiento preventivo / revisión por kilometraje";
            string asunto = $"Recordatorio de mantenimiento - {placa}";

            string cuerpo = $@"
                <h2>Recordatorio de mantenimiento</h2>
                <p>Hola <strong>{cliente}</strong>,</p>
                <p>Tu vehículo <strong>{marca} {modelo}</strong> de placa <strong>{placa}</strong> llegó a los <strong>{kilometrajeRecordatorio:N0} km</strong>.</p>
                <p>Te recomendamos realizar una revisión o cambio preventivo para mantener tu vehículo en buen estado.</p>
                <p><strong>Servicio sugerido:</strong> {servicio}</p>
                <br>
                <p>Atentamente,</p>
                <p><strong>Optimus Byte</strong></p>";

            try
            {
                await _correoService.EnviarCorreoAsync(correo, asunto, cuerpo);

                using var cmdInsert = new SqlCommand(@"
                INSERT INTO RecordatoriosMantenimiento
                (id_vehiculo, kilometraje_recordatorio, servicio, enviado, fecha_envio)
                VALUES
                (@idVehiculo, @kilometraje, @servicio, 1, GETDATE())", conn);

                cmdInsert.Parameters.AddWithValue("@idVehiculo", idVehiculo);
                cmdInsert.Parameters.AddWithValue("@kilometraje", kilometrajeRecordatorio);
                cmdInsert.Parameters.AddWithValue("@servicio", servicio);

                cmdInsert.ExecuteNonQuery();
            }
            catch
            {
                using var cmdInsertError = new SqlCommand(@"
                INSERT INTO RecordatoriosMantenimiento
                (id_vehiculo, kilometraje_recordatorio, servicio, enviado, fecha_envio)
                VALUES
                (@idVehiculo, @kilometraje, @servicio, 0, NULL)", conn);

                cmdInsertError.Parameters.AddWithValue("@idVehiculo", idVehiculo);
                cmdInsertError.Parameters.AddWithValue("@kilometraje", kilometrajeRecordatorio);
                cmdInsertError.Parameters.AddWithValue("@servicio", servicio);

                cmdInsertError.ExecuteNonQuery();
            }
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
    }
}
