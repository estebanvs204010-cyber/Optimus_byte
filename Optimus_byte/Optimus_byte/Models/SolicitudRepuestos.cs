using System;

namespace Optimus_byte.Models
{
    public class SolicitudRepuesto
    {
        public int Id { get; set; }   // id_solicitud
        public int OrdenId { get; set; }   // id_orden
        public int MecanicoId { get; set; }   // id_mecanico
        public string NombreRepuesto { get; set; } = string.Empty;  // nombre_repuesto
        public int Cantidad { get; set; }   // cantidad
        public string? Motivo { get; set; }   // motivo
        public DateTime FechaSolicitud { get; set; }  // fecha_solicitud
        public bool Atendida { get; set; }   // atendida (BIT → bool)

        // Campos extras que se llenan por JOIN en el controlador
        public string? NombreMecanico { get; set; }
    }
}
