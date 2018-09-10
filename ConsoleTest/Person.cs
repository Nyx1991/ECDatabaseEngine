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

        [TableField(FieldType.VARCHAR, 50)]
        public string Firstname { get; set; }

        [TableField(FieldType.VARCHAR, 50)]
        public string Name { get; set; }

    }
}
