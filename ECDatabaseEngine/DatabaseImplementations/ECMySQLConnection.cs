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
        MySqlCommand command = null;

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
            {
                connection.Close();
                connection = null;
                isConnected = false;
            }
        }

        public List<Dictionary<string, string>> GetData(ECTable _table, Dictionary<string, string> _filter, Dictionary<string, KeyValuePair<string, string>> _ranges, List<string> _order)
        {
            command = new MySqlCommand();
            command.Connection = connection;

            Type t = _table.GetType();
            List<string> where = new List<string>();
            Dictionary<string, string> parms = new Dictionary<string, string>();
            //Select From
            string sql = _table.MakeSelectFrom(true);

            //Joins
            sql += _table.MakeJoins();

            //Where
            command.Parameters.Clear();
            _table.GetParameterizedWhereClause(ref where, ref parms);

            foreach (KeyValuePair<string, string> kv in parms)
                command.Parameters.AddWithValue(kv.Key, kv.Value);
            if (where.Count != 0)
            {
                sql += " WHERE ";
                foreach (string s in where)
                    sql += "(" + s + ") AND";
                sql = sql.Substring(0, sql.Length - 4);
            }

            //Order By
            string orderClause = _table.GetOrderByClause();
            if (orderClause.Length > 0)
                sql += " ORDER BY "+ orderClause + " " + _table.OrderType.ToString();

            sql += ";";
            command.CommandText = sql;
            command.Prepare();

            return (ExecuteSql());
        }

        protected List<Dictionary<string, string>> ExecuteSql()
        {
            List<Dictionary<string, string>> ret = new List<Dictionary<string, string>>();
            Dictionary<string, string> currentRecord = new Dictionary<string, string>();
            
            MySqlDataReader res = command.ExecuteReader();

            while (res.Read())
            {
                currentRecord = new Dictionary<string, string>();
                for (int i = 0; i < res.FieldCount; i++)
                {
                    if (!res.IsDBNull(i))
                        currentRecord.Add(res.GetName(i), res.GetString(i));
                    else
                        currentRecord.Add(res.GetName(i), "");
                }
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

        public string FieldTypeToSqlType(FieldType _ft)
        {
            return _ft.ToString();
        }

        public void AlterTableFields(ECTable _table)
        {
            Type t = _table.GetType();
            Dictionary<string, string> fieldsToAdd = new Dictionary<string, string>();
            Dictionary<string, string> fieldsToDelete = new Dictionary<string, string>();
            Dictionary<string, string> databaseFields = new Dictionary<string, string>();

            GetFieldsToChange(_table, ref fieldsToAdd, ref fieldsToDelete, ref databaseFields);

            //Add Fields
            foreach (KeyValuePair<string, string> kv in fieldsToAdd)
            {
                PropertyInfo p = t.GetProperty(kv.Key);
                string sql = "ALTER TABLE `" + t.Name + "` ADD `" + kv.Key + "` " + kv.Value;

                if (p.GetCustomAttribute(typeof(PrimaryKeyAttribute)) != null)
                {
                    sql += " NOT NULL PRIMARY KEY";
                }
                else if (p.GetCustomAttribute(typeof(NotNullAttribute)) != null)
                    sql += " NOT NULL";
                else
                    sql += " NULL";

                if (p.GetCustomAttribute(typeof(AutoIncrementAttribute)) != null)
                    sql += " AUTOINCREMENT";

                using (MySqlCommand cmd = new MySqlCommand(sql, connection))
                    cmd.ExecuteNonQuery();
            }

            //Delete Fields
            foreach(KeyValuePair<string, string> kv in fieldsToDelete)
            { 
                string sql = "ALTER TABLE `" + t.Name + "` DROP COLUMN `" + kv.Key + "`";
                using (MySqlCommand cmd = new MySqlCommand(sql, connection))
                    cmd.ExecuteNonQuery();                
            }
        }        

        internal void GetFieldsToChange(ECTable _table, ref Dictionary<string, string> _fieldsToAdd,
                                        ref Dictionary<string, string> _fieldsToDelete,
                                        ref Dictionary<string, string> _databaseFields)
        {
            Type t = _table.GetType();
            Dictionary<string, string> tableProperties = new Dictionary<string, string>();

            foreach (PropertyInfo p in _table.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
            {
                TableFieldAttribute tfa = (TableFieldAttribute)p.GetCustomAttribute(typeof(TableFieldAttribute));
                if (tfa.type == FieldType.VARCHAR)
                    tableProperties.Add(p.Name, FieldTypeToSqlType(tfa.type) + "(" + tfa.length.ToString() + ")");
                else
                    tableProperties.Add(p.Name, FieldTypeToSqlType(tfa.type));
            }
            string sql = "SHOW COLUMNS FROM `" + t.Name + "`;";

            MySqlCommand cmd = new MySqlCommand(sql, connection);
            MySqlDataReader res = cmd.ExecuteReader();

            while (res.Read())
            {
                string type = res["Type"].ToString().ToUpper();
                if (type.Substring(0, type.IndexOf('(')) != "VARCHAR")
                    type = type.Substring(0, type.IndexOf('('));
                _databaseFields.Add(res["Field"].ToString(), type);
            }
                

            foreach (KeyValuePair<string, string> kv in tableProperties)
                if (!_databaseFields.Contains(kv))
                    _fieldsToAdd.Add(kv.Key, kv.Value);

            foreach (KeyValuePair<string, string> kv in _databaseFields)
                if (!tableProperties.Contains(kv))
                    _fieldsToDelete.Add(kv.Key, kv.Value);

            res.Close();
        }

        public void SetPassword(string _password)
        {
            throw new Exception("MySQL hast no password functionallity");
        }
    }
}
