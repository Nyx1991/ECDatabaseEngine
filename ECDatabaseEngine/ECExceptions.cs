using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECDatabaseEngine
{
    public class ECJoinNotFoundException : Exception { public ECJoinNotFoundException(string _message) : base(_message) { } }
    public class ECFieldNotFoundException : Exception { public ECFieldNotFoundException(string _message) : base(_message) { } }
}
