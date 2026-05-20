using Microsoft.Data.SqlClient;

namespace Optimus_byte.DATA
{
    /// <summary>
    /// Clase helper para obtener conexiones ADO.NET.
    /// Reemplaza el DbContext de Entity Framework.
    /// </summary>
    public class DbHelper
    {
        private readonly IConfiguration _configuration;

        public DbHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Retorna una conexión abierta a la base de datos.
        /// Recuerda llamar .Dispose() o usar "using".
        /// </summary>
        public SqlConnection GetConnection()
        {
            var connStr = _configuration.GetConnectionString("DefaultConnection");
            var conn = new SqlConnection(connStr);
            conn.Open();
            return conn;
        }
    }
}
