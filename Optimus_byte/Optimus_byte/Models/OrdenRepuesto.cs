using System.ComponentModel.DataAnnotations.Schema;

namespace Optimus_byte.Models
{
    [Table("OrdenRepuestos")]
    public class OrdenRepuesto
    {
        [Column("id_orden")]
        public int IdOrden { get; set; }

        [Column("id_repuesto")]
        public int IdRepuesto { get; set; }

        [Column("cantidad")]
        public int Cantidad { get; set; }

        [Column("precio_usado")]
        public decimal PrecioUsado { get; set; }

        [Column("fecha_uso")]
        public DateTime FechaUso { get; set; }

        [ForeignKey("IdOrden")]
        public OrdenTrabajo? Orden { get; set; }

        [ForeignKey("IdRepuesto")]
        public Repuesto? Repuesto { get; set; }
    }
}
