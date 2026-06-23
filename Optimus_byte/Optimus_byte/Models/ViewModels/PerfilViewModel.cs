using System.ComponentModel.DataAnnotations;

namespace Optimus_byte.Models.ViewModels
{
    public class PerfilViewModel
    {
        // ── Datos de solo lectura (identificación) ────────────────────────────
        public int IdUsuario { get; set; }
        public int IdCliente { get; set; }

        [Display(Name = "Tipo de documento")]
        public string TipoDocumento { get; set; } = "";

        [Display(Name = "Número de documento")]
        public string NumeroDocumento { get; set; } = "";

        // ── Datos editables ───────────────────────────────────────────────────
        [Required(ErrorMessage = "El nombre completo es obligatorio")]
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

        // ── Foto de perfil ────────────────────────────────────────────────────
        /// <summary>Ruta relativa almacenada en BD, ej. /img/Perfiles/guid.jpg</summary>
        public string? FotoUrl { get; set; }


        [Display(Name = "Foto de perfil")]
        public IFormFile? FotoArchivo { get; set; }

        // ── Confirmación de contraseña para guardar cambios ───────────────────
        [Required(ErrorMessage = "Debes confirmar tu contraseña para guardar cambios")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña actual")]
        public string ContrasenaActual { get; set; } = "";

        // ── Mensajes de estado ────────────────────────────────────────────────
        public string? Error { get; set; }
        public string? Exito { get; set; }

        public string? NuevaContrasena { get; set; }
        public string? ConfirmarNuevaContrasena { get; set; }
    }
}

