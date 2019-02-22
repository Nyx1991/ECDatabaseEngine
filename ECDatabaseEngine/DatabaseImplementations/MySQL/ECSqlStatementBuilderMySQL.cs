using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECDatabaseEngine
{
    class ECSqlStatementBuilderMySQL : ECSqlStatementBuilderBase
    {
        private static readonly object padlock = new object();
        private static ECSqlStatementBuilderMySQL instance = null;

        public static ECSqlStatementBuilderMySQL Instance
        {
            get {
                lock (padlock)
                { 
                    if (instance == null)
                    {
                        instance = new ECSqlStatementBuilderMySQL();
                    }
                }
                return instance;
            }
        }

    }
}
