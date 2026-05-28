namespace VistaPrincipal.Models.ViewModels
{
    public class FacturaPagadaViewModel
    {
        public int IdFactura { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}