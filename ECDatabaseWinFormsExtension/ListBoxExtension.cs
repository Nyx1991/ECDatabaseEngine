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
    public static class ListBoxExtension
    {
        private static Dictionary<ListBox, Dictionary<int, int>> listBoxBufferIdxMap;
        private static List<ListBox> listBoxesWithECTableFieldItemBinding;
        private static List<ListBox> listBoxesWithECTableFieldTextBinding;


        static ListBoxExtension()
        {
            listBoxBufferIdxMap = new Dictionary<ListBox, Dictionary<int, int>>();
            listBoxesWithECTableFieldItemBinding = new List<ListBox>();
            listBoxesWithECTableFieldTextBinding = new List<ListBox>(); 
        }
        
        /// <summary>
        /// Removes all items from the ListBox and loads new ones based on the given table and field
        /// </summary>
        /// <param name="_listBox">ListBox itself</param>
        /// <param name="_table">Table the data should be loaded from</param>
        /// <param name="_fieldName">Name of the field the data should loaded from</param>
        public static void LoadItemsFromECTable(this ListBox _listBox, ECTable _table, string _fieldName)
        {
            _listBox.Items.Clear();
            _listBox.Text = "";
            AddItemsFromECTable(_listBox, _table, _fieldName);
        }

        /// <summary>
        /// Adds new items to the ListBox based on the given table and field. Existing items will not be removed.
        /// </summary>
        /// <param name="_listBox">ListBox itself</param>
        /// <param name="_table">Table the data should be loaded from</param>
        /// <param name="_fieldName">Name of the field the data should loaded from</param>
        public static void AddItemsFromECTable(this ListBox _listBox, ECTable _table, string _fieldName)
        {            
            PropertyInfo p = _table.GetType().GetProperty(_fieldName);
            
            _table.FindSet(false);
            do
            {
                int index = _listBox.Items.Add(p.GetValue(_table));                
            } while (_table.Next(false));

        }

        /// <summary>
        /// Sets the current buffer index of the given table to the index that is related to the ListBox index
        /// </summary>
        /// <param name="_listBox">ListBox itself</param>
        /// <param name="_table">Table the data should be loaded from</param>
        public static void GetSelectedRecord(this ListBox _listBox, ECTable _table)
        {            
            _table.Clear();
            _table.SetCurrentBufferIndex(listBoxBufferIdxMap[_listBox][_listBox.SelectedIndex]);                
        }

        /// <summary>
        /// Adds a binding to the items in the list onto the given field of the given table.
        /// All operations on the table will be synched automatically to the ListBox.
        /// </summary>
        /// <param name="_listBox">ListBox itself</param>
        /// <param name="_table">Table the data should be loaded from</param>
        /// <param name="_fieldName">Name of the field the data should loaded from</param>
        public static void SetECTableFieldItemBinding(this ListBox _listBox, ECTable _table, string _fieldName)
        {
            if (listBoxesWithECTableFieldItemBinding.Contains(_listBox))
            {
                throw new ECBindingAlreadyExistsException(_listBox);
            }

            PropertyInfo p = _table.GetType().GetProperty(_fieldName);
            Dictionary<int, int> indexBufferIdxMap = new Dictionary<int, int>();

            listBoxesWithECTableFieldItemBinding.Add(_listBox);

            _table.FindSet(false);
            do
            {
                int index = _listBox.Items.Add(p.GetValue(_table));
                indexBufferIdxMap.Add(index, _table.GetCurrentBufferIndex());
            } while (_table.Next(false));

            if (listBoxBufferIdxMap.ContainsKey(_listBox))
                listBoxBufferIdxMap.Remove(_listBox);

            listBoxBufferIdxMap.Add(_listBox, indexBufferIdxMap);

            _table.OnAfterModify += delegate (object sender, ECTable _callerTable)
            {
                int listIdx = listBoxBufferIdxMap[_listBox].First( x => x.Value == _callerTable.GetCurrentBufferIndex()).Key;
                _listBox.Items[listIdx] = p.GetValue(_callerTable);
            };

            _table.OnAfterFindSet += delegate (object sender, ECTable _callerTable)
            {
                LoadItemsFromECTable(_listBox, _table, _fieldName);
            };
                
            _listBox.Disposed += delegate (object sender, EventArgs e)
            {
                listBoxBufferIdxMap.Remove(_listBox);
                listBoxesWithECTableFieldItemBinding.Remove(_listBox);
            };
           
        }

        /// <summary>
        /// Adds a binding to the text in the control onto the given field of the given table.
        /// All operations on the table will be synched automatically to the control.
        /// </summary>
        /// <param name="_control">ListBox itself</param>
        /// <param name="_table">Table the data should be loaded from</param>
        /// <param name="_fieldName">Name of the field the data should loaded from</param>
        public static void SetECTableFieldTextBinding(this ListBox _listBox, ECTable _table, string _fieldName)
        {
            if (listBoxesWithECTableFieldTextBinding.Contains(_listBox))
            {
                throw new ECBindingAlreadyExistsException(_listBox);
            }

            listBoxesWithECTableFieldTextBinding.Add(_listBox);

            _table.OnChanged += delegate (object sender, ECTable _callerTable)
            {
                PropertyInfo p = _callerTable.GetType().GetProperty(_fieldName);
                _listBox.Text = p.GetValue(_callerTable).ToString();
            };

            _table.OnAfterFindSet += delegate (object sender, ECTable _callerTable)
            {
                PropertyInfo p = _callerTable.GetType().GetProperty(_fieldName);
                _listBox.Text = p.GetValue(_callerTable).ToString();
            };

            _listBox.Disposed += delegate (object sender, EventArgs a)
            {
                listBoxesWithECTableFieldTextBinding.Remove(_listBox);
            };

        }
    }
}
