using System.ComponentModel.DataAnnotations;

namespace Optimus_byte.Models.ViewModels
{
    public class RegistroViewModel
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [Display(Name = "Nombre completo")]
        public string NombreCompleto { get; set; } = "";

        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Ingresa un correo válido")]
        [Display(Name = "Correo electrónico")]
        public string Correo { get; set; } = "";

        [Required(ErrorMessage = "El teléfono es obligatorio")]
        [Phone(ErrorMessage = "Número de teléfono inválido")]
        [Display(Name = "Teléfono")]
        public string Telefono { get; set; } = "";

        [Required(ErrorMessage = "La dirección es obligatoria")]
        [Display(Name = "Dirección")]
        public string Direccion { get; set; } = "";

        [Required(ErrorMessage = "Selecciona el tipo de documento")]
        [Display(Name = "Tipo de documento")]
        public string TipoDocumento { get; set; } = "";

        [Required(ErrorMessage = "El número de documento es obligatorio")]
        [Display(Name = "Número de documento")]
        public string NumeroDocumento { get; set; } = "";

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [MinLength(6, ErrorMessage = "Mínimo 6 caracteres")]
        [Display(Name = "Contraseña")]
        public string Contrasena { get; set; } = "";

        [Required(ErrorMessage = "Confirma tu contraseña")]
        [Compare("Contrasena", ErrorMessage = "Las contraseñas no coinciden")]
        [Display(Name = "Confirmar contraseña")]
        public string ConfirmarContrasena { get; set; } = "";

        public string? Error { get; set; }

    }
}
