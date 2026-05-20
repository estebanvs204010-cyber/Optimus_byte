using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VistaPrincipal.Models
{
    [Table("Pagos")]
    public class Pago
    {
        [Key]
        [Column("Id")]
        public int IdPago { get; set; }

        [Required]
        [Column("FacturaId")]
        public int IdFactura { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a cero.")]
        [Column("Monto", TypeName = "decimal(14,2)")]
        public decimal Monto { get; set; }

        [Required(ErrorMessage = "El método de pago es obligatorio.")]
        [Column("MetodoPago")]
        public string Metodo { get; set; } = "Efectivo";

        [NotMapped]
        [StringLength(100)]
        public string? ReferenciaTransaccion { get; set; }

        [NotMapped]
        [StringLength(300)]
        public string? Observaciones { get; set; }

        [Column("FechaPago")]
        public DateTime FechaPago { get; set; } = DateTime.Now;

        [ForeignKey(nameof(IdFactura))]
        public Factura? Factura { get; set; }
    }
}
