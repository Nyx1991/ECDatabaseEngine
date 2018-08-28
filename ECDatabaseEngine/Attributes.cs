using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECDatabaseEngine
{
    public class PrimaryKeyAttribute : Attribute
    {
    }

    public class NotNullAttribute : Attribute
    {
    }

    internal class AutoIncrementAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class TableFieldAttribute : Attribute
    {
        public FieldType type;
        public int length = 0;
        public TableFieldAttribute(FieldType t)
        {
            if(t == FieldType.VARCHAR)
            {
                throw new Exception("You need to specify a length for the types varchar");
            }
            type = t;
            length = 0;
        }

        public TableFieldAttribute(FieldType t, int l)
        {
            type = t;
            length = l;
        }
    }

    //[AttributeUsage(AttributeTargets.Field)]
    //public class TaskParameterAttribute : Attribute
    //{

    //}
}
