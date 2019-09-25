using System;
using System.Threading.Tasks;
using log4net;

[assembly: log4net.Config.XmlConfigurator(ConfigFile = "log4net.config", Watch = true)]

namespace MyLib
{
    class MainClass
    {
        public static void Main(string[] args)
        {


            Console.WriteLine("Hello World!");
            LogHelper.Log("Server", "StartServer");
            var sg = new SaveGame();   //SaveGame()是一个获取ProtoBuff协议的类,通过生成的nameMap.json,获得modeid messageid和对应的名字
            var config = new ServerConfig();   //读取配置文件 ServerConfig.json
            var am = new ActorManager();   //用于管理所有Actor

            var lobby = new Lobby();  //Lobby Actor,一般在这创建的Actor用于管理所有的玩家,不同类型的游戏不同的管理方法,Moba是一个Lobby进行管理,MMO可能就是一个World Actor
            am.AddActor(lobby, true);  //一般唯一的Actor的第二个参数可以设置为true,但是最后优化下,不允许同类型的传入

            var ss = new SocketServer();  //网络端口监听
            am.AddActor(ss, true);


            Console.CancelKeyPress += new ConsoleCancelEventHandler(myHandler);  //注册ctrl + c的回调
            RegisterException();   //异常处理

            var port = ServerConfig.instance.configMap["Port"].AsInt;  //从ServerConfig.json解析出来的端口配置文件
            ss.Start(port);    //开启端口,并监听
            ss.mThread.Join(); //阻塞,回收资源
            GC.Collect();
            Console.WriteLine("EndServer");
        }

        private static void RegisterException()
        {
            AppDomain.CurrentDomain.UnhandledException += UnhandleExcepition;  //添加未捕捉的异常,的处理函数
            TaskScheduler.UnobservedTaskException += (sender, eventArgs) =>  //添加未观测到的异常,的异常处理程序
            {
                eventArgs.SetObserved(); //标记该异常已经被观测到  //否则会直接结束该进程
                var error = eventArgs.Exception;
                Console.WriteLine(sender.ToString() + "  " + error.ToString());
                LogHelper.LogUnhandleException(sender.ToString() + "  " + error.ToString());
            };
        }

        private static void UnhandleExcepition(object sender, UnhandledExceptionEventArgs e)
        {
            var error = e.ExceptionObject as Exception;
            Console.WriteLine(sender.ToString() + "  " + error.ToString());
            LogHelper.LogUnhandleException(sender.ToString() + "  " + error.ToString());
        }

        private static void myHandler(object sender, ConsoleCancelEventArgs args)
        {
            Debug.Log("ServerStop");
            ActorManager.Instance.Stop();  //停止所有Actor活动
        }
    }
}
