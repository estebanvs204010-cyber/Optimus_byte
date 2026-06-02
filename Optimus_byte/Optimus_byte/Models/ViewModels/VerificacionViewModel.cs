namespace Optimus_byte.Models.ViewModels
{
    public class VerificacionViewModel
    {
        public string Correo { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string? Error { get; set; }
    }
}