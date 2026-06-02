namespace Optimus_byte.Models.ViewModels
{
    public class CitaClienteViewModel
    {
        public int IdCita { get; set; }
        public int IdVehiculo { get; set; }
        public string Cliente { get; set; } = "";
        public string Vehiculo { get; set; } = "";
        public string Placa { get; set; } = "";
        public string Servicio { get; set; } = "";
        public string Observaciones { get; set; } = "";
        public DateTime FechaHora { get; set; }
        public string Estado { get; set; } = "";
        public DateTime FechaCreacion { get; set; }
    }

    public class VehiculoCitaViewModel
    {
        public int IdVehiculo { get; set; }
        public string Placa { get; set; } = "";
        public string MarcaModelo { get; set; } = "";
    }

    public class CitasClientePageViewModel
    {
        public bool RequiereLogin { get; set; }
        public string NombreCliente { get; set; } = "";
        public List<VehiculoCitaViewModel> Vehiculos { get; set; } = new();
        public List<CitaClienteViewModel> MisCitas { get; set; } = new();
    }
}
