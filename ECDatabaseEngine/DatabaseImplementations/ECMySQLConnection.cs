using System;
using System.Collections.Generic;
using System.Linq;
using MySql.Data.MySqlClient;
using System.Reflection;

namespace ECDatabaseEngine
{
    internal class ECMySQLConnection : IECConnection
    {
        MySqlConnection connection = null;

        public bool IsConnected => isConnected;
        public string CurrentDatabase => currentDatabase;
        public string CurrentUser => currentUser;


        private bool isConnected;
        private string currentDatabase;
        private string currentUser;

        public bool Connect(Dictionary<string, string> _params)
        {
            MySqlConnectionStringBuilder connBuilder = new MySqlConnectionStringBuilder();

            connBuilder.Server = _params["server"];
            connBuilder.UserID = _params["user"];
            connBuilder.Password = _params["pass"];
            connBuilder.Database = _params["database"];

            connection = new MySqlConnection(connBuilder.ToString());

            try
            {
                connection.Open();
                currentDatabase = _params["database"];
                currentUser = _params["user"];
                isConnected = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                isConnected = false;
                return false;
            }
            return true;
        }

        public void Disconnect()
        {
            if (isConnected && connection != null)
                connection.Close();
        }

        public List<Dictionary<string, string>> GetData(ECTable _table, Dictionary<string, string> _filter, Dictionary<string, KeyValuePair<string, string>> _ranges)
        {
            Type t = _table.GetType();
            string where = "";
            string sql = "SELECT * FROM `" + t.Name + "` ";

            foreach (KeyValuePair<string, KeyValuePair<string, string>> kp in _ranges.ToArray())
                if (kp.Value.Value.Equals(""))
                    where += kp.Key + "='" + kp.Value.Key + "' AND";
                else
                    where += "("+kp.Key + " BETWEEN " + kp.Value.Key + " AND " + kp.Value.Value + ") AND";

            foreach (KeyValuePair<string, string> kp in _filter.ToArray())
                where += kp.Key+" "+kp.Value+" AND";

            if (!where.Equals(""))
            {
                sql += "WHERE " + where.Substring(0, where.Length - 4) + ";";
            }

            return (ExecuteSql(sql));
        }

        protected List<Dictionary<string, string>> ExecuteSql(string sql)
        {
            List<Dictionary<string, string>> ret = new List<Dictionary<string, string>>();
            Dictionary<string, string> currentRecord = new Dictionary<string, string>();

            MySqlCommand cmd = new MySqlCommand(sql, connection);
            MySqlDataReader res = cmd.ExecuteReader();

            while (res.Read())
            {
                currentRecord = new Dictionary<string, string>();
                for (int i = 0; i < res.FieldCount; i++)
                    currentRecord.Add(res.GetName(i), res.GetString(i));
                ret.Add(currentRecord);
            }

            res.Close();
            return ret;
        }

        public int Insert(ECTable _table)
        {
            Type t = _table.GetType();
            PropertyInfo[] properties = t.GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute), false)).ToArray();

            string sql = "INSERT INTO `" + t.Name + "` (";

            foreach (PropertyInfo p in properties.Where(x => !x.IsDefined(typeof(AutoIncrementAttribute), false)))
                sql += "`" + p.Name + "`,";
            sql = sql.Substring(0, sql.Length - 1); //delete last comma

            sql += ") VALUES (";
            foreach (PropertyInfo p in properties.Where(x => !x.IsDefined(typeof(AutoIncrementAttribute), false)))
                sql += _table.GetValueInSqlFormat(p) + ",";
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

        public void Delete(ECTable _table)
        {
            Type t = _table.GetType();
            string sql = "DELETE FROM `" + t.Name + "` WHERE RecId=" + _table.RecId.ToString() + ";";

            using (MySqlCommand cmd = new MySqlCommand(sql, connection))
                cmd.ExecuteNonQuery();
        }

        public void Modify(ECTable _table)
        {
            Type t = _table.GetType();
            PropertyInfo[] properties = t.GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute), false)).ToArray();
            string sql = "UPDATE `" + t.Name + "` SET ";

            foreach (PropertyInfo p in properties)
            {
                sql += "`" + p.Name + "`=" + _table.GetValueInSqlFormat(p) + ",";
            }
            sql = sql.Substring(0, sql.Length - 1) + " WHERE RecId=" + _table.RecId + ";";

            using (MySqlCommand cmd = new MySqlCommand(sql, connection))
                cmd.ExecuteNonQuery();
        }

        public bool Exists(ECTable _table)
        {
            Type t = _table.GetType();
            string sql = "SELECT Count(*) AS c FROM information_schema.TABLES where " +
                         "TABLE_SCHEMA = '" + currentDatabase + "' AND TABLE_NAME = '" + t.Name + "'; ";
            MySqlCommand cmd = new MySqlCommand(sql, connection);
            MySqlDataReader res = cmd.ExecuteReader();

            res.Read();
            bool ret = (res.GetInt16("c") == 0);
            res.Close();
            return ret;
        }

        public void CreateTableIfNotExist(ECTable _table)
        {
            Type t = _table.GetType();
            PropertyInfo[] pi = t.GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute), false)).ToArray();

            TableFieldAttribute tfa;
            if (Exists(_table))
            {
                string primKey = "";
                string createStmt = "CREATE TABLE `" + currentDatabase + "`.`" + t.Name + "` (";
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

        public string GetConnectionStringExample()
        {
            return "driver=mysql;server=localhost;database=database;user=Username;pass=Password";
        }
    }
}
