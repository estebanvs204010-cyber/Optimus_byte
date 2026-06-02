namespace Optimus_byte.Models.ViewModels
{
    public class MecanicoDashboardViewModel
    {
        public string NombreMecanico { get; set; } = string.Empty;
        public List<CitaAsignadaViewModel> CitasAsignadas { get; set; } = new();
        public List<CitaClienteViewModel> CitasClientes { get; set; } = new();
        public List<ServicioVehiculoViewModel> HistorialServicios { get; set; } = new();
        public List<ChecklistTecnicoViewModel> Checklists { get; set; } = new();
        public List<EvidenciaViewModel> Evidencias { get; set; } = new();
        public List<MensajeInternoViewModel> Mensajes { get; set; } = new();
        public List<ManualTecnicoViewModel> Manuales { get; set; } = new();
        public List<HerramientaViewModel> Herramientas { get; set; } = new();
    }

    public class CitaAsignadaViewModel
    {
        public int Id { get; set; }
        public DateTime FechaHora { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string Vehiculo { get; set; } = string.Empty;
        public string Placa { get; set; } = string.Empty;
        public string Servicio { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public int TiempoEstimadoMinutos { get; set; }
    }

    public class ServicioVehiculoViewModel
    {
        public DateTime Fecha { get; set; }
        public string Vehiculo { get; set; } = string.Empty;
        public string Diagnostico { get; set; } = string.Empty;
        public string PartesReemplazadas { get; set; } = string.Empty;
        public string Tecnico { get; set; } = string.Empty;
    }

    public class ChecklistTecnicoViewModel
    {
        public string Nombre { get; set; } = string.Empty;
        public string Orden { get; set; } = string.Empty;
        public List<string> Pasos { get; set; } = new();
    }

    public class EvidenciaViewModel
    {
        public string Orden { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Nota { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
    }

    public class MensajeInternoViewModel
    {
        public string De { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public DateTime FechaHora { get; set; }
        public string Prioridad { get; set; } = string.Empty;
    }

    public class ManualTecnicoViewModel
    {
        public string Titulo { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    public class HerramientaViewModel
    {
        public string Nombre { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string UltimoUso { get; set; } = string.Empty;
    }
}
