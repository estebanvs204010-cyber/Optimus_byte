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

        // GET: /Inventario — catálogo público de repuestos
        public IActionResult Index(string? categoria, string? buscar)
        {
            var lista = new List<Repuesto>();

            string where = "WHERE activo = 1";
            if (!string.IsNullOrWhiteSpace(categoria))
                where += $" AND categoria = '{categoria.Replace("'", "''")}'";
            if (!string.IsNullOrWhiteSpace(buscar))
                where += $" AND (nombre LIKE '%{buscar.Replace("'", "''")}%' OR referencia LIKE '%{buscar.Replace("'", "''")}%')";

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand($@"
                SELECT id_repuesto, nombre, referencia, descripcion,
                       categoria, precio_unitario, stock_actual, stock_minimo,
                       activo, fecha_registro, marca, modelo
                FROM Repuestos
                {where}
                ORDER BY nombre ASC", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new Repuesto
                {
                    IdRepuesto = Convert.ToInt32(reader["id_repuesto"]),
                    Nombre = reader["nombre"].ToString()!,
                    Referencia = reader["referencia"].ToString()!,
                    Descripcion = reader["descripcion"]?.ToString(),
                    Categoria = reader["categoria"].ToString()!,
                    PrecioUnitario = Convert.ToDecimal(reader["precio_unitario"]),
                    StockActual = Convert.ToInt32(reader["stock_actual"]),
                    StockMinimo = Convert.ToInt32(reader["stock_minimo"]),
                    Activo = Convert.ToBoolean(reader["activo"]),
                    FechaRegistro = Convert.ToDateTime(reader["fecha_registro"]),
                    marca = reader["marca"].ToString()!,
                    modelo = reader["modelo"].ToString()!

                });
            }

            var categorias = new List<string>();
            using var conn2 = _db.GetConnection();
            using var cmd2 = new SqlCommand(
                "SELECT DISTINCT categoria FROM Repuestos WHERE activo = 1 ORDER BY categoria",
                conn2);
            using var r2 = cmd2.ExecuteReader();
            while (r2.Read())
                categorias.Add(r2[0].ToString()!);

            ViewBag.Categorias = categorias;
            ViewBag.CatActual = categoria ?? "todos";
            ViewBag.BuscarActual = buscar ?? "";

            return View(lista);
        }

        // GET: /Inventario/GetProductosJson — endpoint para el JS del catálogo
        public IActionResult GetProductosJson(string? categoria, string? buscar)
        {
            var lista = new List<object>();

            string where = "WHERE activo = 1";
            if (!string.IsNullOrWhiteSpace(categoria) && categoria != "todos")
                where += $" AND categoria = '{categoria.Replace("'", "''")}'";
            if (!string.IsNullOrWhiteSpace(buscar))
                where += $" AND (nombre LIKE '%{buscar.Replace("'", "''")}%')";

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand($@"
                SELECT id_repuesto, nombre, referencia, descripcion,
                       categoria, precio_unitario, stock_actual, stock_minimo
                FROM Repuestos
                {where}
                ORDER BY nombre ASC", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string cat = reader["categoria"].ToString()!.ToLower();
                string bg = cat switch
                {
                    "motor" => "bg-motor",
                    "frenos" => "bg-frenos",
                    "filtros" => "bg-filtros",
                    "eléctrico" => "bg-electrico",
                    "electrico" => "bg-electrico",
                    "suspensión" => "bg-suspension",
                    "suspension" => "bg-suspension",
                    "lubricantes" => "bg-lubricante",
                    _ => "bg-motor"
                };

                string icon = cat switch
                {
                    "motor" => "⚙️",
                    "frenos" => "🔴",
                    "filtros" => "🔩",
                    "eléctrico" => "🔋",
                    "electrico" => "🔋",
                    "suspensión" => "🔧",
                    "suspension" => "🔧",
                    "lubricantes" => "🛢️",
                    _ => "🔩"
                };

                lista.Add(new
                {
                    id = Convert.ToInt32(reader["id_repuesto"]),
                    nombre = reader["nombre"].ToString(),
                    marca = reader["referencia"].ToString(),
                    categoria = reader["categoria"].ToString(),
                    desc = reader["descripcion"]?.ToString() ?? "",
                    precio = Convert.ToDecimal(reader["precio_unitario"]),
                    stock = Convert.ToInt32(reader["stock_actual"]),


                    bg,
                    icon
                });
            }

            return Json(lista);
        }

        // GET: /Inventario/Detalles/5
        public IActionResult Detalles(int id)
        {
            Repuesto? repuesto = null;

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT id_repuesto, nombre, referencia, descripcion,
                       categoria, precio_unitario, stock_actual, stock_minimo,
                       activo, fecha_registro, marca, modelo
                FROM Repuestos
                WHERE id_repuesto = @id AND activo = 1", conn);
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                repuesto = new Repuesto
                {
                    IdRepuesto = Convert.ToInt32(reader["id_repuesto"]),
                    Nombre = reader["nombre"].ToString()!,
                    Referencia = reader["referencia"].ToString()!,
                    Descripcion = reader["descripcion"]?.ToString(),
                    Categoria = reader["categoria"].ToString()!,
                    PrecioUnitario = Convert.ToDecimal(reader["precio_unitario"]),
                    StockActual = Convert.ToInt32(reader["stock_actual"]),
                    StockMinimo = Convert.ToInt32(reader["stock_minimo"]),
                    Activo = Convert.ToBoolean(reader["activo"]),
                    FechaRegistro = Convert.ToDateTime(reader["fecha_registro"]),
                    marca = reader["marca"].ToString()!,
                    modelo = reader["modelo"].ToString()!
                };
            }

            if (repuesto == null) return NotFound();
            return View(repuesto);
        }
    }
}