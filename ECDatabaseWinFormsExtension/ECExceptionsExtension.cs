using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ECDatabaseWinFormsExtension
{
    public class ECBindingAlreadyExistsException : Exception { public ECBindingAlreadyExistsException(Control _control) : base($"The control \"{_control.Name}\" is already bound to an ECTable") { } }

}
