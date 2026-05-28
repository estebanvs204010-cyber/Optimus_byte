using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Optimus_byte.Models.ViewModels;

namespace Optimus_byte.Controllers
{
    public class PagosController : Controller
    {
        private static readonly string[] MetodosPago =
        {
            "Transferencia", "Tarjeta debito", "Tarjeta credito", "PSE", "Nequi"
        };

        private static readonly string[] EstadosPago =
        {
            "Pendiente", "Cancelado", "Pagado"
        };

        private readonly string _connectionString;

        public PagosController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión DefaultConnection.");
        }

        private bool EsAdmin() =>
            HttpContext.Session.GetString("UsuarioRol") == "Admin";

        // =========================
        // INDEX
        // =========================

        public IActionResult Index()
        {
            if (!EsAdmin())
                return RedirectToAction("Index", "Login");

            var pagos = ObtenerPagos();

            var model = new PagosIndexViewModel
            {
                Pagos = pagos,
                TotalPagos = pagos.Count,
                FacturasPendientes = ContarFacturasPorEstado("Pendiente"),
                FacturasPagadas = ContarFacturasPorEstado("Pagado"),
                TotalRecaudado = pagos.Sum(p => p.Monto)
            };

            return View("~/Views/Pagos/Index.cshtml", model);
        }

        // =========================
        // CREAR
        // =========================

        public IActionResult Crear()
        {
            if (!EsAdmin())
                return RedirectToAction("Index", "Login");

            CargarCombos();

            return View("~/Views/Pagos/Crear.cshtml", new PagoFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(PagoFormViewModel model)
        {
            if (!EsAdmin())
                return RedirectToAction("Index", "Login");

            ValidarPago(model);

            if (!ModelState.IsValid)
            {
                CargarCombos();
                return View("~/Views/Pagos/Crear.cshtml", model);
            }

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                using (var command = new SqlCommand(@"
                    INSERT INTO Pagos (FacturaId, FechaPago, Monto, MetodoPago)
                    VALUES (@FacturaId, @FechaPago, @Monto, @MetodoPago);",
                    connection, transaction))
                {
                    command.Parameters.AddWithValue("@FacturaId", model.IdFactura);
                    command.Parameters.AddWithValue("@FechaPago", model.FechaPago);
                    command.Parameters.AddWithValue("@Monto", model.Monto);
                    command.Parameters.AddWithValue("@MetodoPago", model.Metodo);

                    command.ExecuteNonQuery();
                }

                ActualizarEstadoFactura(
                    connection,
                    transaction,
                    model.IdFactura,
                    model.EstadoPago
                );

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            TempData["Exito"] = "Pago registrado correctamente.";

            return RedirectToAction("Index");
        }

        // =========================
        // EDITAR
        // =========================

        public IActionResult Editar(int id)
        {
            if (!EsAdmin())
                return RedirectToAction("Index", "Login");

            var model = ObtenerPagoPorId(id);

            if (model == null)
                return NotFound();

            CargarCombos();

            return View("~/Views/Pagos/Editar.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(int id, PagoFormViewModel model)
        {
            if (!EsAdmin())
                return RedirectToAction("Index", "Login");

            if (!ExistePago(id))
                return NotFound();

            ValidarPago(model);

            if (!ModelState.IsValid)
            {
                CargarCombos();
                return View("~/Views/Pagos/Editar.cshtml", model);
            }

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction();

            try
            {
                using (var command = new SqlCommand(@"
                    UPDATE Pagos
                    SET FacturaId = @FacturaId,
                        FechaPago = @FechaPago,
                        Monto = @Monto,
                        MetodoPago = @MetodoPago
                    WHERE Id = @Id;",
                    connection, transaction))
                {
                    command.Parameters.AddWithValue("@Id", id);
                    command.Parameters.AddWithValue("@FacturaId", model.IdFactura);
                    command.Parameters.AddWithValue("@FechaPago", model.FechaPago);
                    command.Parameters.AddWithValue("@Monto", model.Monto);
                    command.Parameters.AddWithValue("@MetodoPago", model.Metodo);

                    command.ExecuteNonQuery();
                }

                ActualizarEstadoFactura(
                    connection,
                    transaction,
                    model.IdFactura,
                    model.EstadoPago
                );

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            TempData["Exito"] = "Pago actualizado correctamente.";

            return RedirectToAction("Index");
        }

        // =========================
        // OBTENER PAGOS
        // =========================

        private List<PagoResumenViewModel> ObtenerPagos()
        {
            var pagos = new List<PagoResumenViewModel>();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(@"
                SELECT
                    p.Id,
                    p.FacturaId,
                    p.Monto,
                    p.MetodoPago,
                    p.FechaPago,
                    ISNULL(f.Total, 0) AS TotalFactura,
                    ISNULL(f.Estado, '') AS EstadoFactura
                FROM Pagos p
                LEFT JOIN Facturas f ON f.Id = p.FacturaId
                ORDER BY p.FechaPago DESC;",
                connection);

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                pagos.Add(new PagoResumenViewModel
                {
                    IdPago = reader.GetInt32(reader.GetOrdinal("Id")),
                    IdFactura = reader.GetInt32(reader.GetOrdinal("FacturaId")),
                    IdOrden = 0,
                    Monto = reader.GetDecimal(reader.GetOrdinal("Monto")),
                    Metodo = reader.GetString(reader.GetOrdinal("MetodoPago")),
                    FechaPago = reader.GetDateTime(reader.GetOrdinal("FechaPago")),
                    TotalFactura = reader.GetDecimal(reader.GetOrdinal("TotalFactura")),
                    EstadoPago = reader.GetString(reader.GetOrdinal("EstadoFactura")),
                    ReferenciaTransaccion = null,
                    Observaciones = null,
                    Administrador = "Admin"
                });
            }

            return pagos;
        }

        // =========================
        // CONTAR FACTURAS
        // =========================

        private int ContarFacturasPorEstado(string estado)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(
                "SELECT COUNT(1) FROM Facturas WHERE Estado = @Estado;",
                connection
            );

            command.Parameters.AddWithValue("@Estado", estado);

            return (int)command.ExecuteScalar()!;
        }

        // =========================
        // OBTENER PAGO POR ID
        // =========================

        private PagoFormViewModel? ObtenerPagoPorId(int id)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(@"
                SELECT
                    p.Id,
                    p.FacturaId,
                    p.FechaPago,
                    p.Monto,
                    p.MetodoPago,
                    f.Estado
                FROM Pagos p
                INNER JOIN Facturas f ON f.Id = p.FacturaId
                WHERE p.Id = @Id;",
                connection);

            command.Parameters.AddWithValue("@Id", id);

            using var reader = command.ExecuteReader();

            if (!reader.Read())
                return null;

            return new PagoFormViewModel
            {
                IdPago = reader.GetInt32(reader.GetOrdinal("Id")),
                IdFactura = reader.GetInt32(reader.GetOrdinal("FacturaId")),
                FechaPago = reader.GetDateTime(reader.GetOrdinal("FechaPago")),
                Monto = reader.GetDecimal(reader.GetOrdinal("Monto")),
                Metodo = reader.GetString(reader.GetOrdinal("MetodoPago")),
                EstadoPago = reader.GetString(reader.GetOrdinal("Estado"))
            };
        }

        // =========================
        // COMBOS
        // =========================

        private void CargarCombos()
        {
            var facturas = new List<SelectListItem>();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using (var command = new SqlCommand(@"
                SELECT Id, Estado, Total
                FROM Facturas
                ORDER BY Id DESC;",
                connection))
            {
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    int id = reader.GetInt32(reader.GetOrdinal("Id"));
                    string estado = reader.GetString(reader.GetOrdinal("Estado"));
                    decimal total = reader.GetDecimal(reader.GetOrdinal("Total"));

                    facturas.Add(new SelectListItem
                    {
                        Value = id.ToString(),
                        Text = $"Factura #{id} - {estado} - {total:C0}"
                    });
                }
            }

            ViewBag.Facturas = facturas;

            ViewBag.MetodosPago = MetodosPago
                .Select(m => new SelectListItem
                {
                    Value = m,
                    Text = m
                })
                .ToList();

            ViewBag.EstadosPago = EstadosPago
                .Select(e => new SelectListItem
                {
                    Value = e,
                    Text = e
                })
                .ToList();
        }

        // =========================
        // VALIDACIONES
        // =========================

        private void ValidarPago(PagoFormViewModel model)
        {
            if (!ExisteFactura(model.IdFactura))
            {
                ModelState.AddModelError(
                    nameof(model.IdFactura),
                    "La factura seleccionada no existe."
                );
            }

            if (!MetodosPago.Contains(model.Metodo))
            {
                ModelState.AddModelError(
                    nameof(model.Metodo),
                    "Selecciona un método de pago válido."
                );
            }

            if (!EstadosPago.Contains(model.EstadoPago))
            {
                ModelState.AddModelError(
                    nameof(model.EstadoPago),
                    "Selecciona un estado válido."
                );
            }
        }

        private bool ExisteFactura(int idFactura)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(
                "SELECT COUNT(1) FROM Facturas WHERE Id = @Id;",
                connection
            );

            command.Parameters.AddWithValue("@Id", idFactura);

            return (int)command.ExecuteScalar()! > 0;
        }

        private bool ExistePago(int idPago)
        {
            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var command = new SqlCommand(
                "SELECT COUNT(1) FROM Pagos WHERE Id = @Id;",
                connection
            );

            command.Parameters.AddWithValue("@Id", idPago);

            return (int)command.ExecuteScalar()! > 0;
        }

        // =========================
        // ACTUALIZAR FACTURA
        // =========================

        private static void ActualizarEstadoFactura(
            SqlConnection connection,
            SqlTransaction transaction,
            int idFactura,
            string estado)
        {
            using var command = new SqlCommand(@"
                UPDATE Facturas
                SET Estado = @Estado
                WHERE Id = @Id;",
                connection, transaction);

            command.Parameters.AddWithValue("@Id", idFactura);
            command.Parameters.AddWithValue("@Estado", estado);

            command.ExecuteNonQuery();
        }
    }
}