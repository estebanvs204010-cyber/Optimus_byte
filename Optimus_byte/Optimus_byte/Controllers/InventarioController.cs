using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.DATA;
using Optimus_byte.Models;

namespace Optimus_byte.Controllers
{
    public class InventarioController : Controller
    {
        private readonly DbHelper _db;

        public InventarioController(DbHelper db)
        {
            _db = db;
        }

        // GET /Inventario/Index — catálogo público
        public IActionResult Index() => View();

        // GET /Inventario/GetProductos — JSON para catalogo.js
        public IActionResult GetProductos()
        {
            var lista = new List<object>();
            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT id_repuesto, nombre, referencia, descripcion,
                       categoria, precio_unitario, stock_actual,
                       imagen_url, marca, modelo
                       categoria, precio_unitario, stock_actual, stock_minimo,
                       activo, fecha_registro, marca, modelo
                FROM Repuestos
                WHERE activo = 1
                ORDER BY nombre ASC", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new
                {
                    id = Convert.ToInt32(r["id_repuesto"]),
                    nombre = r["nombre"].ToString(),
                    referencia = r["referencia"].ToString(),
                    desc = r["descripcion"]?.ToString() ?? "",
                    categoria = r["categoria"].ToString(),
                    precio = Convert.ToDecimal(r["precio_unitario"]),
                    stock = Convert.ToInt32(r["stock_actual"]),
                    imagenUrl = r["imagen_url"]?.ToString() ?? "",
                    marca = r["marca"]?.ToString() ?? "",
                    modelo = r["modelo"]?.ToString() ?? ""
                });
            }
            return Json(lista);
        }

        // GET /Inventario/Detalles/5
        public IActionResult Detalles(int id)
        {
            using var conn = _db.GetConnection();

            Repuesto? rep = null;
            using (var cmd = new SqlCommand(@"
                SELECT id_repuesto, nombre, referencia, descripcion,
                       categoria, precio_unitario, stock_actual, stock_minimo,
                       fecha_registro, imagen_url, marca, modelo
                FROM Repuestos
                WHERE id_repuesto = @id AND activo = 1", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using var r = cmd.ExecuteReader();
                if (r.Read())
                    rep = new Repuesto
                    {
                        IdRepuesto = Convert.ToInt32(r["id_repuesto"]),
                        Nombre = r["nombre"].ToString()!,
                        Referencia = r["referencia"].ToString()!,
                        Descripcion = r["descripcion"]?.ToString(),
                        Categoria = r["categoria"].ToString()!,
                        PrecioUnitario = Convert.ToDecimal(r["precio_unitario"]),
                        StockActual = Convert.ToInt32(r["stock_actual"]),
                        StockMinimo = Convert.ToInt32(r["stock_minimo"]),
                        FechaRegistro = Convert.ToDateTime(r["fecha_registro"]),
                        ImagenUrl = r["imagen_url"]?.ToString(),
                        Marca = r["marca"]?.ToString(),
                        Modelo = r["modelo"]?.ToString(),
                        Activo = true
                    };
            }

            if (rep == null) return NotFound();

            // Calificaciones
            var cals = new List<CalificacionViewModel>();
            double prom = 0;
            try
            {
                using var cmd2 = new SqlCommand(@"
                    SELECT c.estrellas, c.comentario, c.fecha,
                           u.nombre_completo
                    FROM CalificacionesRepuesto c
                    INNER JOIN Usuarios u ON c.id_usuario = u.id_usuario
                    WHERE c.id_repuesto = @id
                    ORDER BY c.fecha DESC", conn);
                cmd2.Parameters.AddWithValue("@id", id);
                using var r2 = cmd2.ExecuteReader();
                while (r2.Read())
                    cals.Add(new CalificacionViewModel
                    {
                        Estrellas = Convert.ToInt32(r2["estrellas"]),
                        Comentario = r2["comentario"]?.ToString() ?? "",
                        Fecha = Convert.ToDateTime(r2["fecha"]),
                        NombreUsuario = r2["nombre_completo"].ToString()!
                    });
                prom = cals.Count > 0 ? cals.Average(c => c.Estrellas) : 0;
            }
            catch { /* tabla opcional aún */ }

            ViewBag.Calificaciones = cals;
            ViewBag.Promedio = prom;
            ViewBag.TotalCals = cals.Count;
            return View(rep);
        }

        // POST /Inventario/Calificar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Calificar(int idRepuesto, int estrellas, string? comentario)
        {
            var idStr = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(idStr))
                return RedirectToAction("Index", "Login");
            int idUsuario = int.Parse(idStr);

            using var conn = _db.GetConnection();
            try
            {
                using var cmd = new SqlCommand(@"
                    IF EXISTS (SELECT 1 FROM CalificacionesRepuesto
                               WHERE id_repuesto = @rep AND id_usuario = @usr)
                        UPDATE CalificacionesRepuesto
                        SET estrellas = @est, comentario = @com, fecha = GETDATE()
                        WHERE id_repuesto = @rep AND id_usuario = @usr
                    ELSE
                        INSERT INTO CalificacionesRepuesto
                            (id_repuesto, id_usuario, estrellas, comentario)
                        VALUES (@rep, @usr, @est, @com)", conn);
                cmd.Parameters.AddWithValue("@rep", idRepuesto);
                cmd.Parameters.AddWithValue("@usr", idUsuario);
                cmd.Parameters.AddWithValue("@est", Math.Clamp(estrellas, 1, 5));
                cmd.Parameters.AddWithValue("@com", (object?)comentario ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            catch { }

            TempData["CalOk"] = "¡Gracias por tu calificación!";
            return RedirectToAction("Detalles", new { id = idRepuesto });
        }
    }
}