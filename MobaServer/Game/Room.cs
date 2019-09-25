using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MyLib
{
	public class Room : Actor
	{
        private Dictionary<int, PlayerActor> PlayerMap = new Dictionary<int, PlayerActor>();
        private Dictionary<int, Bullet> bulletMap = new Dictionary<int, Bullet>();
		private List<PlayerActor> players = new List<PlayerActor>();
		private int curTurn = 0;
		private bool isGameOver = false;

		private List<int> chessState = new List<int>();   //游戏数据,在这个游戏中为一个棋盘格

		public Room()
		{
			InitChess();  //创建Room需要初始化游戏数据
		}
		//MakeMove 0
		public async Task MakeMove(int id, CGPlayerCmd cmd) //有玩家走了一步,更新数据,广播给所有玩家,开始下一手
		{
			await _messageQueue;
			var pos = Convert.ToInt32(cmd.Cmd.Split(' ')[1]);
			chessState[pos] = id;

			curTurn++;
			var playerWho = curTurn % 2;
			var p = players[playerWho];


			var cmd2 = GCPlayerCmd.CreateBuilder();
			cmd2.Result = string.Format("MakeMove {0} {1}", id, pos);
			BroadcastToAll(cmd2);

			if (CheckWin())
			{
				await GameOver();
			}
			else {
				var cmd3 = GCPlayerCmd.CreateBuilder();
				cmd3.Result = string.Format("NewTurn {0} {1}", curTurn, p.Id);
				BroadcastToAll(cmd3);
			}
		}

		private bool CheckWin()
		{
			return false;
		}


		private async Task GameOver()
		{
            await _messageQueue;
			if (!isGameOver)
			{
				isGameOver = true;
				var cmd = GCPlayerCmd.CreateBuilder();
				cmd.Result = "GameOver";
				BroadcastToAll(cmd);

				foreach (var p in players)
				{
					await p.SetRoom(null);
				}

				ActorManager.Instance.RemoveActor(Id);  //删除该房间
			}
		}

		//中途离开房间
		public async Task Leave(int id)  //有玩家离开,游戏结束
		{
			await _messageQueue;
			await GameOver();
		}

		public async Task AddPlayer(int id1)    //加人
		{
			await _messageQueue;
			var player = ActorManager.Instance.GetActor(id1);
            //players.Add(id1, (PlayerActor)player);
            var p = (PlayerActor)player;
            players.Add(p);
            PlayerMap.Add(id1, p);
			//Room
			await p.SetRoom(this);
		}

		private void InitChess()  //初始化棋盘格
		{
			for (var i = 0; i < 9; i++)
			{
				chessState.Add(-1);
			}
			//0 1
		}

		public async Task GameStart() //游戏开始
		{
			await _messageQueue;
            Console.WriteLine($"GameStart RoomID : {Id}");

            var cmd = GCPlayerCmd.CreateBuilder();  //发送GameStart消息给所有玩家
            cmd.Result = "GameStart";
            BroadcastToAll(cmd);

            var cmd1 = GCPlayerCmd.CreateBuilder();  //第一手,放入玩家的唯一ID
            cmd1.Result = string.Format("NewTurn {0} {1}", curTurn, players[0].Id);
            BroadcastToAll(cmd1);

            //try
            //{
            //    var cmd1 = GCPlayerCmd.CreateBuilder();
            //    string str = "Position";
            //    foreach (var p in players)
            //    {
            //        str = $"{str} {p.Id} {p.playerState.px} {p.playerState.py} {p.playerState.hp}";
            //    }
            //    cmd1.Result = str;
            //    BroadcastToAll(cmd1);
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine(e);
            //}


            //RunTask(UpdateWorld);
        }

        private async Task UpdateWorld()  //主循环用于更新玩家位置
        {
            while (!isStop)
            {
                //每帧逻辑更新,技能处理
                List<int> deadBullet = new List<int>();
                foreach (var bulletPair in bulletMap)
                {
                    bulletPair.Value.Update();
                    if (bulletPair.Value.isDead)
                        deadBullet.Add(bulletPair.Key);
                }
                foreach (var bullet in deadBullet)
                {
                    bulletMap.Remove(bullet);
                }

                //同步状态给所有玩家:
                SyncWorldState();

                await Task.Delay(1000);
            }

            Console.WriteLine($"GameOver RoomID : {Id}");
        }

        private void SyncWorldState()//同步状态给所有玩家:
        {
            //玩家位置同步更新
            var cmd1 = GCPlayerCmd.CreateBuilder();
            string str = "Position";
            foreach (var p in players)
            {
                str = $"{str} {p.Id} {p.playerState.px} {p.playerState.py} {p.playerState.hp}";
            }
            cmd1.Result = str; 
            BroadcastToAll(cmd1);

            //技能位置同步
            var cmd2 = GCPlayerCmd.CreateBuilder();
            var str2 = "Bullet";
            foreach (var bulletPair in bulletMap)
            {
                str2 = $"{str2} {bulletPair.Value.id} {bulletPair.Value.px} {bulletPair.Value.py}";
            }
            cmd2.Result = str2;
            BroadcastToAll(cmd2);
        }



        private void BroadcastToAll(GCPlayerCmd.Builder cmd)  //广播消息
		{
			var parr = players;
			ServerBundle bundle;
			var bytes = ServerBundle.sendImmediateError(cmd, 0, 0, out bundle);
			ServerBundle.ReturnBundle(bundle);

			foreach (var p in parr)
			{
				p.GetAgent().SendBytes(bytes);
			}
		}

        public void CheckDamage(Bullet bullet)
        {

            foreach (var p in players)
            {
                if (p.playerState.px == bullet.px && p.playerState.py == bullet.py
                     && bullet.playerID != p.Id)
                {
                    p.playerState.hp = p.playerState.hp - 1;
                    bullet.isDead = true;
                }
            }
        }

        /// <summary>
        /// 0 right
        /// 1 up
        /// 2 left
        /// 3 down
        /// </summary>
        /// <param name="id"></param>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public async Task MoveTo(int id, CGPlayerCmd cmd)
        {
            await _messageQueue;
            var pos = Convert.ToInt32(cmd.Cmd.Split(' ')[1]);
            var player = PlayerMap[id];
            switch (pos)
            {
                case 0:
                    player.playerState.px += 1;
                    break;
                case 1:
                    player.playerState.py += 1;
                    break;              
                case 2:
                    player.playerState.px -= 1;
                    break;              
                case 3:
                    player.playerState.py -= 1;
                    break;
                default:
                    break;
            }

            //交由每帧的同步消息取去发送同步命令
            //var cmd3 = GCPlayerCmd.CreateBuilder();
            //cmd3.Result = $"MoveTo {id} {player.px} {player.py}";
            //BroadcastToAll(cmd3);
        }

        public async Task Skill(int id, CGPlayerCmd cmd)
        {
            await _messageQueue;

            var player = PlayerMap[id];
            var dir = Convert.ToInt32(cmd.Cmd.Split(' ')[1]);

            var bullet = new Bullet(this, id, player.playerState.px, player.playerState.py, dir);
            bulletMap.Add(bullet.id, bullet);
        }
    }
}
