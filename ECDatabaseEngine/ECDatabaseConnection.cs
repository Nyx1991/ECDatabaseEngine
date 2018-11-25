using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECDatabaseEngine
{
    public enum FieldType { VARCHAR, CHAR, INT, BLOB, BOOLEAN, DATETIME, DECIMAL, FLOAT, DOUBLE, DATE, TEXT };
    public enum OrderType { ASC, DESC };

    public static class ECDatabaseConnection
    {
        internal static IECConnection               Connection;
        public static bool                          IsConnected => Connection.IsConnected;

        private static Dictionary<string, string>   parms;

        public static void CreateConnection(string connectionString)
        {
            ProcessConnectionString(connectionString);
            switch (parms["driver"])
            {
                case "mysql":
                    Connection = new ECMySQLConnection();
                    break;

                case "sqlite":
                    Connection = new ECSqLiteConnection();
                    break;

                default:
                    throw new Exception("Driver not found");
            }
            Connection.Connect(parms);
        }

        public static string            CurrentDatabase => Connection.CurrentDatabase;
        public static string            CurrentUser => Connection.CurrentUser;

        public static void Disconnect()
        {
            if (Connection != null)
                Connection.Disconnect();
        }

        public static void SynchronizeSchema(ECTable _table)
        {
            Connection.CreateTableIfNotExist(_table);
            Connection.AlterTableFields(_table);
        }
        
        private static void ProcessConnectionString(string _connStr)
        {
            parms = new Dictionary<string, string>();
            string[] keyValPairs = _connStr.Split(';');

            foreach (string kvPair in keyValPairs)
            {
                parms.Add(kvPair.Split('=')[0].ToLower(), kvPair.Split('=')[1]);
            }
        }

    }
}
