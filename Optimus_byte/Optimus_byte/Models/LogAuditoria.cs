using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VistaPrincipal.Models
{
    [Table("LogAuditoria")]
    public class LogAuditoria
    {
        [Key]
        [Column("id_log")]
        public int IdLog { get; set; }

        [Column("id_usuario")]
        public int? IdUsuario { get; set; }

        [Column("accion")]
        public string Accion { get; set; } = string.Empty;

        [Column("modulo")]
        public string Modulo { get; set; } = string.Empty;
    }
}
