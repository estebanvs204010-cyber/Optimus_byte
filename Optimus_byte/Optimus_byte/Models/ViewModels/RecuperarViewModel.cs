namespace Optimus_byte.Models.ViewModels
{
    public class RecuperarViewModel
    {
        public string Correo { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string NuevaContrasena { get; set; } = string.Empty;
        public string ConfirmarContrasena { get; set; } = string.Empty;
        public string? Error { get; set; }
        public string? Paso { get; set; } = "correo"; // correo → codigo → nueva
    }
}