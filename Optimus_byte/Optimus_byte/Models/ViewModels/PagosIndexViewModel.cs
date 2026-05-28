using VistaPrincipal.Models.ViewModels;

namespace Optimus_byte.Models.ViewModels
{
    public class PagosIndexViewModel
    {
        public List<PagoResumenViewModel> Pagos { get; set; } = new();

        public int TotalPagos { get; set; }

        public int FacturasPendientes { get; set; }

        public int FacturasPagadas { get; set; }

        public decimal TotalRecaudado { get; set; }
    }
}