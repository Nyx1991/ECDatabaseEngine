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

        private static List<Control> controlsWithECTableFieldTextBinding;

        static ControlExtension()
        {
            controlsWithECTableFieldTextBinding = new List<Control>();
        }

        /// <summary>
        /// Adds a binding to the text in the control onto the given field of the given table.
        /// All operations on the table will be synched automatically to the control.
        /// </summary>
        /// <param name="_control">ListBox itself</param>
        /// <param name="_table">Table the data should be loaded from</param>
        /// <param name="_fieldName">Name of the field the data should loaded from</param>
        public static void SetECTableFieldTextBinding(this Control _control, ECTable _table, string _fieldName)
        {
            if (controlsWithECTableFieldTextBinding.Contains(_control))
            {
                throw new ECBindingAlreadyExistsException(_control);
            }

            controlsWithECTableFieldTextBinding.Add(_control);

            _table.OnChanged += delegate (object sender, ECTable _callerTable)
            {
                PropertyInfo p = _callerTable.GetType().GetProperty(_fieldName);
                _control.Text = p.GetValue(_callerTable).ToString();
            };

            _table.OnAfterFindSet += delegate (object sender, ECTable _callerTable)
            {
                PropertyInfo p = _callerTable.GetType().GetProperty(_fieldName);
                _control.Text = p.GetValue(_callerTable).ToString();
            };

            _control.TextChanged += delegate (object sender, EventArgs e) 
            {
                PropertyInfo p = _table.GetType().GetProperty(_fieldName);

                if (_control.Text != "")
                { 
                    p.SetValue(_table, Convert.ChangeType(_control.Text, p.PropertyType));
                }
            };

            _control.Disposed += delegate (object sender, EventArgs a)
            {
                controlsWithECTableFieldTextBinding.Remove(_control);
            };            
            
        }

    }
}
