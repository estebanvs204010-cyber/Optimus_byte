using Microsoft.EntityFrameworkCore;
using VistaPrincipal.Models;
using System.Collections.Generic;
using Optimus_byte.Models;

namespace Optimus_byte.Data
{
    public class optimusDBContext : DbContext
    {
        public optimusDBContext(DbContextOptions<optimusDBContext> options)
            : base(options) { }

        public DbSet<Rol> Roles { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Sesion> Sesiones { get; set; }
        public DbSet<LogAuditoria> LogAuditoria { get; set; }
        public DbSet<IntentoFallido> IntentosFallidos { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Factura> Facturas { get; set; }
        public DbSet<Pago> Pagos { get; set; }
    }
}