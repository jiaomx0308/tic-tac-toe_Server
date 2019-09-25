using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SimpleJSON;

namespace MyLib
{
    public class ServerConfig
    {
        public string VERSION = "1.1.0";

        public static ServerConfig instance;
        public JSONClass configMap;

        public ServerConfig()
        {
            instance = this;
            var file = System.IO.File.ReadAllText("ServerConfig.json");
            configMap = JSON.Parse(file).AsObject;
        }

        public string GetMySqlConnectionString()
        {
            return configMap["DatabaseConnection"];
        }

        public int GetHttpServerListenPort()
        {
            return int.Parse(configMap["HttpServerListenPort"]);
        }
    }


}
