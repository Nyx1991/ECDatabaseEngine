using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECDatabaseEngine
{
    internal interface IECConnection
    {
        bool    IsConnected { get; }
        string  CurrentDatabase { get; }
        string  CurrentUser { get; }

        bool Connect(Dictionary<string, string> _params);
        void Disconnect();        
        int Insert(ECTable _table);
        void Delete(ECTable _table);
        void Modify(ECTable _table);
        bool Exists(ECTable _table);
        List<Dictionary<string, string>> GetData(ECTable _table, Dictionary<string, string> _filter, Dictionary<string, KeyValuePair<string, string>> _ranges);
        void CreateTableIfNotExist(ECTable _table);
        string GetConnectionStringExample();
    }
}
