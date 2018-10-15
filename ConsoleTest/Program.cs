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
            string connectionString = String.Format("driver=mysql;server={0};database={1};user={2};pass={3}",
                    AppRessource.url,
                    AppRessource.database,
                    AppRessource.user,
                    AppRessource.pass
                );

            //string connectionString = String.Format("driver=sqlite;dbPath=C:\\temp\\test.db3");

            ECDatabaseConnection.CreateConnection(connectionString);
            Console.WriteLine(ECDatabaseConnection.IsConnected);

            Address a = new Address();
            Person p = new Person();            
            p.AddJoin(a, "RefAddress", ECJoinType.Inner);
            p.FindSet();

            do
            {
                Console.Write(p.Name+" - ");
                Console.WriteLine(p.JoinedTable<Address>().City);
            } while (p.Next());

            p.Reset();         

            do
            {
                Console.Write(p.Name + " - ");
                Console.WriteLine(p.JoinedTable<Address>().City);
            } while (p.Next());

            Console.ReadKey();

            ECDatabaseConnection.Disconnect();            
            Console.ReadKey();
        }

        public static void print(Person pers)
        {
            do
            {
                Console.WriteLine("------------------------------------");
                Console.WriteLine(pers);
            } while (pers.Next());
        }
    }
}
