using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VistaPrincipal.Models
{
    [Table("IntentosFallidos")]
    public class IntentoFallido
    {
        [Key]
        [Column("id_intento")]
        public int IdIntento { get; set; }

        [Column("correo")]
        public string Correo { get; set; } = string.Empty;

        [Column("bloqueado")]
        public bool Bloqueado { get; set; }
    }
}
