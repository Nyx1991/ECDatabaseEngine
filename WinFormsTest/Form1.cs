using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ECDatabaseEngine;
using ECDatabaseWinFormsExtension;

namespace WinFormsTest
{
    public partial class Form1 : Form
    {
        private Person person;        
        private Address address;
        private Address address_Grid;

        public Form1()
        {
            InitializeComponent();
            person = new Person();
            address = new Address();
            address_Grid = new Address();

            person.AddJoin(address, nameof(person.RefAddress), ECJoinType.Inner);            

            txtFirstname.SetECTableFieldTextBinding(person, nameof(person.Firstname));
            txtName.SetECTableFieldTextBinding(person, nameof(person.Name));
            txtRefAddr.SetECTableFieldTextBinding(person, nameof(person.RefAddress));

            txtCity.SetECTableFieldTextBinding(address, nameof(address.City));
            txtStreet.SetECTableFieldTextBinding(address, nameof(address.Street));
            txtRecIdAddress.SetECTableFieldTextBinding(address, nameof(address.RecId));

            personGrid.SetECTableDataBinding(person, FieldFilter.HideGivenFields, nameof(person.RecId));
            personGrid.AddDataFromECTable(address, FieldFilter.ShowGivenFields, nameof(address.City), nameof(address.Street));

            //addressGrid.SetECTableDataBinding(address);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            person.ModifyAll();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            person.FindSet();
            personGrid.AddDataFromECTable(address, FieldFilter.ShowGivenFields, nameof(address.City), nameof(address.Street));
        }
    }
}
