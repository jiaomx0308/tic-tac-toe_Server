using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

namespace MyLib
{
    public class PlayerState
    {
        public int px = 0;
        public int py = 0;
        public int hp = 100;
    }

	public class ActorExport : Attribute
	{
	}

	public class PlayerActor : Actor  //继承自Actor的玩家类
	{
		private uint agentId;
		private Agent agent;
		private Room room;

        public PlayerState playerState;


		public async Task SetRoom(Room r)  //async,在Room中调用
		{
			await _messageQueue;   //表示把该调用压入messageQueue,
            room = r;
		}

		private static Dictionary<string, MethodInfo> methods = new Dictionary<string, MethodInfo>();
		static PlayerActor()  //把所有带有ActorExport标签的method放入methods中
        {
			var met = typeof(PlayerActor).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)  //GetMethods找到所有的非公有方法
                .Where(m => m.GetCustomAttributes(typeof(ActorExport), false).Length > 0).ToArray();  //where变量所有Enumerable的成员,找出满足条件的成员 //m为其中一个成员,  m.GetCustomAttributes找出带有ActorExport标签的methods, false表示不追踪继承关系
            Console.WriteLine("PlayerActorInit:"+met.Count());
			/*
			foreach (var m in met)
			{
				var attr = m.GetCustomAttributes(typeof(ActorExport), false);
				Console.WriteLine("Methods: "+m.Name+attr.Length);
			}
			*/
			foreach (var m in met)
			{
				methods.Add(m.Name, m);
			}
		}

		public PlayerActor(uint playerId)  //playerId由Agent的Static变量分配,agent和Player相互绑定
        {
			agentId = playerId;   
			var server = ActorManager.Instance.GetActor<SocketServer>();
			agent = server.GetAgent(agentId);
			agent.actor = this;
            playerState = new PlayerState();

        }

		private void InitPlayerId()  //初始化客户端Player的ID,为每个Actor的专属ID,由ActorManager统一管理
		{
			var cmd = GCPlayerCmd.CreateBuilder();
			cmd.Result = string.Format("Init {0}", Id);

			ServerBundle bundle;
			var bytes = ServerBundle.sendImmediateError(cmd, 0, 0, out bundle);
			ServerBundle.ReturnBundle(bundle);

			agent.SendBytes(bytes);
		}

		public override void Init()
		{
			InitPlayerId();  //为player分配ID
			RunTask(Dispatch);  //开启接收task,负责处理消息
		}

		protected override async Task ReceiveMsg(ActorMsg msg)   //消息处理,当Agent解析完二进制数据后,发送到这处理
		{

			if (!string.IsNullOrEmpty(msg.msg))  //对内部string命令的处理,
			{
				var cmds = msg.msg.Split(' ');
				if (cmds[0] == "close")
				{
					await RemovePlayer();
				}
			}
			else
            {    //对ProtoBuff进行处理 ,对于PlayerActor,其接收的protobuff格式都是CGPlayerCmd的格式
                LogHelper.Log("PlayerActor", "Receive:"+msg.packet.protoBody.ToString());
				var pb = msg.packet.protoBody;
				if (pb is CGPlayerCmd)
				{
					var cmd = msg.packet.protoBody as CGPlayerCmd;
					var cmds = cmd.Cmd.Split(' ');
					var mn = cmds[0];

                    //根据cmd的命令在methods查找对应的方法并并调用执行处理
                    MethodInfo methodInfo = null;
					var find = methods.TryGetValue(mn, out methodInfo);

					if (methodInfo != null)
					{
						var ta = (Task)(methodInfo.Invoke(this, new object[] { cmd }));
						await ta;
					}
				}
			}
		}




		[ActorExport]
		private async Task Match(CGPlayerCmd cmd)
		{
			LogHelper.Log("Match", cmd.ToString());
			var lobby = ActorManager.Instance.GetActor<Lobby>();
			await lobby.FindRoom(Id);
		}

        [ActorExport]
        private async Task Skill(CGPlayerCmd cmd)
        {
            await room.Skill(Id, cmd);
        }

        [ActorExport]
        private async Task MoveTo(CGPlayerCmd cmd)
        {
            await room.MoveTo(Id, cmd);
        }


		[ActorExport]
		private async Task MakeMove(CGPlayerCmd cmd)
		{
			await room.MakeMove(Id, cmd);
		}

		[ActorExport]
		private async Task Leave()
		{
			await room.Leave(Id);
		}

		public Agent GetAgent()
		{
			return agent;
		}

		private async Task RemovePlayer()  //删除player
		{
			if (room != null)
			{
				await room.Leave(Id);
			}

			ActorManager.Instance.RemoveActor(Id);
			LogHelper.Log("PlayerActor", "CloseActor " + Id);

		}
	}
}
