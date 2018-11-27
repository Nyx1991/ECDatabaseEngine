using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ECDatabaseEngine;
using System.Windows.Forms;
using System.Reflection;

namespace ECDatabaseWinFormsExtension
{
    public static class ControlExtension
    {
        public static void AddECTableFieldBinding(this Control _control, ECTable _table, string _fieldName)
        {
            _control.TextChanged += delegate(object sender, EventArgs e) 
                {
                    PropertyInfo p = _table.GetType().GetProperty(_fieldName);
                    p.SetValue(_table, _control.Text);
                };

            _table.OnChanged += delegate (object sender, ECTable _callerTable)
            {
                PropertyInfo p = _callerTable.GetType().GetProperty(_fieldName);
                _control.Text = p.GetValue(_callerTable).ToString();
            };
        }        

    }
}
