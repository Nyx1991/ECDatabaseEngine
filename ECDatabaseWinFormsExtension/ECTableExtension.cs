using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ECDatabaseEngine;
using System.Reflection;

namespace ECDatabaseWinFormsExtension
{
    public static class ECTableExtension
    {
        /// <summary>
        /// Transfers the data of a DataGridViewRow into the table buffer
        /// </summary>
        /// <param name="_table">ECTable instance</param>
        /// <param name="_row">DataGridViewRow the data should be copied from</param>
        public static void CopyFromDataGridViewRow(this ECTable _table, DataGridViewRow _row)
        {
            foreach (PropertyInfo p in _table.GetType().GetProperties().Where(x => x.IsDefined(typeof(TableFieldAttribute))))
            {
                string cellName = String.Format("{0}_{1}", _table.TableName, p.Name);
                try
                {
                    DataGridViewCell cell = _row.Cells[cellName];
                    if (cell != null)
                    {
                        p.SetValue(_table, cell.Value);                        
                    }
                }
                catch
                {
                    continue;
                }                
            }
        }

        /// <summary>
        /// If the table is bound to a DataGridView, this funtions returns the name of the column of the given field in that DataGridView
        /// </summary>
        /// <param name="_table">ECTable instance</param>
        /// <param name="_fieldName">Name of the field the DataGridViewColumnName should returend from</param>
        /// <returns>name of the column of the given field in bound DataGridView</returns>
        public static string GetDataGridViewColumnName(this ECTable _table, string _fieldName)
        {
            return String.Format("{0}_{1}", _table.TableName, _fieldName);
        }

    }
}
