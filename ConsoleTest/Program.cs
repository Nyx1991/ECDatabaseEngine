using System;
using ECDatabaseEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            ECDatabaseConnection.Connect(AppRessource.url, AppRessource.database, AppRessource.user, AppRessource.pass);
            Console.WriteLine(ECDatabaseConnection.IsConnected);

            Person pers = new Person();
            pers.SynchronizeSchema();


            ECDatabaseConnection.Disconnect();            
            Console.ReadKey();
        }
    }
}
