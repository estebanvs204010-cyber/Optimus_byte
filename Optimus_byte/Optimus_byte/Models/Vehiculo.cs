using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Optimus_byte.Models;

namespace Optimus_byte.Models
{
    [Table("Vehiculos")]
    public class Vehiculo
    {
        [Key]
        [Column("id_vehiculo")]
        public int IdVehiculo { get; set; }

        [Column("id_cliente")]
        public int IdCliente { get; set; }

        [Required]
        [Column("placa")]
        public string Placa { get; set; } = string.Empty;

        [Required]
        [Column("marca")]
        public string Marca { get; set; } = string.Empty;

        [Required]
        [Column("modelo")]
        public string Modelo { get; set; } = string.Empty;

        [Required]
        [Column("anio")]
        public int Anio { get; set; }

        [Column("color")]
        public string? Color { get; set; }

        [Column("vin")]
        public string? Vin { get; set; }

        [Column("km_actuales")]
        public int KmActuales { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("fecha_registro")]
        public DateTime FechaRegistro { get; set; }

        [ForeignKey("IdCliente")]
        public Cliente? Cliente { get; set; }
    }
}

