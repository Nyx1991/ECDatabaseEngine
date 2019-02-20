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
    /// <summary>
    /// Abstract table class. Use this class as a base for your tables.
    /// </summary>
    public abstract class ECTable : IEnumerator, IEnumerable, IEquatable<ECTable>
    {
        /// <summary>
        /// Internal list which is used to store all loaded records.
        /// The records are stored as ECTable instances itself.
        /// </summary>
        protected List<ECTable> records;
        private Dictionary<string, string> filter;
        private Dictionary<string, KeyValuePair<string, string>> ranges;
        private List<string> order;
        private List<ECJoin> joins;

        /// <summary>
        /// Determines in which order the records should be loaded.
        /// Use AddOrderBy(_fieldName) to add fields you want the records to be orderd after
        /// </summary>
        public OrderType OrderType { get; set; }
        private string SqlTableName { get => "`" + TableName + "`."; }
        
        int currRecIdx;

        #region EventHandler
        /// <summary>
        /// Invoked before a new record will be inserted an written to the database.
        /// </summary>
        public EventHandler<ECTable> OnBeforeInsert;
        /// <summary>
        /// Invoked after a new record has been inserted and written to the database.
        /// </summary>
        public EventHandler<ECTable> OnAfterInsert;
        /// <summary>
        /// Invoked before all changes on the current record will be written to the database.
        /// </summary>
        public EventHandler<ECTable> OnBeforeModify;
        /// <summary>
        /// Invoked after all changes has been written to the database.
        /// </summary>
        public EventHandler<ECTable> OnAfterModify;
        /// <summary>
        /// Invoked before the current record will be deleted from the database.
        /// </summary>
        public EventHandler<ECTable> OnBeforeDelete;        
        /// <summary>
        /// Invoked after the current record has been deleted from the database.
        /// </summary>
        public EventHandler<ECTable> OnAfterDelete;
        /// <summary>
        /// Invoked after a new record has been loaded.
        /// Can be used to keep UI up to date, for example.
        /// </summary>
        public EventHandler<ECTable> OnChanged;
        #endregion

        /// <summary>
        /// Ongoing primary key of the table.
        /// This is a unique identifier (table scope).
        /// </summary>
        [TableField(FieldType.INT)]
        [NotNull]
        [PrimaryKey]
        [AutoIncrement]        
        public int RecId { get; internal set; }
        /// <summary>
        /// IEnumerator implementation. Returns the current record.
        /// </summary>
        public object Current => records[currRecIdx];
        /// <summary>
        /// Record count.
        /// </summary>
        public int Count => records.Count;
        /// <summary>
        /// Return the name of the table.
        /// </summary>
        public String TableName { get { return this.GetType().Name; } }
        /// <summary>
        /// Initializes a new ECTable instance.
        /// </summary>
        public ECTable()
        {
            Init();            
        }

        internal ECTable(Dictionary<string, string> _values) : this()
        {
            InitRecordFromDictionary(_values);
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

            currRecIdx = 0;
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
            OnChanged?.Invoke(this, this);
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
        /// IEnumerator implementation: Set current Position to first record
        /// </summary>
        public void Reset()
        {
            InvokeMethodeOnJoinedTables("Reset");
            currRecIdx = 0;
            CopyFrom(records[0], false);
            OnChanged?.Invoke(this, this);
        }

        /// <summary>
        /// Clears the internal record-list.
        /// In other words: unload all records.
        /// </summary>
        protected void DeleteRecords()
        {
            if (records != null)
            {
                records.Clear();
            }
        }
        #endregion

        #region Joins
        /// <summary>
        /// Get the instance of a table you Joined before.
        /// Make sure to use a subclass of ECTable.
        /// </summary>
        /// <typeparam name="T">Type of the joined table (ECTable subclass)</typeparam>
        /// <returns>Instance of the joined table with all records.</returns>
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
        /// <summary>
        /// Join a table. You can only join a table once per type.
        /// </summary>
        /// <param name="_table">Instance of the table you want to join.</param>
        /// <param name="_onSourceField">The field on this table to which the join should be connected to.
        /// In the other table it will be the RecId. (Join someTable ON thisTable.SourceField=_table.RecId)</param>
        /// <param name="_joinType">INNER, LEFT OUTER or RIGHT OUTER</param>
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
        /// <summary>
        /// Join a table. You can only join a table once per type.
        /// </summary>
        /// <param name="_table">Instance of the table you want to join.</param>
        /// <param name="_onSourceField">The field on this table to which the join should be connected to.</param>
        /// <param name="_onTargetField">The field on the other table to which the join should be connected to.
        /// In the other table it will be the RecId. (Join someTable ON thisTable.SourceField=_table.TargetField)</param>
        /// <param name="_joinType">INNER, LEFT OUTER or RIGHT OUTER</param>
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
            bool ret = false;
            if (records.Count == 0)
                return ret;

            records[currRecIdx].CopyFrom(this, false); //save the data to the buffer before getting next record
            
            if (currRecIdx >= records.Count - 1) //Here we stand at the last record
            {
                currRecIdx = 0;
                ret = false;
            }
            else
            {
                currRecIdx++;
                ret = true;
            }
            records[currRecIdx].CopyFrom(this, false); //save the data to the buffer before getting next record
            CopyFrom(records[currRecIdx], false);            
            InvokeMethodeOnJoinedTables("Next");
            OnChanged?.Invoke(this, this);
            return ret;
        }

        /// <summary>
        /// Get the last record
        /// </summary>
        public void Last()
        {
            if (records.Count > 0)
            {
                currRecIdx = records.Count - 1;
            }
            else
            {
                currRecIdx = 0;
            }
            records[currRecIdx].CopyFrom(this, false); //save the data to the buffer before getting next record
            CopyFrom(records[currRecIdx], false);
            InvokeMethodeOnJoinedTables("Last");
            OnChanged?.Invoke(this, this);
        }

        /// <summary>
        /// Get the firs record
        /// </summary>
        public void First()
        {
            currRecIdx = 0;
            CopyFrom(records[currRecIdx], false);
            InvokeMethodeOnJoinedTables("First");
            OnChanged?.Invoke(this, this);
        }

        /// <summary>
        /// IEnumerable implementation.
        /// </summary>
        /// <returns>True: Some more records to come. False: No more records to come.</returns>
        public bool MoveNext()
        {
            return Next();
        }

        /// <summary>
        /// Load records from database.
        /// </summary>
        public void FindSet(bool _invokeEvent = true)
        {            
            InitTableDataFromDictionaryList(ECDatabaseConnection.Connection.GetData(this, filter, ranges, order));
            if (_invokeEvent)
                OnChanged?.Invoke(this, this);
        }

        #endregion

        #region Filter Data and order
        /// <summary>
        /// Get a specific record from the database by its RecId
        /// </summary>
        /// <param name="_recId">RecId of the record</param>
        public void Get(int _recId)
        {
            Init();
            filter = new Dictionary<string, string>();
            filter.Add("RecId", _recId.ToString());
            InitTableDataFromDictionaryList(ECDatabaseConnection.Connection.GetData(this, filter,
                                                                                    new Dictionary<string,
                                                                                    KeyValuePair<string, string>>(),
                                                                                    order));
            OnChanged?.Invoke(this, this);
        }
        /// <summary>
        /// Add a filterstring on a field.          
        /// </summary>
        /// <param name="_fieldName">Field the filter should be applied to.</param>
        /// <param name="_filterString">Filter string. Leave empty to remove the filter.</param>
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
        /// <summary>
        /// Add a range on a field
        /// </summary>
        /// <param name="_fieldName">Field the range should be applied to.</param>
        /// <param name="_from">From value. Leave empty to remove the range.</param>
        /// <param name="_to">To value</param>
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
        /// <summary>
        /// Add field you want your result to be orderd after.
        /// </summary>
        /// <param name="_fieldName"></param>
        public void AddOrderBy(string _fieldName)
        {
            order.Add(_fieldName);
        }

        #endregion

        #region Database operations
        /// <summary>
        /// Insert a new record to the database. Make sure to fill it before.
        /// </summary>
        public void Insert()
        {
            OnBeforeInsert?.Invoke(this, this);
            RecId = ECDatabaseConnection.Connection.Insert(this);
            Get(RecId);            
            OnAfterInsert?.Invoke(this, this);
        }

        /// <summary>
        /// Deletes the current record from the database.
        /// Has no impact on joined tables.
        /// </summary>
        public void Delete()
        {
            int recIdx;

            OnBeforeDelete?.Invoke(this, this);
            if (records.Count == 0)
            {
                currRecIdx = 0;
                Init();
            }
            else
            {
                if (currRecIdx == records.Count - 1)
                {
                    currRecIdx--;
                }
                recIdx = currRecIdx;
                ECDatabaseConnection.Connection.Delete(this);
                FindSet();
                SetCurrentBufferIndex(recIdx);
                OnChanged?.Invoke(this, this);
            }
            OnAfterDelete?.Invoke(this, this);           
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
            OnChanged?.Invoke(this, this);
        }     

        /// <summary>
        /// Writes the current record to the database.
        /// Has no impact on joined tables.
        /// </summary>
        public void Modify()
        {
            OnBeforeModify?.Invoke(this, this);
            ECDatabaseConnection.Connection.Modify(this);
            records[currRecIdx] = this;
            OnAfterModify?.Invoke(this, this);
        }

        /// <summary>
        /// Returns the position of the current record in the loaded dataset
        /// </summary>
        /// <returns>Index of the current record</returns>
        public int GetCurrentBufferIndex()
        {
            return currRecIdx;
        }

        /// <summary>
        /// Loads the record at the given position in the dataset
        /// </summary>
        /// <param name="_pos">Index of the record</param>
        public void SetCurrentBufferIndex(int _pos)
        {
            if (records.Count == 0)
            {
                currRecIdx = 0;
                Init();
                return;
            }
            
            if (_pos > records.Count - 1)
                throw new IndexOutOfRangeException();

            records[currRecIdx].CopyFrom(this, false);
            currRecIdx = _pos;

            CopyFrom(records[currRecIdx], false);
            InvokeMethodeOnJoinedTables("SetCurrentBufferIndex", new object[] { _pos });
            OnChanged?.Invoke(this, this);
        }

        /// <summary>
        /// Writes all records to the database
        /// </summary>
        public void ModifyAll()
        {
            int recIdx = currRecIdx;
            SetCurrentBufferIndex(0);
            do
            {
                Modify();
            } while (Next());

            FindSet();

            if (records.Count == 0)
            { 
                SetCurrentBufferIndex(0);
                return;
            }

            if (recIdx > records.Count - 1)
            {
                recIdx = records.Count - 1;
            }
            SetCurrentBufferIndex(recIdx);
        }

        /// <summary>
        /// Synchronizes the table schema to the database.
        /// Important: This can lead to data loss.
        /// </summary>
        public void SynchronizeSchema()
        {
            ECDatabaseConnection.SynchronizeSchema(this);
        }
        #endregion

        #region Data manipulation: Copy and convert

        /// <summary>
        /// Copy the content of all fields from the currently active record of a given tabel
        /// to the currently active record of this table
        /// </summary>
        /// <param name="_table">Table from which the data should be copied</param>
        /// <param name="_invokeOnChangeEvent">True: OnChange Event will be invoked if function is called. False: OnChange Event will not be invoked</param>
        public void CopyFrom(ECTable _table, bool _invokeOnChangeEvent = true)
        {
            PropertyInfo targetPi;
            foreach (PropertyInfo sourcePi in _table.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
                if ((targetPi = GetType().GetProperty(sourcePi.Name)) != null)
                    targetPi.SetValue(this, sourcePi.GetValue(_table));

            if (_invokeOnChangeEvent)
                OnChanged?.Invoke(this, this);
        }

        /// <summary>
        /// Convert a DateTime variable into the SQL date notation.
        /// </summary>
        /// <param name="_dt">DateTime to be converted.></param>
        /// <returns>String with date in SQL format</returns>
        public string DateTimeToSqlDate(DateTime _dt)
        {
            return String.Format("{0}-{1}-{2}", _dt.Year, _dt.Month, _dt.Day);
        }
        /// <summary>
        /// Convert a DateTime variable into the SQL date-time notation.
        /// </summary>
        /// <param name="_dt">DateTime to be converted.></param>
        /// <returns>String with date-time in SQL format</returns>
        public string DateTimeToSqlDateTime(DateTime _dt)
        {
            return String.Format("{0}-{1}-{2} {3}:{4}:{5}.{6}", _dt.Year, _dt.Month, _dt.Day, _dt.Hour, _dt.Minute, _dt.Second, _dt.Millisecond);
        }
        #endregion

        #region Database helper functions        

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
                records.Add(table);                
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
                currRecIdx = 0;
                CopyFrom(records.First(), false);
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
            if (ret.Length > 0)
                return ret.Substring(0, ret.Length - 1);
            else
                return ret;
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

        private void InvokeMethodeOnJoinedTables(string _method, object[] _params = null)
        {
            if (_params == null)
                _params = new object[] { };
            foreach (ECJoin j in joins)
            {
                ((ECTable)j.Table).GetType().GetMethod(_method).Invoke(j.Table, _params);
            }
        }

        #region Interface
        /// <summary>
        /// IEquatable implementation.
        /// </summary>
        /// <param name="_other">Table to which this table should be compared to.</param>
        /// <returns>True: if both RecIds are the same. False: If not so.</returns>
        public bool Equals(ECTable _other)
        {            
            if (this.GetType() != _other.GetType())
                return false;

            foreach (PropertyInfo p in this.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
            {
                if (!_other.GetType().GetProperties().Contains(p))
                    return false;

                Console.WriteLine(p.GetValue(this));
                Console.WriteLine(p.GetValue(_other));

                if (!p.GetValue(this).Equals(p.GetValue(_other)))
                    return false;
                
            }

            return true;
        }
        /// <summary>
        /// IEnumerator implementation.
        /// </summary>
        /// <returns>IEnumerator</returns>
        public IEnumerator GetEnumerator()
        {
            return this;
        }
        /// <summary>
        /// Return the content of the current record as string.
        /// </summary>
        /// <returns>Contents of all fields.</returns>
        override
        public string ToString()
        {
            string ret = "";

            foreach (PropertyInfo p in this.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
                ret += p.Name + ": " + p.GetValue(this) + " \n";

            return ret.Substring(0, ret.Length - 2);
        }
        #endregion
        
    }
}
