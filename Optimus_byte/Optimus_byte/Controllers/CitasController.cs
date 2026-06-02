using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.DATA;
using Optimus_byte.Models.ViewModels;

namespace Optimus_byte.Controllers
{
    public class CitasController : Controller
    {
        private readonly DbHelper _db;

        public CitasController(DbHelper db) => _db = db;

        public IActionResult Index()
        {
            var rol = HttpContext.Session.GetString("UsuarioRol");
            var idUsuario = ObtenerIdUsuarioActual();

            if (string.IsNullOrWhiteSpace(rol) || idUsuario == 0)
            {
                return View(new CitasClientePageViewModel { RequiereLogin = true });
            }

            if (rol != "Cliente")
            {
                TempData["Error"] = "Solo los clientes pueden agendar citas desde esta vista.";
                return RedirectToAction("Index", "Home");
            }

            using var conn = _db.GetConnection();
            AsegurarTablaCitas(conn);

            var idCliente = ObtenerIdCliente(conn, idUsuario);
            var vm = new CitasClientePageViewModel
            {
                NombreCliente = HttpContext.Session.GetString("UsuarioNombre") ?? "Cliente",
                Vehiculos = ObtenerVehiculosCliente(conn, idCliente),
                MisCitas = ObtenerCitasCliente(conn, idCliente)
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Agendar(int idVehiculo, string servicio, DateTime fecha, string hora, string? observaciones)
        {
            if (HttpContext.Session.GetString("UsuarioRol") != "Cliente")
            {
                TempData["Error"] = "Debes iniciar sesión como cliente para agendar una cita.";
                return RedirectToAction("Index");
            }

            if (idVehiculo <= 0 || string.IsNullOrWhiteSpace(servicio) || string.IsNullOrWhiteSpace(hora))
            {
                TempData["Error"] = "Completa vehículo, servicio, fecha y hora para agendar.";
                return RedirectToAction("Index");
            }

            if (!TimeSpan.TryParse(hora, out var horaCita))
            {
                TempData["Error"] = "La hora seleccionada no es válida.";
                return RedirectToAction("Index");
            }

            var fechaHora = fecha.Date.Add(horaCita);
            if (fechaHora < DateTime.Now)
            {
                TempData["Error"] = "La cita debe ser para una fecha y hora futura.";
                return RedirectToAction("Index");
            }

            var idUsuario = ObtenerIdUsuarioActual();
            using var conn = _db.GetConnection();
            AsegurarTablaCitas(conn);

            var idCliente = ObtenerIdCliente(conn, idUsuario);
            if (idCliente == 0 || !VehiculoPerteneceAlCliente(conn, idVehiculo, idCliente))
            {
                TempData["Error"] = "Selecciona un vehículo registrado a tu nombre.";
                return RedirectToAction("Index");
            }

            using var cmd = new SqlCommand(@"
                INSERT INTO CitasCliente
                    (id_cliente, id_vehiculo, servicio, fecha_cita, observaciones, estado)
                VALUES
                    (@idCliente, @idVehiculo, @servicio, @fechaCita, @observaciones, 'Solicitada')", conn);

            cmd.Parameters.AddWithValue("@idCliente", idCliente);
            cmd.Parameters.AddWithValue("@idVehiculo", idVehiculo);
            cmd.Parameters.AddWithValue("@servicio", servicio.Trim());
            cmd.Parameters.AddWithValue("@fechaCita", fechaHora);
            cmd.Parameters.AddWithValue("@observaciones", (object?)observaciones?.Trim() ?? DBNull.Value);
            cmd.ExecuteNonQuery();

            TempData["Exito"] = "Tu cita fue solicitada correctamente. El taller podrá verla en Admin y Mecánico.";
            return RedirectToAction("Index");
        }

        public static void AsegurarTablaCitas(SqlConnection conn)
        {
            using var cmd = new SqlCommand(@"
                IF OBJECT_ID('dbo.CitasCliente', 'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.CitasCliente
                    (
                        id_cita INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        id_cliente INT NOT NULL,
                        id_vehiculo INT NOT NULL,
                        servicio NVARCHAR(100) NOT NULL,
                        fecha_cita DATETIME NOT NULL,
                        observaciones NVARCHAR(500) NULL,
                        estado NVARCHAR(30) NOT NULL DEFAULT 'Solicitada',
                        fecha_creacion DATETIME NOT NULL DEFAULT GETDATE()
                    );
                END", conn);
            cmd.ExecuteNonQuery();
        }

        public static List<CitaClienteViewModel> ObtenerCitasGenerales(SqlConnection conn, int top = 12)
        {
            AsegurarTablaCitas(conn);
            var citas = new List<CitaClienteViewModel>();

            using var cmd = new SqlCommand($@"
                SELECT TOP ({top})
                       ct.id_cita, ct.id_vehiculo, ct.servicio, ct.fecha_cita,
                       ct.observaciones, ct.estado, ct.fecha_creacion,
                       c.nombre_completo AS cliente,
                       v.placa, v.marca, v.modelo, v.anio
                FROM CitasCliente ct
                INNER JOIN Clientes c ON c.id_cliente = ct.id_cliente
                INNER JOIN Vehiculos v ON v.id_vehiculo = ct.id_vehiculo
                WHERE ct.estado <> 'Cancelada'
                ORDER BY ct.fecha_cita ASC", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                citas.Add(MapearCita(reader));
            }

            return citas;
        }

        public static int ContarCitasHoy(SqlConnection conn)
        {
            AsegurarTablaCitas(conn);
            using var cmd = new SqlCommand(@"
                SELECT COUNT(1)
                FROM CitasCliente
                WHERE CAST(fecha_cita AS DATE) = CAST(GETDATE() AS DATE)
                  AND estado <> 'Cancelada'", conn);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static int ContarProximasCitas(SqlConnection conn)
        {
            AsegurarTablaCitas(conn);
            using var cmd = new SqlCommand(@"
                SELECT COUNT(1)
                FROM CitasCliente
                WHERE fecha_cita >= GETDATE()
                  AND estado <> 'Cancelada'", conn);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private int ObtenerIdUsuarioActual()
        {
            return int.TryParse(HttpContext.Session.GetString("UsuarioId"), out var id) ? id : 0;
        }

        private static int ObtenerIdCliente(SqlConnection conn, int idUsuario)
        {
            using var cmd = new SqlCommand("SELECT id_cliente FROM Clientes WHERE id_usuario = @idUsuario", conn);
            cmd.Parameters.AddWithValue("@idUsuario", idUsuario);
            var result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
        }

        private static bool VehiculoPerteneceAlCliente(SqlConnection conn, int idVehiculo, int idCliente)
        {
            using var cmd = new SqlCommand(@"
                SELECT COUNT(1)
                FROM Vehiculos
                WHERE id_vehiculo = @idVehiculo
                  AND id_cliente = @idCliente
                  AND activo = 1", conn);
            cmd.Parameters.AddWithValue("@idVehiculo", idVehiculo);
            cmd.Parameters.AddWithValue("@idCliente", idCliente);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static List<VehiculoCitaViewModel> ObtenerVehiculosCliente(SqlConnection conn, int idCliente)
        {
            var vehiculos = new List<VehiculoCitaViewModel>();

            using var cmd = new SqlCommand(@"
                SELECT id_vehiculo, placa, marca, modelo, anio
                FROM Vehiculos
                WHERE id_cliente = @idCliente AND activo = 1
                ORDER BY fecha_registro DESC", conn);
            cmd.Parameters.AddWithValue("@idCliente", idCliente);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                vehiculos.Add(new VehiculoCitaViewModel
                {
                    IdVehiculo = Convert.ToInt32(reader["id_vehiculo"]),
                    Placa = reader["placa"].ToString() ?? "",
                    MarcaModelo = $"{reader["marca"]} {reader["modelo"]} {reader["anio"]}".Trim()
                });
            }

            return vehiculos;
        }

        private static List<CitaClienteViewModel> ObtenerCitasCliente(SqlConnection conn, int idCliente)
        {
            var citas = new List<CitaClienteViewModel>();

            using var cmd = new SqlCommand(@"
                SELECT ct.id_cita, ct.id_vehiculo, ct.servicio, ct.fecha_cita,
                       ct.observaciones, ct.estado, ct.fecha_creacion,
                       c.nombre_completo AS cliente,
                       v.placa, v.marca, v.modelo, v.anio
                FROM CitasCliente ct
                INNER JOIN Clientes c ON c.id_cliente = ct.id_cliente
                INNER JOIN Vehiculos v ON v.id_vehiculo = ct.id_vehiculo
                WHERE ct.id_cliente = @idCliente
                ORDER BY ct.fecha_cita DESC", conn);
            cmd.Parameters.AddWithValue("@idCliente", idCliente);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                citas.Add(MapearCita(reader));
            }

            return citas;
        }

        private static CitaClienteViewModel MapearCita(SqlDataReader reader)
        {
            return new CitaClienteViewModel
            {
                IdCita = Convert.ToInt32(reader["id_cita"]),
                IdVehiculo = Convert.ToInt32(reader["id_vehiculo"]),
                Cliente = reader["cliente"].ToString() ?? "",
                Placa = reader["placa"].ToString() ?? "",
                Vehiculo = $"{reader["marca"]} {reader["modelo"]} {reader["anio"]}".Trim(),
                Servicio = reader["servicio"].ToString() ?? "",
                Observaciones = reader["observaciones"]?.ToString() ?? "",
                Estado = reader["estado"].ToString() ?? "",
                FechaHora = Convert.ToDateTime(reader["fecha_cita"]),
                FechaCreacion = Convert.ToDateTime(reader["fecha_creacion"])
            };
        }
    }
}
