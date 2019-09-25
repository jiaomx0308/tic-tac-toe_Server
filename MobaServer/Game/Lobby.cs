using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;

namespace MyLib
{
	public class Lobby : Actor
	{
		private class MatchRequest
		{
			public BroadcastBlock<int> bb = new BroadcastBlock<int>(null);  //bb用来表征房间的id
			public int playerId;
		}

		private Queue<MatchRequest> matchQueue = new Queue<MatchRequest>();
		private BufferBlock<int> waitForPlayer = new BufferBlock<int>();

		//private Dictionary<int, Room> rooms = new Dictionary<int, Room>();

		public Lobby()
		{
		}
		public override void Init()
		{
			RunTask(CheckMatch);
		}
		private async Task CheckMatch()  //开始匹配,在RunTask时,就已经把该片断放到了messageQueue中的线程执行
        {
			while (!isStop)
			{
				await waitForPlayer.ReceiveAsync();   //会阻塞等待SendAsync的到来
                if (matchQueue.Count >= 2)   //当匹配到两个人时,创建一个room,开始游戏
				{
					var room = new Room();   
					ActorManager.Instance.AddActor(room);  
					//rooms.Add(room.Id, room);

					var a = matchQueue.Dequeue();
					var b = matchQueue.Dequeue();
					await room.AddPlayer(a.playerId);
					await room.AddPlayer(b.playerId);
					await a.bb.SendAsync(room.Id);
					await b.bb.SendAsync(room.Id);
					await room.GameStart();
				}
			}
		}

		public async Task FindRoom(int playerId)  //玩家寻找房间
		{
			await _messageQueue;
			LogHelper.Log("Lobby", "FindRoom:"+playerId);
            
            var bb = new BroadcastBlock<int>(null);
			var mr = new MatchRequest()
			{
				bb = bb,
				playerId = playerId,
			};
			matchQueue.Enqueue(mr);
            await waitForPlayer.SendAsync(playerId);

            var roomId = await bb.ReceiveAsync();  //这里需要等匹配完成后才会继续执行,否则会等待
            LogHelper.Log("Lobby", "FindRoomId:"+roomId);
			//var room = rooms[roomId];
			//room.AddPlayer(playerId);
		}
	}
}
