using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ECDatabaseEngine;

namespace ConsoleTest
{
    class Person : ECTable
    {

        [ECTableField(FieldType.VARCHAR, 50)]
        public string Firstname { get; set; }
        [ECTableField(FieldType.VARCHAR, 50)]
        public string Name { get; set; }
        [ECTableField(FieldType.INT)]
        public int RefAddress { get; set; }
    }
}
