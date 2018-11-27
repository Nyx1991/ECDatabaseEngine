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
        public static void LoadFromECTable(this DataGridView _dgv, ECTable _table)
        {
            LoadFromECTable(_dgv, _table, FieldFilter.None, null);
        }

        public static void LoadFromECTable(this DataGridView _dgv, ECTable _table, 
                                            FieldFilter _ff, 
                                            params string[] _fields)
        {
            DataGridViewRow row;
            _dgv.Columns.Clear();
            foreach (PropertyInfo p in _table.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
            {
                _dgv.Columns.Add(String.Format("{0}_{1}", _table.TableName, p.Name), p.Name);                 
            }
            
            if (_table.Count == 0)
                _table.FindSet();
            do
            {
                row = new DataGridViewRow();
                row.CreateCells(_dgv);
                List<object> data = new List<object>();
                foreach (PropertyInfo p in _table.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
                {
                    string colName = String.Format("{0}_{1}", _table.TableName, p.Name);
                    row.Cells[_dgv.Columns[colName].Index].Value = p.GetValue(_table);                    
                }
                _dgv.Rows.Add(row);
            }
            while(_table.Next());
            
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

        public static void GetSelectedRecords(this DataGridView _dgv, ECTable _table)
        {
            string filter = "";

            foreach (DataGridViewCell c in _dgv.SelectedCells)
            {
                string cellName = String.Format("{0}_{1}", _table.TableName, nameof(_table.RecId));
                filter += _dgv.Rows[c.RowIndex].Cells[cellName].Value + "|";
            }
            if (filter != "")
            {
                _table.Clear();
                _table.SetFilter("RecId", filter.Substring(0, filter.Length - 1));
                _table.FindSet();
            }
            else
            {
                _table.Init();
            }
        }
    }
}
