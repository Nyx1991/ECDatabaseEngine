using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ECDatabaseEngine;
using System.Reflection;

namespace ECDatabaseWinFormsExtension
{
    public enum FieldFilter { None, HideGivenFields, ShowGivenFields }

    public static class DataGridViewExtension
    {
        private static List<DataGridView> dataGridViewsWithECTableBinding;
        const string mappingColNameConst = "Buffer Idx";

        static DataGridViewExtension()
        {
            dataGridViewsWithECTableBinding = new List<DataGridView>();
        }

        /// <summary>
        /// Removes all lines from the DataGridView and loads new ones based on the given table
        /// </summary>
        /// <param name="_dgv">DataGridView itself</param>
        /// <param name="_table">ECTable instance from which the data should be loaded</param>        
        public static void AddDataFromECTable(this DataGridView _dgv, ECTable _table)
        {
            AddDataFromECTable(_dgv, _table, FieldFilter.None, false, null);
        }

        /// <summary>
        /// Removes all lines from the DataGridView and loads new ones based on the given table
        /// </summary>
        /// <param name="_dgv">DataGridView itself</param>
        /// <param name="_table">ECTable instance from which the data should be loaded</param>
        /// <param name="_ff">FieldFilter: Should the fields listed in the parameters should be in- or excluded from the view?</param>
        /// <param name="_fields">List of fields that should be in- or excluded (determined by the given FieldFilter) from the view</param>
        public static void AddDataFromECTable(this DataGridView _dgv, ECTable _table,
                                           FieldFilter _ff,
                                           params string[] _fields)
        {
            AddDataFromECTable(_dgv, _table, _ff, false, _fields);
        }

        private static void AddDataFromECTable(this DataGridView _dgv, ECTable _table,
                                           FieldFilter _ff,
                                           bool _writeBufferIdxMapping,
                                           params string[] _fields)
        {
            string mappingColName = $"{ _table.TableName }_{mappingColNameConst}";
            DataGridViewRow row;
            DataGridViewColumn mappingCol;

            _dgv.Columns.Clear();
            foreach (PropertyInfo p in _table.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
            {
                _dgv.Columns.Add(String.Format("{0}_{1}", _table.TableName, p.Name), p.Name);
            }

            //Add Column for BufferIdx- <=> RowIdx-Mapping            
            mappingCol = _dgv.Columns[_dgv.Columns.Add(mappingColName, "")];
            mappingCol.Visible = false;

            if (_table.Count == 0)
                _table.FindSet(false);

            int i = 0;
            foreach (ECTable t in _table)
            {
                row = new DataGridViewRow();
                row.CreateCells(_dgv);
                List<object> data = new List<object>();
                foreach (PropertyInfo p in t.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
                {
                    string colName = String.Format("{0}_{1}", t.TableName, p.Name);
                    row.Cells[_dgv.Columns[colName].Index].Value = p.GetValue(t);
                }

                int index = _dgv.Rows.Add(row);

                if (_writeBufferIdxMapping)
                {
                    _dgv.Rows[index].Cells[mappingColName].Value = i;
                    i++;
                }                
            }

            if (_ff == FieldFilter.HideGivenFields)
            {
                foreach (string s in _fields)
                {
                    DataGridViewColumn c = _dgv.Columns[String.Format("{0}_{1}", _table.TableName, s)];
                    if (c != null)
                    {
                        c.Visible = false;
                    }
                }
            }
            if (_ff == FieldFilter.ShowGivenFields)
            {
                foreach (DataGridViewColumn c in _dgv.Columns)
                {
                    string fieldName = c.Name.Replace(String.Format("{0}_", _table.TableName), "");
                    if (!_fields.Contains(fieldName))
                    {
                        c.Visible = false;
                    }
                }
            }

        }

        /// <summary>
        /// Based on the Cell-/Row-/Column-Selection of the DataGridView, the given ECTable will be filtered to these records
        /// </summary>
        /// <param name="_dgv">DataGridView itself</param>
        /// <param name="_table">ECTable instance which will be filtered</param>
        public static void GetSelectedRecords(this DataGridView _dgv, ECTable _table)
        {
            string filter = "";

            foreach (DataGridViewRow r in _dgv.SelectedRows)
            {
                string cellName = String.Format("{0}_{1}", _table.TableName, nameof(_table.RecId));
                object recId = r.Cells[cellName].Value;
                filter += r.Cells[cellName].Value + "|";
            }
            if (filter != "")
            {
                _table.SetFilter(nameof(_table.RecId), filter.Substring(0, filter.Length - 1));
                _table.FindSet();
            }
            else
            {
                _table.Init();
            }
        }

        public static void SetECTableDataBinding(this DataGridView _dgv, ECTable _table)
        {
            SetECTableDataBinding(_dgv, _table, FieldFilter.None, null);
        }

        public static void SetECTableDataBinding(this DataGridView _dgv, ECTable _table,
                                             FieldFilter _ff,
                                             params string[] _fields)
        {
            string mappingColName = $"{ _table.TableName }_{mappingColNameConst}";

            if (dataGridViewsWithECTableBinding.Contains(_dgv))
            {
                throw new ECBindingAlreadyExistsException(_dgv);
            }
            dataGridViewsWithECTableBinding.Add(_dgv);
            _dgv.AddDataFromECTable(_table, _ff, true, _fields);

            _table.OnBeforeFindSet += delegate (object sender, ECTable _callerTable)
            {                
                _dgv.Rows.Clear();
                _dgv.Columns.Clear();
                _dgv.Refresh();
            };

            _table.OnAfterFindSet += delegate (object sender, ECTable _callerTable)
            {
                _dgv.AddDataFromECTable(_callerTable, _ff, true, _fields);
            };

            _dgv.CellEnter += delegate (object sender, DataGridViewCellEventArgs e)
            {
                _dgv.Rows[e.RowIndex].Selected = _dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected;                
                if ((!_dgv.AllowUserToAddRows) || (_dgv.AllowUserToAddRows && e.RowIndex < _dgv.Rows.Count - 1))
                   _table.SetCurrentBufferIndex(Convert.ToInt32(_dgv.Rows[e.RowIndex].Cells[mappingColName].Value));
                
            };

            _dgv.CellEndEdit += delegate (object sender, DataGridViewCellEventArgs e)
            {
                PropertyInfo p;
                string colName = _dgv.Columns[e.ColumnIndex].Name;
                string fieldName = colName.Substring(colName.IndexOf('_') + 1);

                //_table.SetCurrentBufferIndex(Convert.ToInt32(_dgv.Rows[e.RowIndex].Cells[mappingColName].Value));
                p = _table.GetType().GetProperty(fieldName);
                p.SetValue(_table, _dgv.Rows[e.RowIndex].Cells[e.ColumnIndex].Value);
            };

            _dgv.Disposed += delegate (object sender, EventArgs a)
            {
                dataGridViewsWithECTableBinding.Remove(_dgv);
            };
        }

    }
}
