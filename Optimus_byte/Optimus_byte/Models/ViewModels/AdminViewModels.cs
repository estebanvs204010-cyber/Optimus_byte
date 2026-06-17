namespace Optimus_byte.Models.ViewModels
{
    // ── Dashboard ────────────────────────────────────────────────
    public class DashboardViewModel
    {
        public int     TotalUsuarios      { get; set; }
        public int     TotalClientes      { get; set; }
        public int     TotalVehiculos     { get; set; }
        public int     OrdenesAbiertas    { get; set; }
        public int     OrdenesHoy         { get; set; }
        public int     CitasHoy           { get; set; }
        public int     VehiculosMantenimiento { get; set; }
        public int     ProximasCitas      { get; set; }
        public int     RepuestosBajoStock { get; set; }
        public decimal IngresosMes        { get; set; }

        public List<OrdenResumenViewModel> OrdenesRecientes { get; set; } = new();
        public List<RepuestoCriticoViewModel> RepuestosCriticos { get; set; } = new();
        public List<CitaClienteViewModel>      CitasRecientes    { get; set; } = new();
    }

    // ── Órdenes ──────────────────────────────────────────────────
    public class OrdenResumenViewModel
    {
        public int IdOrden { get; set; }
        public string Estado { get; set; } = "";
        public string TipoServicio { get; set; } = "";
        public DateTime FechaApertura { get; set; }
        public DateTime? FechaCierre { get; set; }
        public DateTime? FechaEntregaEstimada { get; set; }   // ← NUEVO
        public string Placa { get; set; } = "";
        public string MarcaModelo { get; set; } = "";
        public string Cliente { get; set; } = "";
        public string Mecanico { get; set; } = "";
        public string Administrador { get; set; } = "";
    }

    public class OrdenDetalleViewModel
    {
        public int IdOrden { get; set; }
        public string Estado { get; set; } = "";
        public string TipoServicio { get; set; } = "";
        public string DescripcionProblema { get; set; } = "";
        public string Diagnostico { get; set; } = "";
        public string Observaciones { get; set; } = "";
        public int KmIngreso { get; set; }
        public DateTime FechaApertura { get; set; }
        public DateTime? FechaCierre { get; set; }
        public int? IdMecanicoActual { get; set; }
        public string Placa { get; set; } = "";
        public string MarcaModelo { get; set; } = "";
        public string Color { get; set; } = "";
        public int KmActuales { get; set; }
        public string Cliente { get; set; } = "";
        public string TelefonoCliente { get; set; } = "";
        public string CorreoCliente { get; set; } = "";
        public string Mecanico { get; set; } = "";

        public List<RepuestoUsadoViewModel> Repuestos { get; set; } = new();
        public List<EstadoHistorialViewModel> Historial { get; set; } = new();
    }

    public class RepuestoUsadoViewModel
    {
        public string Nombre { get; set; } = "";
        public string Referencia { get; set; } = "";
        public int Cantidad { get; set; }
        public decimal PrecioUsado { get; set; }
    }

    public class EstadoHistorialViewModel
    {
        public string EstadoNuevo { get; set; } = "";
        public string Observacion { get; set; } = "";
        public DateTime FechaCambio { get; set; }
        public string Usuario { get; set; } = "";
    }

    // ── Inventario ───────────────────────────────────────────────
    public class RepuestoViewModel
    {
        public int IdRepuesto { get; set; }
        public string Nombre { get; set; } = "";
        public string Referencia { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public string Categoria { get; set; } = "";
        public decimal PrecioUnitario { get; set; }
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
        public DateTime FechaRegistro { get; set; }
        public bool Activo { get; set; }
        public bool BajoStock => StockActual <= StockMinimo;
    }

    public class RepuestoCriticoViewModel
    {
        public string Nombre { get; set; } = "";
        public string Referencia { get; set; } = "";
        public int StockActual { get; set; }
        public int StockMinimo { get; set; }
    }

    // ── Facturas ─────────────────────────────────────────────────
    public class FacturaViewModel
    {
        public int IdFactura { get; set; }
        public int IdOrden { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public string EstadoPago { get; set; } = "";
        public string MetodoPago { get; set; } = "";
        public DateTime FechaEmision { get; set; }
        public DateTime? FechaPago { get; set; }
        public string Placa { get; set; } = "";
        public string MarcaModelo { get; set; } = "";
        public string Cliente { get; set; } = "";
    }

    // ── Solicitudes de repuesto (notificaciones al admin) ─────────
    public class SolicitudRepuestoViewModel
    {
        public int IdSolicitud { get; set; }
        public int IdOrden { get; set; }
        public string NombreRepuesto { get; set; } = "";
        public int Cantidad { get; set; }
        public string Motivo { get; set; } = "";
        public string MecanicoNombre { get; set; } = "";
        public DateTime FechaSolicitud { get; set; }
        public bool Atendida { get; set; }
    }
}
