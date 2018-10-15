using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECDatabaseEngine
{
    public enum ECJoinType { Inner, LeftOuter, RightOuter };

    internal class ECJoin
    {
        public dynamic Table { get; set; }
        public string OnField { get; set; }
        public ECJoinType JoinType { get; set; }    
        public Type TableType { get { return Table.GetType(); } }
    }
}
