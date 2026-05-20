using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VistaPrincipal.Models
{
    [Table("Facturas")]
    public class Factura
    {
        [Key]
        [Column("Id")]
        public int IdFactura { get; set; }

        [NotMapped]
        public int IdOrden { get; set; }

        [NotMapped]
        public int IdAdministrador { get; set; }

        [Column("Subtotal", TypeName = "decimal(14,2)")]
        public decimal Subtotal { get; set; }

        [Column("IVA", TypeName = "decimal(14,2)")]
        public decimal Iva { get; set; }

        [Column("Total", TypeName = "decimal(14,2)")]
        public decimal Total { get; set; }

        [Column("Estado")]
        public string EstadoPago { get; set; } = "Pendiente";

        [NotMapped]
        public string? MetodoPago { get; set; }

        [NotMapped]
        public string? MotivoAnulacion { get; set; }

        [Column("Fecha")]
        public DateTime FechaEmision { get; set; }

        [NotMapped]
        public DateTime? FechaPago { get; set; }

        [NotMapped]
        public Usuario? Administrador { get; set; }

        public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
    }
}
