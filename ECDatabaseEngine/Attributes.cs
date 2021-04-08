using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace ECDatabaseEngine
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ECPrimaryKeyAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ECNotNullAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property)]
    internal class ECAutoIncrementAttribute : Attribute
    {
    }    

    [AttributeUsage(AttributeTargets.Property)]
    public class ECTableFieldAttribute : Attribute
    {
        public FieldType type;
        public int length = 0;
        public ECTableFieldAttribute(FieldType t)
        {
            if(t == FieldType.VARCHAR)
            {
                throw new Exception("You need to specify a length for the types varchar");
            }
            type = t;
            length = 0;
        }

        public ECTableFieldAttribute(FieldType t, int l)
        {
            type = t;
            length = l;        
        }
    }


}
