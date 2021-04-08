using ECDatabaseEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleTest
{
    class Address : ECTable
    {
        [ECTableField(FieldType.VARCHAR, 50)]
        public String Street { get; set; }
        [ECTableField(FieldType.VARCHAR, 50)]
        public String City { get; set; }

    }
}
