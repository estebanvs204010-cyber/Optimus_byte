using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Optimus_byte.Models
{
    [Table("OrdenesTrabajo")]
    public class OrdenTrabajo
    {
        [Key]
        [Column("id_orden")]
        public int IdOrden { get; set; }

        [Column("id_vehiculo")]
        public int IdVehiculo { get; set; }

        [Column("id_mecanico")]
        public int? IdMecanico { get; set; }

        [Column("id_administrador")]
        public int IdAdministrador { get; set; }

        [Column("estado")]
        public string Estado { get; set; } = string.Empty;

        [Column("tipo_servicio")]
        public string TipoServicio { get; set; } = string.Empty;

        [Column("descripcion_problema")]
        public string DescripcionProblema { get; set; } = string.Empty;

        [Column("diagnostico")]
        public string? Diagnostico { get; set; }

        [Column("observaciones")]
        public string? Observaciones { get; set; }

        [Column("km_ingreso")]
        public int KmIngreso { get; set; }

        [Column("fecha_apertura")]
        public DateTime FechaApertura { get; set; }

        [Column("fecha_cierre")]
        public DateTime? FechaCierre { get; set; }

        [ForeignKey("IdVehiculo")]
        public Vehiculo? Vehiculo { get; set; }

        [ForeignKey("IdMecanico")]
        public Usuario? Mecanico { get; set; }

        [ForeignKey("IdAdministrador")]
        public Usuario? Administrador { get; set; }
    }
}

