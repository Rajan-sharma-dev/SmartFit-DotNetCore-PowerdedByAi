using System.Data;
using Microsoft.Data.SqlClient;

namespace MiddleWareWebApi.data
{
    // C#
    public class DapperContext
    {
        private readonly IConfiguration _config;
        public DapperContext(IConfiguration config) => _config = config;

        public IDbConnection CreateConnection() =>
            new SqlConnection(_config.GetConnectionString("DefaultConnection"));
    }
}
