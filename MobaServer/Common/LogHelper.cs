using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using log4net.Core;

namespace MyLib
{
    public class LogHelper
    {
        private static ILog logger = LogManager.GetLogger("RollingLogFileAppender");

        public static void Init()
        {
        }


        public static void LogClientLogin(string content)
        {
            Log("ClientLogin", content);
        }

        public static void LogUnhandleException(string content)
        {
            Log("UnhandleException", content);
        }

        public static void LogClientLogout(string content)
        {
            Log("ClientLogout", content);
        }

        [Conditional("DEBUG_LOG")]
        public static void LogReceivePacket(string content)
        {
            Log("ReceivePacket",content);
        }

        [Conditional("DEBUG_LOG")]
        public static void LogSendPacket(string content)
        {
            Log("SendPacket", content);
        }

        public static void LogAgentsCount(string content)
        {
            Log("AgentsCount", content);
        }

        public static void LogCloseServer()
        {
            Log("CloseServer","");
        }

        //[Conditional("DEBUG_LOG")]
        public static void Log(string tag,string conent)
        {
            logger.Debug(string.Format("{0} {1}",tag,conent));
        }
    }
}
