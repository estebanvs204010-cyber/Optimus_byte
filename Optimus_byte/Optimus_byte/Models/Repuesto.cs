using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Optimus_byte.Models
{
    [Table("Repuestos")]
    public class Repuesto
    {
        [Key]
        [Column("id_repuesto")]
        public int IdRepuesto { get; set; }

        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Column("referencia")]
        public string Referencia { get; set; } = string.Empty;

        [Column("descripcion")]
        public string? Descripcion { get; set; }

        [Column("categoria")]
        public string Categoria { get; set; } = string.Empty;

        [Column("precio_unitario")]
        public decimal PrecioUnitario { get; set; }

        [Column("stock_actual")]
        public int StockActual { get; set; }

        [Column("stock_minimo")]
        public int StockMinimo { get; set; }

        [Column("activo")]
        public bool Activo { get; set; }

        [Column("fecha_registro")]
        public DateTime FechaRegistro { get; set; }
    }
}
