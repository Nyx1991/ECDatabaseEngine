using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections;
using MySql.Data.MySqlClient;

namespace ECDatabaseEngine
{
    public abstract class ECTable : IEnumerator, IEnumerable, IEquatable<ECTable>
    {
        protected List<ECTable> records;
        private Dictionary<string, string> filter;
        private Dictionary<string, KeyValuePair<string, string>> ranges;
        private List<string> order;
        public OrderType OrderType { get; set; }
        private string SqlTableName { get => "`" + TableName + "`."; }

        private List<ECJoin> joins;

        static int nextGlobalRecId = 0;
        int globalRecId = 0;
        int currentRecord;

        /// <summary>
        /// Markes a record
        /// </summary>
        public bool Marked { get; set; }

        [TableField(FieldType.INT)]
        [NotNull]
        [PrimaryKey]
        [AutoIncrement]
        public int RecId { get; internal set; }

        public object Current => records[currentRecord];

        public int Count => records.Count;

        public String TableName { get { return this.GetType().Name; } }

        public ECTable()
        {
            Init();
        }

        #region Init/Reset/Clear
        /// <summary>
        /// Removes all filters and ranges and unloads all loaded Records. Initializes all Fields.
        /// </summary>
        public void Init()
        {
            if (records != null)
                records.Clear();
            else
                records = new List<ECTable>();

            currentRecord = 0;
            filter = new Dictionary<string, string>();
            ranges = new Dictionary<string, KeyValuePair<string, string>>();
            joins = new List<ECJoin>();
            order = new List<string>();
            OrderType = OrderType.ASC;
            foreach (PropertyInfo p in this.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
            {
                Type type = p.PropertyType;
                if (type != typeof(string))
                    p.SetValue(this, Activator.CreateInstance(p.PropertyType));
                else
                    p.SetValue(this, "");
            }
        }

        /// <summary>
        /// Removes all filters, ranges and joins
        /// </summary>
        public void Clear()
        {
            filter.Clear();
            ranges.Clear();
            joins.Clear();
            order.Clear();
        }

        /// <summary>
        /// IEnumerator implementation: Set current Position before to first record
        /// </summary>
        public void Reset()
        {
            InvokeMethodeOnJoinedTables("Reset");
            currentRecord = 0;
        }

        protected void DeleteRecords()
        {
            if (records != null)
            {
                records.Clear();
            }
        }
        #endregion

        #region Joins

        public T JoinedTable<T>()
        {
            try
            {
                return (T)joins.First(x => x.TableType == typeof(T)).Table;
            }
            catch
            {
                throw new ECJoinNotFoundException("Join for table '" + typeof(T).Name + "' does not exist");
            }
        }

        public void AddJoin(ECTable _table, string _onSourceField, ECJoinType _joinType)
        {
            if (GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute)) && x.Name == _onSourceField).Count() == 0)
                throw new ECFieldNotFoundException("Field '" + _onSourceField + "' not found in table '" + TableName + "'");

            ECJoin join = new ECJoin();
            join.JoinType = _joinType;
            join.OnSourceField = _onSourceField;
            join.Table = _table;
            joins.Add(join);
        }

        public void AddJoin(ECTable _table, string _onSourceField, string _onTargetField, ECJoinType _joinType)
        {
            if (GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute)) && x.Name == _onSourceField).Count() == 0)
                throw new ECFieldNotFoundException("Field '" + _onSourceField + "' not found in table '" + TableName + "'");

            if (_table.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute)) && x.Name == _onTargetField).Count() == 0)
                throw new ECFieldNotFoundException("Field '" + _onTargetField + "' not found in table '" + _table.TableName + "'");


            ECJoin join = new ECJoin();
            join.JoinType = _joinType;
            join.OnTargetField = _onTargetField;
            join.OnSourceField = _onSourceField;
            join.Table = _table;
            joins.Add(join);
        }

        #endregion

        #region GetData

        /// <summary>
        /// Get the next record
        /// </summary>
        /// <returns>True if a record was found. False if no more record was found</returns>
        public bool Next()
        {
            if (records.Count == 0)
                return false;
            records[currentRecord].CopyFrom(this);
            bool ret = false;
            if (records.Count == 0)
                return false;
            else if (currentRecord >= records.Count - 1)
            {
                currentRecord = 0;
                ret = false;
            }
            else
            {
                currentRecord++;
                ret = true;
            }
            CopyFrom(records[currentRecord]);
            InvokeMethodeOnJoinedTables("Next");
            return ret;
        }

        /// <summary>
        /// Get the last record
        /// </summary>
        public void Last()
        {
            if (records.Count > 0)
            {
                currentRecord = records.Count - 1;
            }
            else
            {
                currentRecord = 0;
            }
            CopyFrom(records[currentRecord]);
            InvokeMethodeOnJoinedTables("Last");
        }

        /// <summary>
        /// Get the firs record
        /// </summary>
        public void First()
        {
            currentRecord = 0;
            CopyFrom(records[currentRecord]);
            InvokeMethodeOnJoinedTables("First");
        }

        public bool MoveNext()
        {
            return Next();
        }

        /// <summary>
        /// Load records from database.
        /// </summary>
        public void FindSet()
        {
            InitTableDataFromDictionaryList(ECDatabaseConnection.Connection.GetData(this, filter, ranges, order));
        }

        #endregion

        #region Filter Data and order
        public void Get(int _recId)
        {
            filter = new Dictionary<string, string>();
            filter.Add("RecId", _recId.ToString());
            InitTableDataFromDictionaryList(ECDatabaseConnection.Connection.GetData(this, filter,
                                                                                    new Dictionary<string,
                                                                                    KeyValuePair<string, string>>(),
                                                                                    order));
        }

        public void SetFilter(string _fieldName, string _filterString = "")
        {
            if (GetType().GetProperty(_fieldName) == null)
                new Exception(String.Format("Field with name '{0}' does not exist in {1}", _fieldName, GetType().Name));

            if (filter.Keys.Contains(_fieldName))
                if (_filterString.Equals(""))
                    filter.Remove(_fieldName);
                else
                    filter[_fieldName] = _filterString;
            else
                filter.Add(_fieldName, _filterString);
        }

        public void SetRange(string _fieldName, string _from = "", string _to = "")
        {
            if (GetType().GetProperty(_fieldName) == null)
                new Exception(String.Format("Field with name '{0}' does not exist in {1}", _fieldName, GetType().Name));

            if (ranges.Keys.Contains(_fieldName))
                if (_from.Equals("") && _to.Equals(""))
                    ranges.Remove(_fieldName);
                else
                    ranges[_fieldName] = new KeyValuePair<string, string>(_from, _to);
            else
                ranges.Add(_fieldName, new KeyValuePair<string, string>(_from, _to));
        }

        public void AddOrderBy(string _fieldName)
        {
            order.Add(_fieldName);
        }

        #endregion

        #region Database operations
        public void Insert()
        {
            RecId = ECDatabaseConnection.Connection.Insert(this);
            Get(RecId);
            Clear();
        }

        /// <summary>
        /// Deletes the current record from the database.
        /// Has no impact on joined tables.
        /// </summary>
        public void Delete()
        {
            if (records.Count == 0)
            {
                currentRecord = 0;
                Init();
            }
            else
            {
                ECDatabaseConnection.Connection.Delete(this);
            }

        }

        /// <summary>
        /// Deletes all currently loaded records from the database.
        /// Has no impact on joined tables.
        /// </summary>
        public void DeleteAll()
        {
            FindSet();
            do
            {
                Delete();
            } while (this.RecId != 0);
        }

        /// <summary>
        /// Deletes only loaded records. Calling this function has no impact on database
        /// </summary>
        internal void DeleteAndSynchronizeJoins()
        {
            currentRecord = records.IndexOf(this);

            records.Remove(this);

            if (currentRecord > records.Count - 1) //Deleted last record
                currentRecord = records.Count - 1;
            else
                currentRecord = currentRecord % records.Count;
            CopyFrom(records[currentRecord]);

            foreach (ECJoin j in joins)
            {
                ECTable table = ((ECTable)j.Table);
                table.DeleteAndSynchronizeJoins();
            }

        }

        /// <summary>
        /// Writes the current record to the database
        /// Has no impact on joined tables.
        /// </summary>
        public void Modify()
        {
            ECDatabaseConnection.Connection.Modify(this);
            records[currentRecord] = this;
        }

        /// <summary>
        /// Returns the position of the current record in the loaded dataset
        /// </summary>
        /// <returns>Index of the current record</returns>
        public int GetRecPos()
        {
            return currentRecord;
        }

        /// <summary>
        /// Loads the record at the given position in the dataset
        /// </summary>
        /// <param name="_pos">Index of the record</param>
        public void SetRecPos(int _pos)
        {
            if (_pos > records.Count - 1)
                throw new IndexOutOfRangeException();

            records[currentRecord].CopyFrom(this);
            currentRecord = _pos;

            CopyFrom(records[currentRecord]);
            InvokeMethodeOnJoinedTables("SetRecPos", new object[] { _pos });
        }

        /// <summary>
        /// Writes all records to the database
        /// </summary>
        public void ModifyAll()
        {
            FindSet();
            do
            {
                Modify();
            } while (Next());
        }

        public void SynchronizeSchema()
        {
            ECDatabaseConnection.SynchronizeSchema(this);
        }
        #endregion

        #region Data manipulation: Copy and convert
        public void CopyFrom(ECTable _table)
        {
            PropertyInfo targetPi;
            foreach (PropertyInfo sourcePi in _table.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
                if ((targetPi = GetType().GetProperty(sourcePi.Name)) != null)
                    targetPi.SetValue(this, sourcePi.GetValue(_table));
            globalRecId = _table.globalRecId;
            Marked = _table.Marked;
        }

        public string DateTimeToSqlDate(DateTime _dt)
        {
            return String.Format("{0}-{1}-{2}", _dt.Year, _dt.Month, _dt.Day);
        }

        public string DateTimeToSqlDateTime(DateTime _dt)
        {
            return String.Format("{0}-{1}-{2} {3}:{4}:{5}.{6}", _dt.Year, _dt.Month, _dt.Day, _dt.Hour, _dt.Minute, _dt.Second, _dt.Millisecond);
        }
        #endregion

        #region Database helper functions
        internal ECTable(Dictionary<string, string> _values) : this()
        {
            InitRecordFromDictionary(_values);
        }

        internal string GetValueInSqlFormat(PropertyInfo _p)
        {
            string sql = "";
            TableFieldAttribute tfa = this.GetType().GetCustomAttribute<TableFieldAttribute>();
            tfa = _p.GetCustomAttribute<TableFieldAttribute>();
            if (tfa.type == FieldType.VARCHAR || tfa.type == FieldType.CHAR || tfa.type == FieldType.TEXT)
                sql += "'" + _p.GetValue(this).ToString() + "'";
            else if (tfa.type == FieldType.INT || tfa.type == FieldType.DECIMAL || tfa.type == FieldType.FLOAT || tfa.type == FieldType.DOUBLE)
                sql += _p.GetValue(this).ToString();
            else if (tfa.type == FieldType.DATE)
            {
                DateTime dt = (DateTime)_p.GetValue(this);
                sql = String.Format("'{0}-{1}-{2}'", dt.Year, dt.Month, dt.Day);
            }
            else if (tfa.type == FieldType.DATETIME)
            {
                DateTime dt = (DateTime)_p.GetValue(this);
                sql = String.Format("'{0}-{1}-{2} {3}:{4}:{5}.{6}'", dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond);
            }
            else
                throw new NotImplementedException();

            if (tfa.type == FieldType.BOOLEAN)
                sql += "'" + _p.GetValue(this) + "',";

            return sql;
        }

        internal void ConvertAndStore(PropertyInfo _p, string value)
        {
            TableFieldAttribute tfa = _p.GetCustomAttribute<TableFieldAttribute>();
            string[] date, time;

            switch (tfa.type)
            {
                case FieldType.BOOLEAN:
                    if (value == "")
                        _p.SetValue(this, false);
                    else
                        _p.SetValue(this, Convert.ToBoolean(value));
                    break;

                case FieldType.CHAR:
                    if (value == "")
                        _p.SetValue(this, ' ');
                    else
                        _p.SetValue(this, Convert.ToChar(value));
                    break;

                case FieldType.DATE:
                    if (value == "")
                        _p.SetValue(this, new DateTime());
                    else
                    {
                        date = value.Split('-');
                        _p.SetValue(this, new DateTime(Convert.ToInt32(date[0]), Convert.ToInt32(date[1]), Convert.ToInt32(date[2])));
                    }
                    break;

                case FieldType.DATETIME:
                    if (value == "")
                        _p.SetValue(this, new DateTime());
                    else
                    {
                        date = value.Split(' ')[0].Split('-');
                        time = value.Split(' ')[1].Split(':');
                        _p.SetValue(this, new DateTime(Convert.ToInt32(date[0]), Convert.ToInt32(date[1]), Convert.ToInt32(date[2]),
                                                       Convert.ToInt32(time[0]), Convert.ToInt32(time[1]), Convert.ToInt32(time[2].Split('.')[0]),
                                                       Convert.ToInt32(time[2].Split('.')[1])));
                    }
                    break;

                case FieldType.DECIMAL:
                    if (value == "")
                        _p.SetValue(this, 0.0);
                    else
                        _p.SetValue(this, Convert.ToDecimal(value));
                    break;

                case FieldType.DOUBLE:
                    if (value == "")
                        _p.SetValue(this, 0);
                    else
                        _p.SetValue(this, Convert.ToDouble(value));
                    break;

                case FieldType.FLOAT:
                    if (value == "")
                        _p.SetValue(this, 0);
                    else
                        _p.SetValue(this, (float)Convert.ToDouble(value));
                    break;

                case FieldType.INT:
                    if (value == "")
                        _p.SetValue(this, 0);
                    else
                        _p.SetValue(this, Convert.ToInt32(value));
                    break;

                case FieldType.VARCHAR:
                    _p.SetValue(this, value);
                    break;

                case FieldType.TEXT:
                    _p.SetValue(this, value);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        internal void InitRecordFromDictionary(Dictionary<string, string> _values)
        {
            Type t = this.GetType();
            foreach (KeyValuePair<string, string> kv in _values)
            {
                if (kv.Key.Split('.').Count() == 2)
                {
                    string[] splittedFieldName = kv.Key.Split('.');
                    if (splittedFieldName[0].ToLower() == TableName.ToLower())
                    {
                        ConvertAndStore(t.GetProperty(splittedFieldName[1]), kv.Value);
                    }
                }
                else
                {
                    ConvertAndStore(t.GetProperty(kv.Key), kv.Value);
                }
            }
        }

        internal void InitTableDataFromDictionaryList(List<Dictionary<string, string>> _dataDict)
        {
            DeleteRecords();
            ECTable table;
            foreach (Dictionary<string, string> d in _dataDict)
            {
                table = (ECTable)Activator.CreateInstance(this.GetType());
                table.InitRecordFromDictionary(d);
                table.globalRecId = nextGlobalRecId;
                records.Add(table);
                nextGlobalRecId++;
            }
            foreach (ECJoin j in joins)
            {
                ECTable joinedTable = (ECTable)j.Table;
                joinedTable.InitTableDataFromDictionaryList(_dataDict);
            }
            if (records.Count == 0)
            {
                Clear();
            }
            else
            {
                currentRecord = 0;
                CopyFrom(records.First());
            }
        }

        internal void GetParameterizedWhereClause(ref List<string> _where, ref Dictionary<string, string> _parameters)
        {            
            foreach (KeyValuePair<string, KeyValuePair<string, string>> kp in ranges)
                if (kp.Value.Value.Equals(""))
                {
                    string keyParm = TableName + kp.Key;
                    _parameters.Add(keyParm, kp.Value.Key);
                    _where.Add(SqlTableName + kp.Key + "=@" + keyParm);
                }
                else
                {
                    string keyParm = TableName + kp.Key;
                    _parameters.Add("K" + keyParm, kp.Value.Key);
                    _parameters.Add("V" + keyParm, kp.Value.Value);
                    _where.Add("(" + SqlTableName + kp.Key + " BETWEEN @K" + keyParm + " AND @V" + keyParm + ")");
                }

            foreach (KeyValuePair<string, string> kp in filter)
            {
                _where.Add(ParseFilterString(kp.Key, kp.Value, ref _parameters));
            }

            foreach (ECJoin j in joins)
            {
                ECTable joinTable = (ECTable)j.Table;
                joinTable.GetParameterizedWhereClause(ref _where, ref _parameters);
            }
        }

        internal string GetOrderByClause()
        {
            string ret = "";

            foreach (string s in order)
                ret += SqlTableName + s + ",";

            foreach (ECJoin j in joins)
            { 
                ECTable joinTable = (ECTable)j.Table;
                ret += joinTable.GetOrderByClause() + ",";
            }
            return ret.Substring(0, ret.Length - 1);
        }

        internal string ParseFilterString(string _fieldName, string _filter, ref Dictionary<string, string> _parameter)
        {
            string fieldName = "`" + _fieldName + "`";
            string[] val = { "", "" };
            int valId = 0;
            bool foundPoint = false;
            string clause = "("+fieldName;
            string operators = "<>=";
            for(int i=0; i<_filter.Length; i++)
            {
                switch (_filter[i])
                {
                    case '<':
                        if (!foundPoint)
                            clause += '<';
                        else
                        {
                            clause += ProcessFromToOperator(i, val[valId % 2], val[valId + 1 % 2],
                                                _filter[i - 1], ref _parameter);
                            foundPoint = false;
                            val[valId + 1 % 2] = "";
                        }
                        break;
                    case '>':
                        if (!foundPoint)
                            clause += '>';
                        else
                        {
                            clause += ProcessFromToOperator(i, val[valId % 2], val[valId + 1 % 2],
                                                _filter[i - 1], ref _parameter);
                            foundPoint = false;
                            val[valId + 1 % 2] = "";
                        }
                        break;
                    case '=':
                        if (!foundPoint)
                            clause += '=';
                        else
                        {
                            clause += ProcessFromToOperator(i, val[valId % 2], val[valId + 1 % 2],
                                                _filter[i - 1], ref _parameter);
                            foundPoint = false;
                            val[valId + 1 % 2] = "";
                        }
                        break;
                    case '|':
                        if (!foundPoint)
                        {
                            if (!operators.Contains(clause.Last()))
                                clause += "=";
                            clause += "@F" + TableName + i + " OR " + fieldName;
                            _parameter.Add("F" + TableName + i, val[valId % 2]);                            
                        }
                        else
                        {
                            clause += ProcessFromToOperator(i, val[valId % 2], val[valId + 1 % 2],
                                                _filter[i - 1], ref _parameter);
                            foundPoint = false;
                            val[valId + 1 % 2] = "";
                        }
                        val[valId % 2] = "";
                        break;
                    case '&':
                        if (!foundPoint)
                        {
                            if (!operators.Contains(clause.Last()))
                                clause += "=";
                            clause += "@F" + TableName + i + " AND " + fieldName;
                            _parameter.Add("F" + i, val[valId % 2]);
                        }
                        else
                        {
                            clause += ProcessFromToOperator(i, val[valId % 2], val[valId + 1 % 2],
                                                _filter[i - 1], ref _parameter);
                            foundPoint = false;
                            val[valId+1 % 2] = "";
                        }
                        val[valId % 2] = "";
                        break;
                    case '.':
                        if (foundPoint) //found second . => switch to second value storage
                            valId++;
                        else //found first . => remember for next loop (we're now in another State)
                            foundPoint = true;
                        break;
                    default:
                        val[valId % 2] += _filter[i];                        
                        break;
                }
            }

            if (foundPoint) // we're at the end of the line and still havent processed the .'s. That Means we have sth. like "1..5" or "1.." or "..5"
            {
                clause += ProcessFromToOperator(_filter.Length, val[valId % 2], val[(valId+1) % 2], 
                                                _filter[_filter.Length-1], ref _parameter);
            }
            else
            {
                if (!operators.Contains(clause.Last()))
                    clause += "=";
                clause += "@F"+ TableName + _filter.Length;
                _parameter.Add("F"+ TableName + _filter.Length, val[valId % 2]);
            }

            return clause+")";
        }

        private string ProcessFromToOperator(int id, string currVal, string lastVal, char lastChar, ref Dictionary<string, string> _parameter)
        {
            string clause="";

            if (lastVal == "" || currVal == "")
            {
                if (lastChar == '.') //case: "1.."
                { 
                    clause += ">=";
                    _parameter.Add("F"+ TableName + id, lastVal);
                }
                else //case: "..5"
                { 
                    clause += "<=";
                    _parameter.Add("F" + TableName + id, currVal);
                }
                clause += "@F" + id;                
            }
            else //case: "1..5"
            {
                clause += " BETWEEN ";
                clause += "@F" + TableName + (id - 1);
                clause += " AND ";
                clause += "@F" + TableName + id;

                _parameter.Add("F" + TableName + (id - 1), lastVal);
                _parameter.Add("F" + TableName + id, currVal);
            }

            return clause;
        }

        internal string MakeSelectFrom(bool _isRootTable=false)
        {
            string sqlTableName = "`" + TableName + "`.";
            string ret;
            if (_isRootTable)
                ret = "SELECT ";
            else
                ret = "";

            foreach (PropertyInfo p in GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
            {
                ret += sqlTableName + p.Name + " AS '" + TableName + "." + p.Name + "',";
            }            
            foreach(ECJoin j in joins)
            {
                ret += ((ECTable)j.Table).MakeSelectFrom()+",";
            }
            ret = ret.Substring(0, ret.Length - 1);

            if(_isRootTable)
                ret += " FROM "+ "`" + TableName + "`";

            return ret;
        }

        internal string MakeJoins()
        {
            string ret = "";

            foreach(ECJoin j in joins)
            {
                ECTable joinTable = ((ECTable)j.Table);
                switch (j.JoinType)
                {
                    case ECJoinType.Inner:
                        ret += " INNER JOIN ";
                        break;
                    case ECJoinType.LeftOuter:
                        ret += " LEFT OUTER JOIN ";
                        break;
                    case ECJoinType.RightOuter:
                        ret += " RIGHT OUTER JOIN ";
                        break;
                }
                if (j.OnTargetField != null)
                    ret += "`"+joinTable.TableName+"` ON "+"`"+joinTable.TableName+"`."+j.OnTargetField + "=`"+TableName+"`."+j.OnSourceField;
                else
                    ret += "`" + joinTable.TableName + "` ON " + "`" + joinTable.TableName + "`.RecId=`" + TableName + "`." + j.OnSourceField;
                ret += joinTable.MakeJoins();
            }

            return ret;
        }

        #endregion

        #region Interface
        public bool Equals(ECTable other)
        {
            return (globalRecId == other.globalRecId);
        }

        public IEnumerator GetEnumerator()
        {
            return this;
        }

        override
        public string ToString()
        {
            string ret = "";

            foreach (PropertyInfo p in this.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
                ret += p.Name + ": " + p.GetValue(this) + " \n";

            return ret.Substring(0, ret.Length - 2);
        }
        #endregion

        private void InvokeMethodeOnJoinedTables(string _method, object[] _params = null)
        {
            if (_params == null)
                _params = new object[] { };
            foreach (ECJoin j in joins)
            {
                ((ECTable)j.Table).GetType().GetMethod(_method).Invoke(j.Table, _params);
            }
        }
    }
}
