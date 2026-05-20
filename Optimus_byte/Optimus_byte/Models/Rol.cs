using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Optimus_byte.Models
{
    [Table("Roles")]
    public class Rol
    {
        [Key]
        [Column("id_rol")]
        public int RolId { get; set; }

        [Column("nombre")]
        public string NombreRol { get; set; } = string.Empty;
    }
}
