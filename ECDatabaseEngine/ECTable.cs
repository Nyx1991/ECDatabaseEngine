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
        
        private List<ECTable> records;
        private Dictionary<string, string> filter;
        private Dictionary<string, KeyValuePair<string, string>> ranges;
        private List<string> order;
        private List<ECJoin> joins;

        internal List<ECTable> Records => records;
        internal Dictionary<string, string> Filter => filter;
        internal Dictionary<string, KeyValuePair<string, string>> Ranges => ranges;
        internal List<string> Order => order;
        internal List<ECJoin> Joins => joins;


        /// <summary>
        /// Determines in which order the records should be loaded.
        /// Use AddOrderBy(_fieldName) to add fields you want the records to be orderd after
        /// </summary>
        public OrderType OrderType { get; set; }
        internal string SqlTableName { get => "`" + TableName + "`."; }
        
        int currRecIdx;
        int currRecIdxEnumerator;

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
        /// <summary>
        /// Invoked before the FindSet-Method has been called. Can be used to determine if the whole data in the table will be changed.
        /// </summary>
        public EventHandler<ECTable> OnBeforeFindSet;
        /// <summary>
        /// Invoked after the FindSet-Method has been called. Can be used to determine if the whole data in the table has changed.
        /// </summary>
        public EventHandler<ECTable> OnAfterFindSet;

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
            currRecIdxEnumerator = -1;
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
            currRecIdxEnumerator = -1;                        
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
        public T GetJoinedTable<T>()
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
        /// <param name="_invokeEvents">False: Events will not be invoked. Default: True</param>
        /// <returns>True if a record was found. False if no more record was found</returns>
        public bool Next(bool _invokeEvents = true)
        {
            bool ret = false;
            if (records.Count == 0)
                return ret;            
            
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
            
            CopyFrom(records[currRecIdx], false);            
            InvokeMethodeOnJoinedTables("Next");
            if (_invokeEvents)
            { 
                OnChanged?.Invoke(this, this);
            }
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
        /// Load records from database.
        /// </summary>
        /// <param name="_invokeEvents">False: Events will not be invoked. Default: True</param>
        public void FindSet(bool _invokeEvents = true)
        {   
            if (_invokeEvents)
            {
                OnBeforeFindSet?.Invoke(this, this);
            }

            InitTableDataFromDictionaryList(ECDatabaseConnection.Connection.GetData(this, filter, ranges, order));

            if (_invokeEvents)
            { 
                OnChanged?.Invoke(this, this);
                OnAfterFindSet?.Invoke(this, this);
            }
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
            records[currRecIdx].CopyFrom(this);
            OnAfterModify?.Invoke(this, this);
        }

        /// <summary>
        /// Writes all records in the buffer to the database
        /// </summary>
        public void ModifyAll()
        {            
            for (int i = 0; i < records.Count; i++)
            {
                ECDatabaseConnection.Connection.Modify(records[i]);
            }
            records[currRecIdx].CopyFrom(this);
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
            InvokeMethodeOnJoinedTables("SetCurrentBufferIndex", false, new object[] { _pos });
            OnChanged?.Invoke(this, this);
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

        #endregion

        private void InvokeMethodeOnJoinedTables(string _method, bool _fillNullParms = true, object[] _params = null)
        {
            if (_params == null && _fillNullParms)
                _params = new object[] { true };
            foreach (ECJoin j in joins)
            {
                ((ECTable)j.Table).GetType().GetMethod(_method).Invoke(j.Table, _params);
            }
        }

        #region Interface

        /// <summary>
        /// IEnumerable implementation.
        /// </summary>
        /// <returns>True: Some more records to come. False: No more records to come.</returns>
        public bool MoveNext()
        {
            bool ret = false;
            if (records.Count == 0)
                return ret;

            if (currRecIdxEnumerator >= records.Count - 1) //Here we stand at the last record
            {
                currRecIdxEnumerator = -1;
                ret = false;
            }
            else
            {
                currRecIdxEnumerator++;
                ret = true;
            }

            InvokeMethodeOnJoinedTables("MoveNext", false);
            return ret;
        }

        /// <summary>
        /// IEnumerator implementation. Returns the current record.
        /// </summary>
        public object Current
        {
            get
            {
                ECTable _dummy = (ECTable)Activator.CreateInstance(this.GetType());

                _dummy.CopyFrom(records[currRecIdxEnumerator], false);                               
                _dummy.joins = new List<ECJoin>(this.joins);

                foreach (ECJoin j in _dummy.joins)
                {
                    j.Table.CopyFrom(((ECTable)j.Table).GetType().GetProperty("Current").GetValue(j.Table));
                }

                return _dummy;
            }
            
        }

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
