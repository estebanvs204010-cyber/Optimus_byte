using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Optimus_byte.Models
{
    [Table("DetalleCompraCatalogo")]
    public class DetalleCompraCatalogo
    {
        [Key]
        [Column("id_detalle")]
        public int IdDetalle { get; set; }

        [Column("id_compra")]
        public int IdCompra { get; set; }

        [Column("id_repuesto")]
        public int IdRepuesto { get; set; }

        [Column("cantidad")]
        public int Cantidad { get; set; }

        [Column("precio_unitario")]
        public decimal PrecioUnitario { get; set; }

        [Column("subtotal")]
        public decimal Subtotal { get; set; }
    }
}
