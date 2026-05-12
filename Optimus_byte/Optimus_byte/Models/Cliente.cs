using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VistaPrincipal.Models
{
    [Table("Clientes")]
    public class Cliente
    {
        [Key]
        [Column("id_cliente")]
        public int IdCliente { get; set; }
        [Column("id_usuario")]
        public int IdUsuario { get; set; }
        [Column("nombre_completo")]
        public string NombreCompleto { get; set; } = string.Empty;
        [Column("tipo_documento")]
        public string TipoDocumento { get; set; } = "CC";
        [Column("num_documento")]
        public string NumeroDocumento { get; set; } = string.Empty;
        [Column("telefono")]
        public string Telefono { get; set; } = string.Empty;
        [Column("correo")]
        public string Correo { get; set; } = string.Empty;
        [Column("direccion")]
        public string Direccion { get; set; } = string.Empty;
        [Column("activo")]
        public bool Activo { get; set; }
        [Column("fecha_registro")]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public DateTime FechaRegistro { get; set; }
        [ForeignKey("IdUsuario")]
        public Usuario? Usuario { get; set; }

    }
}
