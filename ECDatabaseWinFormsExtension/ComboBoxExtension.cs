using ECDatabaseEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ECDatabaseWinFormsExtension
{
    public static class ComboBoxExtension
    {
        private static Dictionary<ComboBox, Dictionary<int, int>> comboBoxRecIdMap;

        static ComboBoxExtension()
        {
            comboBoxRecIdMap = new Dictionary<ComboBox, Dictionary<int, int>>();
        }

        public static void LoadFromECTable(this ComboBox _comboBox, ECTable _table, string _fieldName)
        {
            _comboBox.Items.Clear();
            _comboBox.Text = "";
            AddFromECTable(_comboBox, _table, _fieldName);
        }

        public static void AddFromECTable(this ComboBox _comboBox, ECTable _table, string _fieldName)
        {
            Dictionary<int, int> indexRecIdMap = new Dictionary<int, int>();
            PropertyInfo p = _table.GetType().GetProperty(_fieldName);
            
            _table.FindSet();
            do
            {
                int index = _comboBox.Items.Add(p.GetValue(_table));
                indexRecIdMap.Add(index, _table.RecId);
            } while (_table.Next());
            comboBoxRecIdMap.Add(_comboBox, indexRecIdMap);

            _comboBox.Disposed += delegate (object sender, EventArgs e)
            {
                comboBoxRecIdMap.Remove(_comboBox);
            };
        }

        public static void GetSelectedRecord(this ComboBox _comboBox, ECTable _table)
        {            
            _table.Clear();
            _table.Get(comboBoxRecIdMap[_comboBox][_comboBox.SelectedIndex]);
        }
    }
}
