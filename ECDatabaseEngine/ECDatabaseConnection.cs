using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Reflection;

namespace ECDatabaseEngine
{
    public enum FieldType { VARCHAR, CHAR, INT, BLOB, BOOLEAN, DATETIME, DECIMAL, FLOAT, DOUBLE, DATE };

    public static class ECDatabaseConnection
    {
        public static bool              IsConnected { get; private set; }
        public static string            CurrentDatabase { get; private set; }
        public static string            CurrentUser { get; private set; }
        public static string            CurrentServer { get; private set; }
        private static MySqlConnection  connection;

        static ECDatabaseConnection()
        {
            Init();
        }

        private static void Init()
        {
            CurrentDatabase = "";
            CurrentServer = "";
            CurrentUser = "";
            IsConnected = false;
        }

        public static bool Connect(string _server, string _database, string _user, string _pass)
        {            
            MySqlConnectionStringBuilder connBuilder = new MySqlConnectionStringBuilder();

            connBuilder.Server = _server;
            connBuilder.UserID = _user;
            connBuilder.Password = _pass;
            connBuilder.Database = _database;

            connection = new MySqlConnection(connBuilder.ToString());            

            try
            {
                connection.Open();
                IsConnected = true;
                CurrentDatabase = _database;
                CurrentServer = _server;
                CurrentUser = _user;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.StackTrace);
                IsConnected = false;
                Init();
                return false;
            }
            return true;
        }

        public static void Disconnect()
        {
            if (IsConnected && connection != null)
                connection.Close();
        }

        public static void SynchronizeSchema(ECTable _table)
        {
            CreateTableIfNotExist(_table);            
        }
       
        internal static List<Dictionary<string, string>> ExecuteSql(string sql)
        {
            List<Dictionary<string, string>> ret = new List<Dictionary<string, string>>();
            Dictionary<string, string> currentRecord = new Dictionary<string, string>();

            MySqlCommand cmd = new MySqlCommand(sql, connection);
            MySqlDataReader res = cmd.ExecuteReader();

            while(res.Read())
            {
                currentRecord = new Dictionary<string, string>();
                for (int i = 0; i < res.FieldCount; i++)
                    currentRecord.Add(res.GetName(i), res.GetString(i));
                ret.Add(currentRecord);
            }
            
            res.Close();
            return ret;
        }

        internal static int Insert(ECTable _table)
        {
            Type t = _table.GetType();            
            PropertyInfo[] properties = t.GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute), false)).ToArray();

            string sql = "INSERT INTO `"+ t.Name +"` (";

            foreach (PropertyInfo p in properties.Where(x => !x.IsDefined(typeof(AutoIncrementAttribute), false)))
                sql += "`"+ p.Name +"`,";
            sql = sql.Substring(0, sql.Length-1); //delete last comma

            sql += ") VALUES (";
            foreach (PropertyInfo p in properties.Where(x => !x.IsDefined(typeof(AutoIncrementAttribute), false)))                            
                sql += _table.GetValueInSqlFormat(p) +",";
            sql = sql.Substring(0, sql.Length - 1); //delete last comma

            sql += ");";

            using (MySqlCommand cmd = new MySqlCommand(sql, connection))
                cmd.ExecuteNonQuery();

            sql = "SELECT MAX(RecId) AS RecId FROM " + _table.GetType().Name;
            using (MySqlCommand cmd = new MySqlCommand(sql, connection))
                using (MySqlDataReader res = cmd.ExecuteReader())
                {
                    res.Read();
                    return res.GetInt32("RecId");
                }            
        }

        internal static void Delete(ECTable _table)
        {            
            Type t = _table.GetType();            
            string sql = "DELETE FROM `" + t.Name + "` WHERE RecId="+_table.RecId.ToString()+";";
            
            using (MySqlCommand cmd = new MySqlCommand(sql, connection))
                cmd.ExecuteNonQuery();            
        }

        internal static void Modify(ECTable _table)
        {
            Type t = _table.GetType();
            PropertyInfo[] properties = t.GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute), false)).ToArray();
            string sql = "UPDATE `"+t.Name+"` SET ";

            foreach(PropertyInfo p in properties)
            {
                sql += "`"+p.Name+"`="+_table.GetValueInSqlFormat(p)+",";
            }
            sql = sql.Substring(0, sql.Length - 1)+" WHERE RecId="+_table.RecId+";";

            using (MySqlCommand cmd = new MySqlCommand(sql, connection))
                cmd.ExecuteNonQuery();
        }

        private static bool Exists(ECTable _table)
        {
            Type t = _table.GetType();
            string sql = "SELECT Count(*) AS c FROM information_schema.TABLES where " +
                         "TABLE_SCHEMA = '" + CurrentDatabase + "' AND TABLE_NAME = '" + t.Name + "'; ";
            MySqlCommand cmd = new MySqlCommand(sql, connection);
            MySqlDataReader res = cmd.ExecuteReader();

            res.Read();
            bool ret = (res.GetInt16("c") == 0);
            res.Close();
            return ret;
        }

        private static void CreateTableIfNotExist(ECTable _table)
        {
            Type t = _table.GetType();
            PropertyInfo[] pi = t.GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute), false)).ToArray();

            TableFieldAttribute tfa;
            if (Exists(_table))
            {
                string primKey = "";
                string createStmt = "CREATE TABLE `" + CurrentDatabase + "`.`" + t.Name + "` (";
                foreach (PropertyInfo p in pi)
                {
                    tfa = p.GetCustomAttribute<TableFieldAttribute>();
                    createStmt += "`" + p.Name + "` " + tfa.type.ToString();
                    if (tfa.type == FieldType.VARCHAR)
                        createStmt += "(" + tfa.length.ToString() + ") ";

                    if (p.GetCustomAttribute(typeof(PrimaryKeyAttribute)) != null)
                    {
                        createStmt += " NOT NULL";
                        primKey += "`" + p.Name + "`,";
                    }
                    else if (p.GetCustomAttribute(typeof(NotNullAttribute)) != null)
                        createStmt += " NOT NULL";
                    else
                        createStmt += " NULL";


                    if (p.GetCustomAttribute(typeof(AutoIncrementAttribute)) != null)
                        createStmt += " AUTO_INCREMENT";

                    createStmt += ",";
                }
                if (primKey != "")
                    createStmt += "PRIMARY KEY (" + primKey.Substring(0, primKey.Length - 1) + "));";
                else
                    createStmt = createStmt.Substring(0, createStmt.Length - 1) + ");"; //Delete the last ,                                    

                using (MySqlCommand cmd = new MySqlCommand(createStmt, connection))
                    cmd.ExecuteNonQuery();
            }
        }

    }
}
