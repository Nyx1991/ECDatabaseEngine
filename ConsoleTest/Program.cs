using System;
using ECDatabaseEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace ConsoleTest
{

    class Program
    {
        static void Main(string[] args)
        {
            /*
            string connectionString = String.Format("driver=mysql;server={0};database={1};user={2};pass={3}",
                    AppRessource.url,
                    AppRessource.database,
                    AppRessource.user,
                    AppRessource.pass
                );
            */
            string connectionString = String.Format(@"driver=sqlite;dbPath=C:\temp\test.db3");

            ECDatabaseConnection.Connect(connectionString);

            Person p = new Person();
            Address a = new Address();

            p.AddJoin(a, nameof(p.RefAddress), ECJoinType.Inner);
            p.AddOrderBy(nameof(p.RecId));
            p.OrderType = OrderType.DESC;
            p.FindSet();
            //a.FindSet();

            print(p);
            print(a);

            Console.WriteLine(ECDatabaseConnection.IsConnected);

            ECDatabaseConnection.Disconnect();            
            Console.ReadKey();
        }

        public static void print(ECTable t)
        {
            do
            {
                Console.WriteLine("------------------------------------");
                Console.WriteLine(t);
            } while (t.Next());
        }
    }
}
