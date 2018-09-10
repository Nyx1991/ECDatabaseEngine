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



        public ECTable()
        {
            Init();
        }       

        public void SynchronizeSchema()
        {
            ECDatabaseConnection.SynchronizeSchema(this);            
        }

        public void Init()
        {
            if (records != null)
                records.Clear();
            else
                records = new List<ECTable>();
            currentRecord = 0;            
            filter = new Dictionary<string, string>();
            ranges = new Dictionary<string, KeyValuePair<string, string>>();
            Clear();
        }

        public void Clear()
        {
            filter.Clear();
            ranges.Clear();
            foreach (PropertyInfo p in this.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
            {
                Type type = p.PropertyType;
                if (type != typeof(string))
                    p.SetValue(this, Activator.CreateInstance(p.PropertyType));
                else
                    p.SetValue(this, "");
            }
        }

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

        public void Get(int _recId)
        {            
            filter = new Dictionary<string, string>();
            filter.Add("RecId", _recId.ToString());
            LoadTableDataFromDictionaryList(ECDatabaseConnection.Connection.GetData(this, filter, 
                                                                                    new Dictionary<string, KeyValuePair<string, string>>()));
        }

        public void FindSet()
        {            
            LoadTableDataFromDictionaryList(ECDatabaseConnection.Connection.GetData(this, filter, ranges));
        }     

        internal void LoadTableDataFromDictionaryList(List<Dictionary<string,string>> _dataDict)
        {
            Init();            
            ECTable table;
            foreach (Dictionary<string, string> d in _dataDict)
            {
                table = (ECTable)Activator.CreateInstance(this.GetType());
                table.InitFromDictionary(d);
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

        public void Insert()
        { 
            RecId = ECDatabaseConnection.Connection.Insert(this);
            FindSet();
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
                    _p.SetValue(this, Convert.ToBoolean(value));
                    break;

                case FieldType.CHAR:
                    _p.SetValue(this, Convert.ToChar(value));
                    break;

                case FieldType.DATE:
                    date = value.Split('-');
                    _p.SetValue(this, new DateTime(Convert.ToInt32(date[0]), Convert.ToInt32(date[1]), Convert.ToInt32(date[2])));
                    break;

                case FieldType.DATETIME:
                    date = value.Split(' ')[0].Split('-');
                    time = value.Split(' ')[1].Split(':');
                    _p.SetValue(this, new DateTime(Convert.ToInt32(date[0]), Convert.ToInt32(date[1]), Convert.ToInt32(date[2]),
                                                   Convert.ToInt32(time[0]), Convert.ToInt32(time[1]), Convert.ToInt32(time[2].Split('.')[0]),
                                                   Convert.ToInt32(time[2].Split('.')[1])));
                    break;

                case FieldType.DECIMAL:
                    _p.SetValue(this, Convert.ToDecimal(value));
                    break;

                case FieldType.DOUBLE:
                    _p.SetValue(this, Convert.ToDouble(value));
                    break;

                case FieldType.FLOAT:
                    _p.SetValue(this, (float)Convert.ToDouble(value));
                    break;

                case FieldType.INT:
                    _p.SetValue(this, Convert.ToInt32(value));
                    break;

                case FieldType.VARCHAR:
                    _p.SetValue(this, value);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        internal ECTable(Dictionary<string, string> _values) : this()
        {
            InitFromDictionary(_values);
        }

        internal void InitFromDictionary(Dictionary<string, string> _values)
        {
            Type t = this.GetType();
            foreach (KeyValuePair<string, string> kv in _values)
                ConvertAndStore(t.GetProperty(kv.Key), kv.Value);
        }



        public bool MoveNext()
        {
            return Next();
        }

        public void Reset()
        {
            currentRecord = 0;
            if (records.Count == 0)
            {
                Init();
            }
            else
            {
                CopyFrom(records[currentRecord]);
            }
        }

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

        
    }
}
