﻿using System;
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
        private ECTable parent;
        private Guid guid;
        private int curRecIdx;
        private int curRecIdxEnumerator;

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
        /// <summary>
        /// True, if the table is part of a join and is not the parent table
        /// </summary>
        public bool IsJoined => (this.Parent != null);
        /// <summary>
        /// Returns the the table this table is joined into.
        /// Returns null if this table is not a part of a join
        /// </summary>
        public ECTable Parent { get { return parent; } private set { parent = value; } }
        internal string SqlTableName { get => "`" + TableName + "`"; }                

        #region EventHandler
        /// <summary>
        /// Invoked before a new record will be inserted an written to the database.
        /// </summary>
        public event EventHandler<ECTable> OnBeforeInsert;
        /// <summary>
        /// Invoked after a new record has been inserted and written to the database.
        /// </summary>
        public event EventHandler<ECTable> OnAfterInsert;
        /// <summary>
        /// Invoked before all changes on the current record will be written to the database.
        /// </summary>
        public event EventHandler<ECTable> OnBeforeModify;
        /// <summary>
        /// Invoked after all changes has been written to the database.
        /// </summary>
        public event EventHandler<ECTable> OnAfterModify;
        /// <summary>
        /// Invoked before the current record will be deleted from the database.
        /// </summary>
        public event EventHandler<ECTable> OnBeforeDelete;        
        /// <summary>
        /// Invoked after the current record has been deleted from the database.
        /// </summary>
        public event EventHandler<ECTable> OnAfterDelete;
        /// <summary>
        /// Invoked after a new record has been loaded.
        /// Can be used to keep UI up to date, for example.
        /// </summary>
        public event EventHandler<ECTable> OnChanged;
        /// <summary>
        /// Invoked before the FindSet-Method has been called. Can be used to determine if the whole data in the table will be changed.
        /// </summary>
        public event EventHandler<ECTable> OnBeforeFindSet;
        /// <summary>
        /// Invoked after the FindSet-Method has been called. Can be used to determine if the whole data in the table has changed.
        /// </summary>
        public event EventHandler<ECTable> OnAfterFindSet;

        #endregion

        /// <summary>
        /// Ongoing primary key of the table.
        /// This is a unique identifier (table scope).
        /// </summary>
        [ECTableField(FieldType.INT)]
        [ECNotNull]
        [ECPrimaryKey]
        [ECAutoIncrement]        
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

        #region Init/Reset/Clear/Add
        /// <summary>
        /// Removes all filters and ranges and unloads all loaded Records. Initializes all Fields.
        /// </summary>
        public void Init()
        {
            if (records != null)
                records.Clear();
            else
                records = new List<ECTable>();

            guid = Guid.NewGuid();
            curRecIdx = 0;
            curRecIdxEnumerator = -1;
            filter = new Dictionary<string, string>();
            ranges = new Dictionary<string, KeyValuePair<string, string>>();
            joins = new List<ECJoin>();
            order = new List<string>();
            OrderType = OrderType.ASC;
            foreach (PropertyInfo p in this.GetType().GetProperties().Where(x => x.IsDefined(typeof(ECTableFieldAttribute))))
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
            InvokeMethodeOnJoinedTables(nameof(Reset));
            curRecIdxEnumerator = -1;                        
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
        /// <param name="_foreignKey">Field that represents the foreign key.
        /// In the other table it will be the RecId be default. (Join someTable ON thisTable.ForeignKey=_table.RecId)</param>
        /// <param name="_joinType">INNER, LEFT OUTER or RIGHT OUTER</param>
        public void AddJoin(ECTable _table, string _foreignKey, ECJoinType _joinType)
        {
            if (GetType().GetProperties().Where(x => x.IsDefined(typeof(ECTableFieldAttribute)) && x.Name == _foreignKey).Count() == 0)
                throw new ECFieldNotFoundException("Field '" + _foreignKey + "' not found in table '" + TableName + "'");

            _table.Parent = this;

            ECJoin join = new ECJoin();
            join.JoinType = _joinType;
            join.OnSourceField = _foreignKey;
            join.Table = _table;
            joins.Add(join);
        }
        /// <summary>
        /// Join a table. You can only join a table once per type.
        /// </summary>
        /// <param name="_table">Instance of the table you want to join.</param>
        /// <param name="_foreignKey">Field that represents the foreign key</param>
        /// <param name="_onTargetField">The field on the other table to which the join should be connected to.
        /// (Join someTable ON thisTable.SourceField=_table.TargetField)</param>
        /// <param name="_joinType">INNER, LEFT OUTER or RIGHT OUTER</param>
        public void AddJoin(ECTable _table, string _foreignKey, string _onTargetField, ECJoinType _joinType)
        {
            if (GetType().GetProperties().Where(x => x.IsDefined(typeof(ECTableFieldAttribute)) && x.Name == _foreignKey).Count() == 0)
                throw new ECFieldNotFoundException("Field '" + _foreignKey + "' not found in table '" + TableName + "'");

            if (_table.GetType().GetProperties().Where(x => x.IsDefined(typeof(ECTableFieldAttribute)) && x.Name == _onTargetField).Count() == 0)
                throw new ECFieldNotFoundException("Field '" + _onTargetField + "' not found in table '" + _table.TableName + "'");

            _table.Parent = this;

            ECJoin join = new ECJoin();
            join.JoinType = _joinType;
            join.OnTargetField = _onTargetField;
            join.OnSourceField = _foreignKey;
            join.Table = _table;
            joins.Add(join);
        }

        
        /// <summary>
        /// True if the given table occures within this tables joined tables
        /// </summary>
        /// <param name="_table">The table that should be looked after in joined tables</param>
        /// <returns></returns>
        public bool IsTablePartOfJoinHierarchy(ECTable _table)
        {
            if (joins.Count == 0)
            {
                return this.Equals(_table);
            }
            else
            {
                foreach (ECJoin j in joins)
                {
                    return j.Table.IsTablePartOfJoinHierarchy(_table);
                }
            }
            return false;
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
            
            if (curRecIdx >= records.Count - 1) //Here we stand at the last record
            {
                curRecIdx = 0;
                ret = false;
            }
            else
            {
                curRecIdx++;
                ret = true;
            }
            
            CopyFrom(records[curRecIdx], false);            
            InvokeMethodeOnJoinedTables(nameof(Next));
            if (_invokeEvents)
            { 
                OnChanged?.Invoke(this, this);
            }
            return ret;
        }

        /// <summary>
        /// Get the next record
        /// </summary>
        /// <param name="_invokeEvents">False: Events will not be invoked. Default: True</param>
        /// <returns>True if a record was found. False if no more record was found</returns>
        public bool Previous(bool _invokeEvents = true)
        {
            bool ret = false;
            if (records.Count == 0)
                return ret;

            if (curRecIdx == 0) //Here we stand at the first record
            {
                curRecIdx = records.Count - 1;
                ret = false;
            }
            else
            {
                curRecIdx--;
                ret = true;
            }

            CopyFrom(records[curRecIdx], false);
            InvokeMethodeOnJoinedTables(nameof(Next));
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
                curRecIdx = records.Count - 1;
            }
            else
            {
                curRecIdx = 0;
            }            
            CopyFrom(records[curRecIdx], false);
            InvokeMethodeOnJoinedTables(nameof(Last));
            OnChanged?.Invoke(this, this);
        }

        /// <summary>
        /// Get the firs record
        /// </summary>
        public void First()
        {
            curRecIdx = 0;
            CopyFrom(records[curRecIdx], false);
            InvokeMethodeOnJoinedTables(nameof(First));
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
                curRecIdx = 0;
                Init();
            }
            else
            {
                if (curRecIdx == records.Count - 1)
                {
                    curRecIdx--;
                }
                recIdx = curRecIdx;
                ECDatabaseConnection.Connection.Delete(this);
                FindSet();
                SetCurentBufferIndex(recIdx);
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
            if (this.RecId == -1)
            {
                this.Insert();
            }
            else
            {
                OnBeforeModify?.Invoke(this, this);
                ECDatabaseConnection.Connection.Modify(this);
                if (records.Count > 0)
                { 
                    records[curRecIdx].CopyFrom(this);
                }
                OnAfterModify?.Invoke(this, this);
            }            
        }

        /// <summary>
        /// Writes all records in the buffer to the database
        /// </summary>
        public void ModifyAll()
        {            
            records[curRecIdx].CopyFrom(this);
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].RecId == -1)
                {
                    this.Insert();
                }
                else
                {
                    records[i].Modify();
                    //ECDatabaseConnection.Connection.Modify(records[i]);
                }
        }            
        }

        /// <summary>
        /// Returns the position of the current record in the loaded dataset
        /// </summary>
        /// <returns>Index of the current record</returns>
        public int GetCurentBufferIndex()
        {
            return curRecIdx;
        }

        /// <summary>
        /// Loads the record at the given position in the dataset
        /// </summary>
        /// <param name="_pos">Index of the record</param>
        public void SetCurentBufferIndex(int _pos)
        {
            if (records.Count == 0)
            {
                curRecIdx = 0;
                Init();
                return;
            }
            
            if (_pos > records.Count - 1)
                throw new IndexOutOfRangeException();

            records[curRecIdx].CopyFrom(this, false);
            curRecIdx = _pos;

            CopyFrom(records[curRecIdx], false);
            InvokeMethodeOnJoinedTables(nameof(SetCurentBufferIndex), false, new object[] { _pos });
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
            foreach (PropertyInfo sourcePi in _table.GetType().GetProperties().Where(x => x.IsDefined(typeof(ECTableFieldAttribute))))
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
            ECTableFieldAttribute tfa = this.GetType().GetCustomAttribute<ECTableFieldAttribute>();
            tfa = _p.GetCustomAttribute<ECTableFieldAttribute>();
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
            else if (tfa.type == FieldType.BOOLEAN)
            { 
                sql += "'" + _p.GetValue(this) + "'";
            }
            else
                throw new NotImplementedException();

            return sql;
        }

        internal void ConvertAndStore(PropertyInfo _p, string value)
        {
            ECTableFieldAttribute tfa = _p.GetCustomAttribute<ECTableFieldAttribute>();
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
                curRecIdx = 0;
                CopyFrom(records.First(), false);
            }
        }

        #endregion        

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

            if (curRecIdxEnumerator >= records.Count - 1) //Here we stand at the last record
            {
                curRecIdxEnumerator = -1;
                ret = false;
            }
            else
            {
                curRecIdxEnumerator++;
                ret = true;
            }

            InvokeMethodeOnJoinedTables(nameof(MoveNext), false);
            return ret;
        }

        /// <summary>
        /// IEnumerator implementation. Returns the current record.
        /// </summary>
        public object Current
        {
            get
            {
                ECTable dummy = (ECTable)Activator.CreateInstance(this.GetType());

                dummy.CopyFrom(records[curRecIdxEnumerator], false);
                dummy.joins = new List<ECJoin>();

                foreach (ECJoin j in this.joins)
                {
                    ECJoin dummyJoin = (ECJoin)Activator.CreateInstance(typeof(ECJoin));

                    dummyJoin.Table = (ECTable)Activator.CreateInstance(j.Table.GetType());
                    dummyJoin.JoinType = j.JoinType;
                    dummyJoin.OnSourceField = j.OnSourceField;
                    dummyJoin.OnTargetField = j.OnTargetField;                    
                    dummyJoin.Table.CopyFrom(((ECTable)j.Table).GetType().GetProperty("Current").GetValue(j.Table));

                    dummy.joins.Add(dummyJoin);
                }                

                return dummy;
            }

        }

        /// <summary>
        /// IEquatable implementation.
        /// </summary>
        /// <param name="_other">Table to which this table should be compared to.</param>
        /// <returns>True: if both RecIds are the same. False: If not so.</returns>
        public bool Equals(ECTable _other)
        {
            return _other.guid.Equals(this.guid);
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

            foreach (PropertyInfo p in this.GetType().GetProperties().Where(x => x.IsDefined(typeof(ECTableFieldAttribute))))
                ret += p.Name + ": " + p.GetValue(this) + " \n";

            return ret.Substring(0, ret.Length - 2);
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

    }   
}
