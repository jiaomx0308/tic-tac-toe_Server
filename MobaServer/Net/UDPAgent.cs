using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.ProtocolBuffers;
using KBEngine;

namespace MyLib 
{
    /// <summary>
    /// 客户端发送TCP 连接
    /// 客户端发送第一个UDP报文到服务器
    /// 客户端和服务器构建了UDP连接
    /// 
    /// 服务器更新位置发送UDP报文到客户端
    /// 如果没有建立UDP连接则 发送TCP报文到服务器
    /// 
    /// 客户端Agent Close的时候 关闭PlayerActor UDPAgent
    /// UDPAgent 长时间没有接受报文则PlayerActor会自己断开服务器连接
    /// 
    /// 
    /// </summary>
    public class UDPAgent
    {
        public IPEndPoint remoteEnd; //不知道对方的remoteEnd 需要等对方连接上才可以
        private ServerMsgReader msgReader;
        private Agent agent;
        private SocketServer socketServer;
        private UdpClient udpClient;
        private PlayerActor playerActor;

        //private Queue<MsgBuffer> msgBuffers = new Queue<MsgBuffer>();
 
        //自己创建连接的概念 同一个Server IP Port
        public UDPAgent(IPEndPoint endPoint, SocketServer ss, UdpClient client)
        {
            //udpClient = client;
            remoteEnd = endPoint;
            socketServer = ss;
            msgReader = new ServerMsgReader();
            msgReader.msgHandle = HandleMsg;

            udpClient = client;
            LogHelper.Log("UDP", "AddUDPAgent: "+endPoint);
        }

        private void HandleMsg(Packet packet)
        {
            //LogHelper.Log("UDP", "Receive Packet: "+packet.protoBody);
            if (agent == null)
            {
                var cg = packet.protoBody as CGPlayerCmd;
                var playerId = cg.AvatarInfo.Id;
                var actor = ActorManager.Instance.GetActor(playerId);
                if (actor != null)
                {
                    var ap = actor as PlayerActor;
                    if (ap != null)
                    {
                        var ag = ap.GetAgent();
                        ag.SetUDPAgent(this);
                        agent = ag;
                        playerActor = ap;
                    }
                }
            }

            if (agent != null)
            {
                agent.handleMsg(packet);
            }
            else
            {
                Close();
            }
        }

        public double lastReceiveTime = 0;
        public void ReceiveData(byte[] bytes)
        {
            lastReceiveTime = Util.GetTimeNow();
            //阻塞处理 线程安全 一个一个接受
            msgReader.process(bytes, (uint)bytes.Length);
        }

        public void SendBytes(byte[] bytes)
        {
            if (udpClient == null)
            {
                return;
            }
            if (IsClose)
            {
                return;
            }
	        var mb = new MsgBuffer() {position = 0, buffer = bytes, bundle = null, remoteEnd = remoteEnd};
            socketServer.SendUDPPacket(mb);
        }

        public void SendPacket(IBuilderLite retpb)
        {
            if (udpClient == null)
            {
                return;
            }
            if (IsClose)
            {
                return;
            }

            //LogHelper.Log("UDP", "SendPacket: "+retpb.ToString());

	        ServerBundle bundle;
	        var bytes = ServerBundle.sendImmediateError(retpb, 0, 0, out bundle);
            ServerBundle.ReturnBundle(bundle);

	        var mb = new MsgBuffer() {position = 0, buffer = bytes, bundle = bundle, remoteEnd = remoteEnd};
            socketServer.SendUDPPacket(mb);
        }


        private int closeRef = 0;
        private bool IsClose = false;
		public bool CheckClose()
		{
			return IsClose;
		}
        public void Close()
        {
            if (Interlocked.Increment(ref closeRef) > 1)
            {
                return;
            }

            LogHelper.Log("UDP", "Close: "+remoteEnd);
            if (IsClose)
            {
                return;
            }
            IsClose = true;
			socketServer.RemoveUdpAgent(this);
            udpClient = null;
        }
    }
}
