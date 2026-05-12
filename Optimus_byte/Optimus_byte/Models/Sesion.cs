using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VistaPrincipal.Models
{
    [Table("Sesiones")]
    public class Sesion
    {
        [Key]
        [Column("id_sesion")]
        public int IdSesion { get; set; }

        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [Column("token")]
        public string Token { get; set; } = string.Empty;

        [Column("activa")]
        public bool Activa { get; set; }
    }
}