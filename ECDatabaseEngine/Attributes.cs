using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace ECDatabaseEngine
{
    [AttributeUsage(AttributeTargets.Property)]
    public class PrimaryKeyAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class NotNullAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property)]
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


}
