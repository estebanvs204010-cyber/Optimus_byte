namespace Optimus_byte.Models.ViewModels
{
    public class PagoResumenViewModel
    {
        public int IdPago { get; set; }
        public int IdFactura { get; set; }
        public int IdOrden { get; set; }
        public decimal Monto { get; set; }
        public decimal TotalFactura { get; set; }
        public string Metodo { get; set; } = string.Empty;
        public string EstadoPago { get; set; } = string.Empty;
        public string? ReferenciaTransaccion { get; set; }
        public string? Observaciones { get; set; }
        public DateTime FechaPago { get; set; }
        public string Administrador { get; set; } = string.Empty;
    }
}