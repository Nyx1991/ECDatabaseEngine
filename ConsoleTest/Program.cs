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

            Person p = new Person();
            p.SynchronizeSchema();

            p.Init();

            p.FindSet();

            Console.WriteLine(p);
            Console.WriteLine("-----------------------");
            p.Next();
            Console.WriteLine(p);
            Console.WriteLine("-----------------------");

            Console.WriteLine(p);
            Console.WriteLine("-----------------------");
            p.Next();
            Console.WriteLine(p);

            Console.WriteLine("-----------------------");

            p.FindSet();
            Console.WriteLine(p);
            Console.WriteLine("-----------------------");
            p.Next();
            Console.WriteLine(p);

            ECDatabaseConnection.Disconnect();            
            Console.ReadKey();
        }
    }
}
