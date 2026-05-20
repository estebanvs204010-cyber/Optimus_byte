using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VistaPrincipal.Data;
using VistaPrincipal.Models;
using VistaPrincipal.Models.ViewModels;

namespace VistaPrincipal.Controllers
{
    public class PagosController : Controller
    {
        private static readonly string[] MetodosPago =
        {
           "Transferencia", "Tarjeta debito", "Tarjeta credito", "PSE", "Nequi"
        };

        private readonly optimusDBContext _db;

        public PagosController(optimusDBContext db) => _db = db;

        private bool EsAdmin() =>
            HttpContext.Session.GetString("UsuarioRol") == "Admin";

        public IActionResult Index()
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var pagos = _db.Pagos
                .Include(p => p.Factura)
                .OrderByDescending(p => p.FechaPago)
                .Select(p => new PagoResumenViewModel
                {
                    IdPago = p.IdPago,
                    IdFactura = p.IdFactura,
                    IdOrden = 0,
                    Monto = p.Monto,
                    TotalFactura = p.Factura != null ? p.Factura.Total : 0,
                    Metodo = p.Metodo,
                    EstadoPago = p.Factura != null ? p.Factura.EstadoPago : "",
                    ReferenciaTransaccion = null,
                    Observaciones = null,
                    FechaPago = p.FechaPago,
                    Administrador = "Admin"
                })
                .ToList();

            var model = new PagosIndexViewModel
            {
                Pagos = pagos,
                TotalPagos = pagos.Count,
                FacturasPendientes = _db.Facturas.Count(f => f.EstadoPago == "Pendiente"),
                FacturasPagadas = _db.Facturas.Count(f => f.EstadoPago == "Pagado"),
                TotalRecaudado = pagos.Sum(p => p.Monto)

            };

            return View("~/Views/Pagos/Index.cshtml", model);
        }

        public IActionResult Crear()
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            CargarCombos();
            return View("~/Views/Pagos/Crear.cshtml", new PagoFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Crear(PagoFormViewModel model)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var factura = _db.Facturas.Find(model.IdFactura);
            if (factura == null)
            {
                ModelState.AddModelError(nameof(model.IdFactura), "La factura seleccionada no existe.");
            }

            if (!MetodosPago.Contains(model.Metodo))
            {
                ModelState.AddModelError(nameof(model.Metodo), "Selecciona un método de pago válido.");
            }

            if (!ModelState.IsValid)
            {
                CargarCombos();
                return View("~/Views/Pagos/Crear.cshtml", model);
            }

            var pago = new Pago
            {
                IdFactura = model.IdFactura,
                Monto = model.Monto,
                Metodo = model.Metodo,
                ReferenciaTransaccion = model.ReferenciaTransaccion,
                Observaciones = model.Observaciones,
                FechaPago = model.FechaPago
            };

            _db.Pagos.Add(pago);
            ActualizarFacturaDespuesDePago(factura!, model.Metodo, model.FechaPago);
            _db.SaveChanges();

            RegistrarAuditoria($"Registró pago de factura #{model.IdFactura} por {model.Monto:C0}");
            TempData["Exito"] = "Pago registrado correctamente.";

            return RedirectToAction("Index");
        }

        public IActionResult Editar(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var pago = _db.Pagos.Find(id);
            if (pago == null) return NotFound();

            var model = new PagoFormViewModel
            {
                IdPago = pago.IdPago,
                IdFactura = pago.IdFactura,
                Monto = pago.Monto,
                Metodo = pago.Metodo,
                ReferenciaTransaccion = pago.ReferenciaTransaccion,
                Observaciones = pago.Observaciones,
                FechaPago = pago.FechaPago
            };

            CargarCombos();
            return View("~/Views/Pagos/Editar.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Editar(int id, PagoFormViewModel model)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var pago = _db.Pagos.Find(id);
            if (pago == null) return NotFound();

            var factura = _db.Facturas.Find(model.IdFactura);
            if (factura == null)
            {
                ModelState.AddModelError(nameof(model.IdFactura), "La factura seleccionada no existe.");
            }

            if (!MetodosPago.Contains(model.Metodo))
            {
                ModelState.AddModelError(nameof(model.Metodo), "Selecciona un método de pago válido.");
            }

            if (!ModelState.IsValid)
            {
                CargarCombos();
                return View("~/Views/Pagos/Editar.cshtml", model);
            }

            pago.IdFactura = model.IdFactura;
            pago.Monto = model.Monto;
            pago.Metodo = model.Metodo;
            pago.ReferenciaTransaccion = model.ReferenciaTransaccion;
            pago.Observaciones = model.Observaciones;
            pago.FechaPago = model.FechaPago;

            ActualizarFacturaDespuesDePago(factura!, model.Metodo, model.FechaPago);
            _db.SaveChanges();

            RegistrarAuditoria($"Editó pago #{id}");
            TempData["Exito"] = "Pago actualizado correctamente.";

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Eliminar(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Index", "Login");

            var pago = _db.Pagos.Include(p => p.Factura).FirstOrDefault(p => p.IdPago == id);
            if (pago == null) return NotFound();

            var idFactura = pago.IdFactura;
            _db.Pagos.Remove(pago);
            _db.SaveChanges();
            SincronizarFactura(idFactura);

            RegistrarAuditoria($"Eliminó pago #{id}");
            TempData["Exito"] = "Pago eliminado correctamente.";

            return RedirectToAction("Index");
        }


        private void CargarCombos()
        {
            ViewBag.Facturas = _db.Facturas
                .OrderByDescending(f => f.FechaEmision)
                .Select(f => new SelectListItem
                {
                    Value = f.IdFactura.ToString(),
                    Text = $"Factura #{f.IdFactura} - {f.EstadoPago} - {f.Total:C0}"
                })
                .ToList();

            ViewBag.MetodosPago = MetodosPago
                .Select(m => new SelectListItem { Value = m, Text = m })
                .ToList();
        }

        private static void ActualizarFacturaDespuesDePago(Factura factura, string metodo, DateTime fechaPago)
        {
            factura.EstadoPago = "Pagado";
            factura.MetodoPago = metodo;
            factura.FechaPago = fechaPago;
            factura.MotivoAnulacion = null;
        }

        private void SincronizarFactura(int idFactura)
        {
            var factura = _db.Facturas.Find(idFactura);
            if (factura == null) return;

            bool tienePagos = _db.Pagos.Any(p => p.IdFactura == idFactura);
            if (!tienePagos)
            {
                factura.EstadoPago = "Pendiente";
                factura.MetodoPago = null;
                factura.FechaPago = null;
                _db.SaveChanges();
            }
        }

        private void RegistrarAuditoria(string accion)
        {
            if (int.TryParse(HttpContext.Session.GetString("UsuarioId"), out int idAdmin))
            {
                _db.LogAuditoria.Add(new LogAuditoria
                {
                    IdUsuario = idAdmin,
                    Accion = accion,
                    Modulo = "Gestión de Pagos"
                });
                _db.SaveChanges();
            }
        }
    }
}
