using System.ComponentModel.DataAnnotations;

namespace Optimus_byte.Models.ViewModels
{
    public class UsuarioEditarViewModel
    {
        public int IdUsuario { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string NombreCompleto { get; set; } = "";

        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Correo inválido")]
        public string Correo { get; set; } = "";

        public string Telefono { get; set; } = "";

        [Required]
        public int IdRol { get; set; }

        public bool Activo { get; set; }

        // Vacío = no cambiar contraseña
        public string? Contrasena { get; set; }
        public Rol? Rol { get; set; }
        public string NombreRol { get; set; } = "";
    }
}