using System.ComponentModel.DataAnnotations;

namespace Optimus_byte.Models.ViewModels
{
    public class PagoFormViewModel
    {
        public int IdPago { get; set; }

        [Display(Name = "Factura")]
        [Range(1, int.MaxValue, ErrorMessage = "Selecciona una factura.")]
        public int IdFactura { get; set; }

        [Display(Name = "Monto")]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a cero.")]
        public decimal Monto { get; set; }

        [Display(Name = "Método")]
        [Required(ErrorMessage = "El método de pago es obligatorio.")]
        public string Metodo { get; set; } = "Transferencia";

        [Display(Name = "Estado")]
        [Required(ErrorMessage = "El estado es obligatorio.")]
        public string EstadoPago { get; set; } = "Pagado";

        [Display(Name = "Referencia")]
        [StringLength(100)]
        public string? ReferenciaTransaccion { get; set; }

        [StringLength(300)]
        public string? Observaciones { get; set; }

        [Display(Name = "Fecha dee pago")]
        public DateTime FechaPago { get; set; } = DateTime.Now;
    }
}