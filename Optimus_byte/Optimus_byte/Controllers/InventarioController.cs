using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Optimus_byte.DATA;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Optimus_byte.Controllers
{
    public class InventarioController : Controller
    {
        private readonly DbHelper _db;

        public InventarioController(DbHelper db)
        {
            _db = db;
        }

        public IActionResult Index() => View();
        public IActionResult Checkout()
        {
            return View();
        }
        public IActionResult Detalles(int id)
        {
            ViewBag.Id = id;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult IniciarPago(string itemsJson)
        {
            if (string.IsNullOrWhiteSpace(itemsJson))
                return RedirectToAction("Index");

            var items = JsonSerializer.Deserialize<List<ItemCarritoPago>>(itemsJson);
            if (items == null || !items.Any())
                return RedirectToAction("Index");

            decimal total = 0;

            using var conn = _db.GetConnection();

            foreach (var item in items)
            {
                using var cmd = new SqlCommand(@"
            SELECT precio_unitario
            FROM Repuestos
            WHERE id_repuesto = @id AND activo = 1", conn);

                cmd.Parameters.AddWithValue("@id", item.id);

                var precioDb = cmd.ExecuteScalar();

                if (precioDb != null && precioDb != DBNull.Value)
                    total += Convert.ToDecimal(precioDb);
            }

            if (total <= 0)
                return RedirectToAction("Index");

            string merchantId = HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()["PayU:MerchantId"]!;

            string accountId = HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()["PayU:AccountId"]!;

            string apiKey = HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()["PayU:ApiKey"]!;

            string currency = HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()["PayU:Currency"]!;

            string test = HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()["PayU:Test"]!;

            string checkoutUrl = HttpContext.RequestServices
                .GetRequiredService<IConfiguration>()["PayU:CheckoutUrl"]!;

            string referenceCode = $"OPT-{DateTime.Now:yyyyMMddHHmmss}";
            string amount = total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

            string firmaTexto = $"{apiKey}~{merchantId}~{referenceCode}~{amount}~{currency}";
            string signature = CrearFirmaMd5(firmaTexto);

            ViewBag.CheckoutUrl = checkoutUrl;
            ViewBag.MerchantId = merchantId;
            ViewBag.AccountId = accountId;
            ViewBag.Description = "Compra de repuestos Optimus Byte";
            ViewBag.ReferenceCode = referenceCode;
            ViewBag.Amount = amount;
            ViewBag.Currency = currency;
            ViewBag.Signature = signature;
            ViewBag.Test = test;
            ViewBag.BuyerEmail = "comprador@test.com";
            ViewBag.ResponseUrl = $"{Request.Scheme}://{Request.Host}/Inventario/RespuestaPago";

            return View("~/Views/Inventario/PayUForm.cshtml");
        }

        public IActionResult RespuestaPago()
        {
            return View("~/Views/Inventario/RespuestaPago.cshtml");
        }

        private static string CrearFirmaMd5(string texto)
        {
            using var md5 = MD5.Create();
            byte[] inputBytes = Encoding.UTF8.GetBytes(texto);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            var sb = new StringBuilder();
            foreach (var b in hashBytes)
                sb.Append(b.ToString("x2"));

            return sb.ToString();
        }

        public class ItemCarritoPago
        {
            public int id { get; set; }
        }

        [HttpGet]
        public IActionResult GetProductos()
        {
            var productos = new List<object>();

            using var conn = _db.GetConnection();
            using var cmd = new SqlCommand(@"
                SELECT
                    id_repuesto,
                    nombre,
                    referencia,
                    descripcion,
                    categoria,
                    precio_unitario,
                    stock_actual
                FROM Repuestos
                WHERE activo = 1
                ORDER BY nombre ASC", conn);

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                productos.Add(new
                {
                    id = Convert.ToInt32(reader["id_repuesto"]),
                    nombre = reader["nombre"]?.ToString() ?? "",
                    referencia = reader["referencia"]?.ToString() ?? "",
                    desc = reader["descripcion"]?.ToString() ?? "",
                    categoria = reader["categoria"]?.ToString() ?? "",
                    marca = "",
                    modelo = "",
                    precio = Convert.ToDecimal(reader["precio_unitario"]),
                    stock = Convert.ToInt32(reader["stock_actual"]),
                    imagenUrl = ""
                });
            }

            return Json(productos);
        }
    }
}           