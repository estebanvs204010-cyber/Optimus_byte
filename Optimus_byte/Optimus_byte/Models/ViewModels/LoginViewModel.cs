using System.ComponentModel.DataAnnotations;

namespace VistaPrincipal.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Ingresa un correo válido")]
       
        public string Correo { get; set; } = "";

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [MinLength(6, ErrorMessage = "Mínimo 6 caracteres")]
        public string? Contrasena { get; set; }


        public bool Recordarme { get; set; }


        
    }
}
