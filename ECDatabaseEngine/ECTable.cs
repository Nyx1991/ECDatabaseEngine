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
        
        int currentRecord;

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
        /// Removes all filters and ranges
        /// </summary>
        public void Clear()
        {
            filter.Clear();
            ranges.Clear();
        }

        /// <summary>
        /// IEnumerator implementation: Set current Position before first item
        /// </summary>
        public void Reset()
        {
            currentRecord = -1;            
        }
        #endregion

        #region GetData

        public bool Next()
        {
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
            return ret;
        }

        public bool MoveNext()
        {
            return Next();
        }        

        public void FindSet()
        {            
            InitTableDataFromDictionaryList(ECDatabaseConnection.Connection.GetData(this, filter, ranges));
        }     
        
        #endregion

        #region Filter Data
        public void Get(int _recId)
        {
            filter = new Dictionary<string, string>();
            filter.Add("RecId", _recId.ToString());
            InitTableDataFromDictionaryList(ECDatabaseConnection.Connection.GetData(this, filter,
                                                                                    new Dictionary<string, KeyValuePair<string, string>>()));
        }

        public void SetFilter(string _fieldName, string _filterString="")
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

        public void SetRange(string _fieldName, string _from = "", string _to="")
        {
            if (GetType().GetProperty(_fieldName) == null)
                new Exception(String.Format("Field with name '{0}' does not exist in {1}", _fieldName, GetType().Name));

            if (ranges.Keys.Contains(_fieldName))
                if (_from.Equals("") && _to.Equals(""))
                    ranges.Remove(_fieldName);
                else
                    ranges[_fieldName] = new KeyValuePair<string,string>(_from, _to);
            else
                ranges.Add(_fieldName, new KeyValuePair<string, string>(_from, _to));
        }
        #endregion

        #region Database operations
        public void Insert()
        { 
            RecId = ECDatabaseConnection.Connection.Insert(this);
            Get(RecId);
        }

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
                currentRecord = records.IndexOf(this);
                records.Remove(this);
                if (records.Count == 0)
                { 
                    currentRecord = 0;
                    Init();
                }
                else
                { 
                    currentRecord = currentRecord % records.Count;            
                    CopyFrom(records[currentRecord]);
                }
            }
        }

        public void Modify()
        {
            ECDatabaseConnection.Connection.Modify(this);
            records[currentRecord] = this;
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
            if (tfa.type == FieldType.VARCHAR || tfa.type == FieldType.CHAR)
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
                new NotImplementedException();

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
                    if(value == "")
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

                default:
                    throw new NotImplementedException();
            }                        
        }                

        internal void InitRecordFromDictionary(Dictionary<string, string> _values)
        {
            Type t = this.GetType();
            foreach (KeyValuePair<string, string> kv in _values)
                ConvertAndStore(t.GetProperty(kv.Key), kv.Value);
        }

        internal void InitTableDataFromDictionaryList(List<Dictionary<string, string>> _dataDict)
        {
            Init();
            ECTable table;
            foreach (Dictionary<string, string> d in _dataDict)
            {
                table = (ECTable)Activator.CreateInstance(this.GetType());
                table.InitRecordFromDictionary(d);
                records.Add(table);
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
        
        internal void GetParameterizedWherClause(ref List<string> _where, ref Dictionary<string, string> _parameters)
        {
            _where.Clear();
            _parameters.Clear();
            
            foreach (KeyValuePair<string, KeyValuePair<string, string>> kp in ranges)
                if (kp.Value.Value.Equals(""))
                {                    
                    _parameters.Add(kp.Key, kp.Value.Key);
                    _where.Add(kp.Key + "= @" + kp.Key);                    
                }
                else
                {
                    _parameters.Add("K" + kp.Key, kp.Value.Key);
                    _parameters.Add("V" + kp.Key, kp.Value.Value);
                    _where.Add("(" + kp.Key + " BETWEEN @K" + kp.Key + " AND @V" + kp.Key + ")");
                }

            foreach (KeyValuePair<string, string> kp in filter)
            {
                _where.Add(ParseFilterString(kp.Key, kp.Value, ref _parameters));
            }
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
                            clause += '<';
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
                            clause += "@F" + i + " OR " + fieldName;
                            _parameter.Add("F" + i, val[valId % 2]);                            
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
                            clause += "@F" + i + " AND " + fieldName;
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
                clause += "@F" + _filter.Length;
                _parameter.Add("F" + _filter.Length, val[valId % 2]);
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
                    _parameter.Add("F" + id, lastVal);
                }
                else //case: "..5"
                { 
                    clause += "<=";
                    _parameter.Add("F" + id, currVal);
                }
                clause += "@F" + id;                
            }
            else //case: "1..5"
            {
                clause += " BETWEEN ";
                clause += "@F" + (id - 1);
                clause += " AND ";
                clause += "@F" + id;

                _parameter.Add("F" + (id - 1), lastVal);
                _parameter.Add("F" + id, currVal);
            }

            return clause;
        }
      
        #endregion

        #region Interface
        public bool Equals(ECTable other)
        {
            PropertyInfo targetPi;
            foreach (PropertyInfo sourcePi in other.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
                if ((targetPi = GetType().GetProperty(sourcePi.Name)) != null)
                {
                    if (!targetPi.GetValue(this).Equals(sourcePi.GetValue(other)))                     
                        return false;
                }
                else
                    return false;

            return true;
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
    }
}
