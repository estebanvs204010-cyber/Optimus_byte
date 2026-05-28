using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.DATA;

namespace Optimus_byte.Controllers
{
    public class OrdenesController : Controller
    {
        private readonly DbHelper _db;
        public OrdenesController(DbHelper db) => _db = db;

        private bool EsAdmin() =>
            HttpContext.Session.GetString("UsuarioRol") == "Admin";

        private bool EsMecanico() =>
            HttpContext.Session.GetString("UsuarioRol") == "Mecanico";

        private int GetIdUsuario() =>
            int.Parse(HttpContext.Session.GetString("UsuarioId") ?? "0");

        // ── Vista Admin ───────────────────────────────────────
        public IActionResult Index()
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");
            ViewBag.Ordenes = ObtenerOrdenes(soloMecanico: false);
            ViewBag.Mecanicos = ObtenerMecanicos();
            ViewBag.Vehiculos = ObtenerVehiculos();
            return View("~/Views/Ordenes/Index.cshtml");
        }

        // ── Vista Mecánico ────────────────────────────────────
        public IActionResult Mecanico()
        {
            if (!EsMecanico()) return RedirectToAction("Index", "Login");
            ViewBag.Ordenes = ObtenerOrdenes(soloMecanico: true, idMecanico: GetIdUsuario());
            ViewBag.Nombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Mecánico";
            return View("~/Views/Ordenes/Mecanico.cshtml");
        }

        // ── Crear orden ───────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Crear(int IdVehiculo, string TipoServicio,
            string DescripcionProblema, int KmIngreso, int? IdMecanico)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                INSERT INTO OrdenesTrabajo
                    (id_vehiculo, id_mecanico, id_administrador, estado,
                     tipo_servicio, descripcion_problema, km_ingreso, fecha_apertura)
                VALUES
                    (@idVehiculo, @idMecanico, @idAdmin, 'Pendiente',
                     @tipo, @descripcion, @km, GETDATE())", conn);

            cmd.Parameters.AddWithValue("@idVehiculo", IdVehiculo);
            cmd.Parameters.AddWithValue("@idMecanico", (object?)IdMecanico ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@idAdmin", GetIdUsuario());
            cmd.Parameters.AddWithValue("@tipo", TipoServicio);
            cmd.Parameters.AddWithValue("@descripcion", DescripcionProblema.Trim());
            cmd.Parameters.AddWithValue("@km", KmIngreso);
            cmd.ExecuteNonQuery();

            RegistrarAuditoria("Creó orden de trabajo");
            TempData["Exito"] = "Orden creada correctamente.";
            return RedirectToAction("Index");
        }

        // ── Asignar mecánico ──────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AsignarMecanico(int IdOrden, int IdMecanico)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(
                "UPDATE OrdenesTrabajo SET id_mecanico = @idMec WHERE id_orden = @id", conn);
            cmd.Parameters.AddWithValue("@idMec", IdMecanico);
            cmd.Parameters.AddWithValue("@id", IdOrden);
            cmd.ExecuteNonQuery();

            RegistrarAuditoria($"Asignó mecánico a orden #{IdOrden}");
            TempData["Exito"] = "Mecánico asignado.";
            return RedirectToAction("Index");
        }

        // ── Cambiar estado ────────────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult CambiarEstado(int IdOrden, string NuevoEstado, string? Observacion)
        {
            if (!EsAdmin() && !EsMecanico()) return RedirectToAction("Index", "Login");

            using (var conn = _db.GetConnection())
            {
                // Actualizar estado en la orden
                using var cmd = new SqlCommand(@"
                    UPDATE OrdenesTrabajo
                    SET estado = @estado,
                        fecha_cierre = CASE WHEN @estado IN ('Entregado','Cancelado')
                                            THEN GETDATE() ELSE fecha_cierre END
                    WHERE id_orden = @id", conn);
                cmd.Parameters.AddWithValue("@estado", NuevoEstado);
                cmd.Parameters.AddWithValue("@id", IdOrden);
                cmd.ExecuteNonQuery();

                // Registrar en historial
                using var cmd2 = new SqlCommand(@"
                    INSERT INTO EstadosOrden (id_orden, id_usuario, estado_nuevo, observacion)
                    VALUES (@idOrden, @idUsuario, @estado, @obs)", conn);
                cmd2.Parameters.AddWithValue("@idOrden", IdOrden);
                cmd2.Parameters.AddWithValue("@idUsuario", GetIdUsuario());
                cmd2.Parameters.AddWithValue("@estado", NuevoEstado);
                cmd2.Parameters.AddWithValue("@obs", (object?)Observacion ?? DBNull.Value);
                cmd2.ExecuteNonQuery();
            }

            RegistrarAuditoria($"Cambió estado de orden #{IdOrden} a {NuevoEstado}");
            TempData["Exito"] = $"Estado actualizado a: {NuevoEstado}";
            return EsAdmin()
                ? RedirectToAction("Index")
                : RedirectToAction("Mecanico");
        }

        // ── Guardar diagnóstico y observaciones ───────────────
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult GuardarDiagnostico(int IdOrden, string? Diagnostico, string? Observaciones)
        {
            if (!EsAdmin() && !EsMecanico()) return RedirectToAction("Index", "Login");

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                UPDATE OrdenesTrabajo
                SET diagnostico = @diag, observaciones = @obs
                WHERE id_orden = @id", conn);
            cmd.Parameters.AddWithValue("@diag", (object?)Diagnostico ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@obs", (object?)Observaciones ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", IdOrden);
            cmd.ExecuteNonQuery();

            RegistrarAuditoria($"Actualizó diagnóstico de orden #{IdOrden}");
            TempData["Exito"] = "Diagnóstico guardado.";
            return EsAdmin()
                ? RedirectToAction("Index")
                : RedirectToAction("Mecanico");
        }

        // ── Helpers ───────────────────────────────────────────
        private List<dynamic> ObtenerOrdenes(bool soloMecanico, int idMecanico = 0)
        {
            var lista = new List<dynamic>();
            var where = soloMecanico ? "WHERE o.id_mecanico = @idMec" : "";

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand($@"
                SELECT o.id_orden, o.estado, o.tipo_servicio, o.descripcion_problema,
                       o.diagnostico, o.observaciones, o.km_ingreso,
                       o.fecha_apertura, o.fecha_cierre,
                       v.placa, v.marca, v.modelo,
                       uc.nombre_completo AS cliente_nombre,
                       ISNULL(um.nombre_completo, '—') AS mecanico_nombre,
                       o.id_mecanico, o.id_vehiculo
                FROM OrdenesTrabajo o
                INNER JOIN Vehiculos v  ON o.id_vehiculo = v.id_vehiculo
                INNER JOIN Clientes c   ON v.id_cliente  = c.id_cliente
                INNER JOIN Usuarios uc  ON c.id_usuario  = uc.id_usuario
                LEFT  JOIN Usuarios um  ON o.id_mecanico = um.id_usuario
                {where}
                ORDER BY o.fecha_apertura DESC", conn);

            if (soloMecanico)
                cmd.Parameters.AddWithValue("@idMec", idMecanico);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new
                {
                    IdOrden = Convert.ToInt32(reader["id_orden"]),
                    Estado = reader["estado"].ToString()!,
                    TipoServicio = reader["tipo_servicio"].ToString()!,
                    DescripcionProblema = reader["descripcion_problema"].ToString()!,
                    Diagnostico = reader["diagnostico"]?.ToString() ?? "",
                    Observaciones = reader["observaciones"]?.ToString() ?? "",
                    KmIngreso = Convert.ToInt32(reader["km_ingreso"]),
                    FechaApertura = Convert.ToDateTime(reader["fecha_apertura"]),
                    FechaCierre = reader["fecha_cierre"] == DBNull.Value
                                            ? (DateTime?)null
                                            : Convert.ToDateTime(reader["fecha_cierre"]),
                    Placa = reader["placa"].ToString()!,
                    Marca = reader["marca"].ToString()!,
                    Modelo = reader["modelo"].ToString()!,
                    ClienteNombre = reader["cliente_nombre"].ToString()!,
                    MecanicoNombre = reader["mecanico_nombre"].ToString()!,
                    IdMecanico = reader["id_mecanico"] == DBNull.Value
                                            ? (int?)null
                                            : Convert.ToInt32(reader["id_mecanico"]),
                    IdVehiculo = Convert.ToInt32(reader["id_vehiculo"])
                });
            }
            return lista;
        }

        private List<dynamic> ObtenerMecanicos()
        {
            var lista = new List<dynamic>();
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT u.id_usuario, u.nombre_completo
                FROM Usuarios u
                INNER JOIN Roles r ON u.id_rol = r.id_rol
                WHERE r.nombre_rol = 'Mecanico' AND u.activo = 1
                ORDER BY u.nombre_completo", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new
                {
                    IdUsuario = Convert.ToInt32(reader["id_usuario"]),
                    NombreCompleto = reader["nombre_completo"].ToString()!
                });
            }
            return lista;
        }

        private List<dynamic> ObtenerVehiculos()
        {
            var lista = new List<dynamic>();
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT v.id_vehiculo, v.placa, v.marca, v.modelo, v.km_actuales,
                       u.nombre_completo AS cliente_nombre
                FROM Vehiculos v
                INNER JOIN Clientes c ON v.id_cliente = c.id_cliente
                INNER JOIN Usuarios u ON c.id_usuario = u.id_usuario
                WHERE v.activo = 1
                ORDER BY v.placa", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new
                {
                    IdVehiculo = Convert.ToInt32(reader["id_vehiculo"]),
                    Placa = reader["placa"].ToString()!,
                    Marca = reader["marca"].ToString()!,
                    Modelo = reader["modelo"].ToString()!,
                    KmActuales = Convert.ToInt32(reader["km_actuales"]),
                    ClienteNombre = reader["cliente_nombre"].ToString()!
                });
            }
            return lista;
        }

        private void RegistrarAuditoria(string accion)
        {
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                INSERT INTO LogAuditoria (id_usuario, accion, modulo)
                VALUES (@id, @accion, @modulo)", conn);
            cmd.Parameters.AddWithValue("@id", GetIdUsuario());
            cmd.Parameters.AddWithValue("@accion", accion);
            cmd.Parameters.AddWithValue("@modulo", "Órdenes de Trabajo");
            cmd.ExecuteNonQuery();
        }
    }
}
