using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ECDatabaseEngine
{
    internal class ECSqLiteConnection : IECConnection
    {
        SQLiteConnection connection = null;
        SQLiteCommand command = null;

        public bool IsConnected => isConnected;
        public string CurrentDatabase => currentDatabase;
        public string CurrentUser => currentUser;

        public ECSqlStatementBuilderBase SqlBuilder => ECSqlStatementBuilderSqLite.Instance;

        private bool isConnected;
        private string currentDatabase;
        private string currentUser;

        public bool Connect(Dictionary<string, string> _params)
        {
            FileInfo fi = new FileInfo(_params["dbpath"]);
            DirectoryInfo d = fi.Directory;
            if (!d.Exists)
                d.Create();
            if (!File.Exists(_params["dbpath"]))
                using (FileStream f = File.Create(_params["dbpath"]))
                {
                    MemoryStream emptyDB = new MemoryStream(LibResource.empty);
                    emptyDB.Seek(0, SeekOrigin.Begin);
                    emptyDB.CopyTo(f);
                    f.Flush();
                    f.Close();
                }
            try
            {
                connection = new SQLiteConnection("Data Source=" + _params["dbpath"]);

                if (_params.Keys.Contains("pass"))
                    connection?.SetPassword(_params["pass"]);
                connection.Open();

                command = new SQLiteCommand(connection);
                currentDatabase = fi.Name;
                currentUser = "";
                isConnected = true;
            }
            catch (Exception e)
            {
                isConnected = false;
                throw e;
            }
            return true;
        }

        public void CreateTableIfNotExist(ECTable _table)
        {
            Type t = _table.GetType();
            PropertyInfo[] pi = t.GetProperties().Where(x => x.IsDefined(typeof(ECTableFieldAttribute), false)).ToArray();

            ECTableFieldAttribute tfa;

            if (!Exists(_table))
            {
                string createStmt = "CREATE TABLE " + t.Name + " (";
                int i = 0;
                foreach (PropertyInfo p in pi)
                {
                    tfa = p.GetCustomAttribute<ECTableFieldAttribute>();
                    createStmt += "`" + p.Name + "` " + FieldTypeToSqlType(tfa.type);
                    if (tfa.type == FieldType.VARCHAR)
                        createStmt += "(" + tfa.length.ToString() + ") ";

                    if (p.GetCustomAttribute(typeof(ECPrimaryKeyAttribute)) != null)
                    {
                        createStmt += " NOT NULL PRIMARY KEY";
                    }
                    else if (p.GetCustomAttribute(typeof(ECNotNullAttribute)) != null)
                        createStmt += " NOT NULL";
                    else
                        createStmt += " NULL";


                    if (p.GetCustomAttribute(typeof(ECAutoIncrementAttribute)) != null)
                        createStmt += " AUTOINCREMENT";

                    createStmt += ",";
                    i++;
                }

                createStmt = createStmt.Substring(0, createStmt.Length - 1) + ");"; //Delete the last ,                                    
                command.CommandText = createStmt;
                command.ExecuteNonQuery();
            }
        }

        public void Delete(ECTable _table)
        {
            Type t = _table.GetType();
            string sql = "DELETE FROM `" + t.Name + "` WHERE RecId=@RecId;";

            command.CommandText = sql;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("RecId", _table.RecId.ToString());
            command.ExecuteNonQuery();
        }

        public void Disconnect()
        {
            if (isConnected && connection != null)
            {
                connection.Close();
                isConnected = false;
                command.Dispose();
                connection.Dispose();
                command = null;
                connection = null;
            }
        }

        public bool Exists(ECTable _table)
        {
            Type t = _table.GetType();
            command.CommandText = "SELECT * FROM sqlite_master where type = 'table' AND name='" + t.Name + "'";
            return (command.ExecuteScalar() != null);
        }

        public List<Dictionary<string, string>> GetData(ECTable _table, Dictionary<string, string> _filter, Dictionary<string, KeyValuePair<string, string>> _ranges, List<string> _order)
        {
            Type t = _table.GetType();
            Dictionary<string, string> parms = new Dictionary<string, string>();
            command = new SQLiteCommand(connection);
            string sql = SqlBuilder.GenerateSqlForECTableWithPreparedStatements(_table, ref parms);


            command.Parameters.Clear();
            foreach (KeyValuePair<string, string> kv in parms)
                command.Parameters.AddWithValue(kv.Key, kv.Value);

            command.CommandText = sql;
            command.Prepare();
            return (ExecuteSql());
        }

        protected List<Dictionary<string, string>> ExecuteSql()
        {
            List<Dictionary<string, string>> ret = new List<Dictionary<string, string>>();
            Dictionary<string, string> currentRecord = new Dictionary<string, string>();
            SQLiteDataReader res = command.ExecuteReader();

            while (res.Read())
            {
                currentRecord = new Dictionary<string, string>();
                for (int i = 0; i < res.FieldCount; i++)
                    currentRecord.Add(res.GetName(i), res.GetValue(i).ToString());
                ret.Add(currentRecord);
            }

            res.Close();
            return ret;
        }

        public int Insert(ECTable _table)
        {
            Type t = _table.GetType();
            PropertyInfo[] properties = t.GetProperties().Where(x => x.IsDefined(typeof(ECTableFieldAttribute), false)).ToArray();

            command.Parameters.Clear();
            string sql = "INSERT INTO `" + t.Name + "` (";

            foreach (PropertyInfo p in properties.Where(x => !x.IsDefined(typeof(ECAutoIncrementAttribute), false)))
                sql += "`" + p.Name + "`,";
            sql = sql.Substring(0, sql.Length - 1); //delete last comma

            sql += ") VALUES (";
            foreach (PropertyInfo p in properties.Where(x => !x.IsDefined(typeof(ECAutoIncrementAttribute), false)))
            {
                command.Parameters.AddWithValue(p.Name, p.GetValue(_table));
                sql += "@" + p.Name + ",";
            }
            sql = sql.Substring(0, sql.Length - 1); //delete last comma

            sql += ");";

            command.CommandText = sql;
            command.Prepare();
            command.ExecuteNonQuery();

            sql = "SELECT MAX(RecId) AS RecId FROM " + _table.GetType().Name;
            command.CommandText = sql;
            using (SQLiteDataReader r = command.ExecuteReader())
            {
                r.Read();
                return r.GetInt32(r.GetOrdinal(nameof(_table.RecId)));
            }
        }

        public void Modify(ECTable _table)
        {
            Type t = _table.GetType();
            PropertyInfo[] properties = t.GetProperties().Where(x => x.IsDefined(typeof(ECTableFieldAttribute), false)).ToArray();
            string sql = "UPDATE `" + t.Name + "` SET ";

            foreach (PropertyInfo p in properties)
            {
                sql += "`" + p.Name + "`=" + _table.GetValueInSqlFormat(p) + ",";
            }
            sql = sql.Substring(0, sql.Length - 1) + " WHERE RecId=" + _table.RecId + ";";

            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        public string FieldTypeToSqlType(FieldType _ft)
        {
            switch (_ft)
            {
                case (FieldType.VARCHAR):
                    return "VARCHAR";
                case (FieldType.CHAR):
                    return "INTEGER";
                case (FieldType.INT):
                    return "INTEGER";
                case (FieldType.BLOB):
                    return "BLOB";
                case (FieldType.BOOLEAN):
                    return "BOOLEAN";
                case (FieldType.DATETIME):
                    return "DATETIME";
                case (FieldType.DECIMAL):
                    return "NUMERIC";
                case (FieldType.FLOAT):
                    return "REAL";
                case (FieldType.DOUBLE):
                    return "REAL";
                case (FieldType.DATE):
                    return "VARCHAR";
                case (FieldType.TEXT):
                    return "TEXT";
                default:
                    throw new Exception("Unknown FieldType");
            }
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
                command.CommandText = "ALTER TABLE '" + t.Name + "' ADD '" + kv.Key + "' " + kv.Value;

                if (p.GetCustomAttribute(typeof(ECPrimaryKeyAttribute)) != null)
                {
                    command.CommandText += " NOT NULL PRIMARY KEY";
                }
                else if (p.GetCustomAttribute(typeof(ECNotNullAttribute)) != null)
                    command.CommandText += " NOT NULL";
                else
                    command.CommandText += " NULL";

                if (p.GetCustomAttribute(typeof(ECAutoIncrementAttribute)) != null)
                    command.CommandText += " AUTOINCREMENT";

                command.CommandText += ";";
                command.ExecuteNonQuery();
            }

            //Delete Fields
            if (fieldsToDelete.Count > 0)
            {
                command.CommandText = "ALTER TABLE '" + t.Name + "' RENAME TO '" + t.Name + "_OLD'";
                command.ExecuteNonQuery();

                CreateTableIfNotExist(_table);

                string fields = "";
                foreach (PropertyInfo p in t.GetProperties().Where(x => x.IsDefined(typeof(ECTableFieldAttribute))))
                    fields += p.Name + ",";
                fields = fields.Substring(0, fields.Length - 1);

                command.CommandText = "INSERT INTO '" + t.Name + "' (" + fields + ") SELECT " + fields + " FROM '" + t.Name + "_OLD';";
                command.ExecuteNonQuery();

                command.CommandText = "DROP TABLE '" + t.Name + "_OLD" + "'";
                command.ExecuteNonQuery();
            }
        }

        internal void GetFieldsToChange(ECTable _table, ref Dictionary<string, string> _fieldsToAdd,
                                        ref Dictionary<string, string> _fieldsToDelete,
                                        ref Dictionary<string, string> _databaseFields)
        {
            Type t = _table.GetType();
            Dictionary<string, string> tableProperties = new Dictionary<string, string>();

            foreach (PropertyInfo p in _table.GetType().GetProperties().Where(x => x.IsDefined(typeof(ECTableFieldAttribute))))
            {
                ECTableFieldAttribute tfa = (ECTableFieldAttribute)p.GetCustomAttribute(typeof(ECTableFieldAttribute));
                if (tfa.type == FieldType.VARCHAR)
                    tableProperties.Add(p.Name, FieldTypeToSqlType(tfa.type) + "(" + tfa.length.ToString() + ")");
                else
                    tableProperties.Add(p.Name, FieldTypeToSqlType(tfa.type));
            }
            command.Parameters.Clear();
            command.CommandText = "PRAGMA table_info(" + t.Name + ");";
            SQLiteDataReader res = command.ExecuteReader();

            while (res.Read())
                _databaseFields.Add(res["name"].ToString(), res["type"].ToString().Replace(" ", ""));

            foreach (KeyValuePair<string, string> kv in tableProperties)
                if (!_databaseFields.Contains(kv))
                    _fieldsToAdd.Add(kv.Key, kv.Value);

            foreach (KeyValuePair<string, string> kv in _databaseFields)
                if (!tableProperties.Contains(kv))
                    _fieldsToDelete.Add(kv.Key, kv.Value);

            res.Close();
        }

    }
}