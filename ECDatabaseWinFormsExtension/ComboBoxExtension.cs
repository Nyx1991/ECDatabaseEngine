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
        private static Dictionary<ComboBox, Dictionary<int, int>> comboBoxBufferIdxMap;
        private static List<ComboBox> comboBoxesWithECTableFieldItemBinding;
        private static List<ComboBox> comboBoxesWithECTableFieldTextBinding;

        static ComboBoxExtension()
        {
            comboBoxBufferIdxMap = new Dictionary<ComboBox, Dictionary<int, int>>();
            comboBoxesWithECTableFieldItemBinding = new List<ComboBox>();
            comboBoxesWithECTableFieldTextBinding = new List<ComboBox>();
        }

        /// <summary>
        /// Removes all items from the ComboBox and loads new ones based on the given table and field
        /// </summary>
        /// <param name="_comboBox">ComboBox itself</param>
        /// <param name="_table">Table the data should be loaded from</param>
        /// <param name="_fieldName">Name of the field the data should loaded from</param>
        public static void LoadItemsFromECTable(this ComboBox _comboBox, ECTable _table, string _fieldName)
        {
            _comboBox.Items.Clear();
            _comboBox.Text = "";
            AddItemsFromECTable(_comboBox, _table, _fieldName);
        }

        /// <summary>
        /// Adds new items to the ComboBox based on the given table and field. Existing items will not be removed.
        /// </summary>
        /// <param name="_comboBox">ComboBox itself</param>
        /// <param name="_table">Table the data should be loaded from</param>
        /// <param name="_fieldName">Name of the field the data should loaded from</param>
        public static void AddItemsFromECTable(this ComboBox _comboBox, ECTable _table, string _fieldName)
        {            
            PropertyInfo p = _table.GetType().GetProperty(_fieldName);
            
            _table.FindSet(false);
            do
            {
                int index = _comboBox.Items.Add(p.GetValue(_table));                
            } while (_table.Next(false));

        }

        /// <summary>
        /// Sets the current buffer index of the given table to the index that is related to the ComboBox index
        /// </summary>
        /// <param name="_comboBox">ListBox itself</param>
        /// <param name="_table">Table the data should be loaded from</param>
        public static void GetSelectedRecord(this ComboBox _comboBox, ECTable _table)
        {            
            _table.Clear();
            _table.SetCurrentBufferIndex(comboBoxBufferIdxMap[_comboBox][_comboBox.SelectedIndex]);                
        }

        /// <summary>
        /// Adds a binding to the items in the list onto the given field of the given table.
        /// All operations on the table will be synched automatically to the ComboBox.
        /// </summary>
        /// <param name="_comboBox">ComboBox itself</param>
        /// <param name="_table">Table the data should be loaded from</param>
        /// <param name="_fieldName">Name of the field the data should loaded from</param>
        public static void SetECTableFieldItemBinding(this ComboBox _comboBox, ECTable _table, string _fieldName)
        {
            if (comboBoxesWithECTableFieldItemBinding.Contains(_comboBox))
            {
                throw new ECBindingAlreadyExistsException(_comboBox);
            }

            _comboBox.SetECTableFieldTextBinding(_table, _fieldName);

            PropertyInfo p = _table.GetType().GetProperty(_fieldName);
            Dictionary<int, int> indexBufferIdxMap = new Dictionary<int, int>();

            comboBoxesWithECTableFieldItemBinding.Add(_comboBox);

            if (_table.Count == 0)
                _table.FindSet(false);

            int i = 0;
            foreach (ECTable t in _table)
            {
                int index = _comboBox.Items.Add(p.GetValue(t));
                indexBufferIdxMap.Add(index, i);
                i++;
            }

            if (comboBoxBufferIdxMap.ContainsKey(_comboBox))
                comboBoxBufferIdxMap.Remove(_comboBox);

            comboBoxBufferIdxMap.Add(_comboBox, indexBufferIdxMap);

            _table.OnAfterModify += delegate (object sender, ECTable _callerTable)
            {
                int comboIdx = comboBoxBufferIdxMap[_comboBox].First( x => x.Value == _callerTable.GetCurrentBufferIndex()).Key;
                _comboBox.Items[comboIdx] = p.GetValue(_callerTable);
            };
            
            _table.OnAfterFindSet += delegate (object sender, ECTable _callerTable)
            {
                LoadItemsFromECTable(_comboBox, _table, _fieldName);
            };

            _table.OnChanged += delegate (object sender, ECTable _callerTable)
            {
                int comboIdx = comboBoxBufferIdxMap[_comboBox].First(x => x.Value == _callerTable.GetCurrentBufferIndex()).Key;
                _comboBox.Items.RemoveAt(comboIdx);
                _comboBox.Items.Insert(comboIdx, _table.GetType().GetProperty(_fieldName).GetValue(_table));
                _comboBox.SelectedIndex = comboIdx;                
            };

            _comboBox.Disposed += delegate (object sender, EventArgs e)
            {
                comboBoxBufferIdxMap.Remove(_comboBox);
                comboBoxesWithECTableFieldItemBinding.Remove(_comboBox);
            };
           
        }

        public static void SetECTableFieldTextBinding(this ComboBox _comboBox, ECTable _table, string _fieldName)
        {
            if (comboBoxesWithECTableFieldTextBinding.Contains(_comboBox))
            {
                throw new ECBindingAlreadyExistsException(_comboBox);
            }

            comboBoxesWithECTableFieldTextBinding.Add(_comboBox);

            _table.OnChanged += delegate (object sender, ECTable _callerTable)
            {
                PropertyInfo p = _callerTable.GetType().GetProperty(_fieldName);
                _comboBox.Text = p.GetValue(_callerTable).ToString();
            };

            _table.OnAfterFindSet += delegate (object sender, ECTable _callerTable)
            {
                PropertyInfo p = _callerTable.GetType().GetProperty(_fieldName);
                _comboBox.Text = p.GetValue(_callerTable).ToString();
            };

            _comboBox.Disposed += delegate (object sender, EventArgs a)
            {
                comboBoxesWithECTableFieldTextBinding.Remove(_comboBox);
            };

        }
    }
}
