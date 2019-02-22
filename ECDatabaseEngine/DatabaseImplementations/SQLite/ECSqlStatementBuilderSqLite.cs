using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECDatabaseEngine
{
    class ECSqlStatementBuilderSqLite : ECSqlStatementBuilderBase
    {
        private static readonly object padlock = new object();
        private static ECSqlStatementBuilderSqLite instance = null;

        public static ECSqlStatementBuilderSqLite Instance
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                    {
                        instance = new ECSqlStatementBuilderSqLite();
                    }
                }
                return instance;
            }
        }
    }
}
