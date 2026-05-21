using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.DATA;
using Optimus_byte.Models;

namespace Optimus_byte.Controllers
{
    public class ClienteController : Controller
    {
        private readonly DbHelper _db;
        public ClienteController(DbHelper db) => _db = db;

        private bool EsCliente() =>
            HttpContext.Session.GetString("UsuarioRol") == "Cliente";

        private int GetIdUsuario() =>
            int.Parse(HttpContext.Session.GetString("UsuarioId") ?? "0");

        // ?? Portal ????????????????????????????????????????????
        public IActionResult Portal()
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");
            ViewBag.Nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Cliente";
            return View("~/Views/Cliente/Portal.cshtml");
        }

        // ?? Mis Vehículos ?????????????????????????????????????
        public IActionResult MisVehiculos()
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var idUsuario = GetIdUsuario();
            var vehiculos = new List<dynamic>();
            int idCliente = 0;

            // Obtener id_cliente
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT id_cliente FROM Clientes WHERE id_usuario = @id", conn))
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

        // ?? Agregar Vehículo GET ???????????????????????????????
        public IActionResult AgregarVehiculo()
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");
            ViewBag.Nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Cliente";
            return View("~/Views/Vehiculo/AgregarVehiculo.cshtml");
        }

        // ?? Agregar Vehículo POST ??????????????????????????????
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AgregarVehiculo(string Placa, string Marca, string Modelo,
            int Anio, string? Color, string? Vin, int KmActuales)
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var idUsuario = GetIdUsuario();
            int idCliente = 0;

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT id_cliente FROM Clientes WHERE id_usuario = @id", conn))
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

            // Verificar placa duplicada
            bool placaExiste = false;
            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM Vehiculos WHERE placa = @placa", conn))
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

        // ?? Editar Vehículo GET ???????????????????????????????
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

        // ?? Editar Vehículo POST ??????????????????????????????
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult EditarVehiculo(int IdVehiculo, string Placa, string Marca,
            string Modelo, int Anio, string? Color, string? Vin, int KmActuales)
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var idUsuario = GetIdUsuario();

            // Verificar placa duplicada excluyendo este vehículo
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

        // ?? Desactivar Vehículo ???????????????????????????????
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

        // ?? Eliminar Vehículo ?????????????????????????????????
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

        // ?? Mis Órdenes ???????????????????????????????????????
        public IActionResult MisOrdenes()
        {
            if (!EsCliente()) return RedirectToAction("Index", "Login");

            var idUsuario = GetIdUsuario();
            var ordenes = new List<dynamic>();

            using (var conn = _db.GetConnection())
            using (var cmd = new SqlCommand(@"
                SELECT o.id_orden, v.placa, o.tipo_servicio, o.descripcion_problema,
                       o.diagnostico, o.observaciones, o.estado,
                       o.fecha_apertura, o.fecha_cierre
                FROM OrdenesTrabajo o
                INNER JOIN Vehiculos v ON o.id_vehiculo = v.id_vehiculo
                INNER JOIN Clientes c  ON v.id_cliente  = c.id_cliente
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
                        TipoServicio = reader["tipo_servicio"].ToString()!,
                        DescripcionProblema = reader["descripcion_problema"].ToString()!,
                        Diagnostico = reader["diagnostico"]?.ToString() ?? "",
                        Observaciones = reader["observaciones"]?.ToString() ?? "",
                        Estado = reader["estado"].ToString()!,
                        FechaApertura = Convert.ToDateTime(reader["fecha_apertura"]),
                        FechaCierre = reader["fecha_cierre"] == DBNull.Value
                                              ? (DateTime?)null
                                              : Convert.ToDateTime(reader["fecha_cierre"])
                    });
                }
            }

            ViewBag.Nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Cliente";
            ViewBag.Ordenes = ordenes;
            return View("~/Views/Cliente/MisOrdenes.cshtml");
        }

        // ?? Helper auditoría ??????????????????????????????????
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
    }
}
