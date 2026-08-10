using System.Configuration;
using System.Data.SqlClient;

namespace WMS_HU_Model.Infrastructure.Database
{
    public static class DbConnectionFactory
    {
        private static readonly string ConnectionString =
            ConfigurationManager.ConnectionStrings["WmsDb"].ConnectionString;

        public static SqlConnection Create()
        {
            return new SqlConnection(ConnectionString);
        }
    }
}