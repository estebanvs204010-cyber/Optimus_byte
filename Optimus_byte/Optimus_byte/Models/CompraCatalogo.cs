using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Optimus_byte.Models
{
    [Table("ComprasCatalogo")]
    public class CompraCatalogo
    {
        [Key]
        [Column("id_compra")]
        public int IdCompra { get; set; }

        [Column("id_cliente")]
        public int IdCliente { get; set; }

        [Column("subtotal")]
        public decimal Subtotal { get; set; }

        [Column("iva")]
        public decimal Iva { get; set; }

        [Column("total")]
        public decimal Total { get; set; }

        [Column("estado_pago")]
        public string EstadoPago { get; set; } = "Pendiente";

        [Column("referencia_payu")]
        public string ReferenciaPayU { get; set; } = string.Empty;

        [Column("transaccion_payu")]
        public string? TransaccionPayU { get; set; }

        [Column("fecha_compra")]
        public DateTime FechaCompra { get; set; }

        [Column("fecha_pago")]
        public DateTime? FechaPago { get; set; }
    }
}
