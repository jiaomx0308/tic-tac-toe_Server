using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using SimpleJSON;
using Google.ProtocolBuffers;
using System.Threading;


namespace MyLib
{
	/// <summary>
	/// Gate--->Forward OpenAgent To Somebody 
	/// 单线程程序
	/// </summary>
	public class Agent
	{
		private static uint maxId = 0;
		public uint id;

		Socket mSocket;
		ServerMsgReader msgReader;
		private bool _isClose = false;

		public bool isClose
		{
			get { return _isClose; }
			set { _isClose = value; }
		}

		public Actor actor;

		public SocketServer server;

		List<MsgBuffer> msgBuffer = new List<MsgBuffer>();
		public EndPoint ep;
		private byte[] mTemp = new byte[0x2000];

		private ulong mReceivePacketCount;   //记录接收数据的次数
		private ulong mReceivePacketSizeCount;  //记录接受数据的大小
		private ulong mSendPacketCount;
		private ulong mSendPacketSizeCount;

		public UDPAgent udpAgent;

		public void SetUDPAgent(UDPAgent ud)
		{
			udpAgent = ud;
		}
		public Agent(Socket socket)  
		{
			socket.NoDelay = true;
			id = ++maxId;
			mSocket = socket;
			ep = mSocket.RemoteEndPoint;
			msgReader = new ServerMsgReader();
			msgReader.msgHandle = handleMsg;   //设置ServerMsgReader解析二进制数据后的处理函数
            Debug.Log("AgentCreate " + id);

			var ip = socket.RemoteEndPoint as IPEndPoint;
			LogHelper.LogClientLogin(string.Format("ip={0}", ip.Address));
		}

		private void Open(uint agentId, Agent agent)   //把PlayerActor和Agent进行绑定,并加入ActorManager,在监听完成后建立socket连接时调用
        {
			//await this._messageQueue;
			var act = new PlayerActor(agentId);
			LogHelper.Log("Agent", "CreateActor " + agentId);
			//如果连接已经关闭就不要把Actor加入到ActorManager 网络断开比较快
			if (!agent.isClose)
			{
				ActorManager.Instance.AddActor(act);
			}
		}
		private void ClosePlayerActor(uint agentId)
		{
		}

		public void StartReceiving()   //开始接收来自客户端的网络数据报文,并创建PlayerActor
		{
			if (mSocket != null && mSocket.Connected && !isClose)
			{
				try
				{
					//watchDog.Open(id, this);
					Open(id, this);    //创建PlayerActor,其和Agent应为一一对应的关系,id由Agent的static变量统一分配
					mSocket.BeginReceive(mTemp, 0, mTemp.Length, SocketFlags.None, OnReceive, mSocket);
				}
				catch (Exception exception)
				{
					LogHelper.Log("Agent", exception.ToString());
					Close();
				}
			}
		}

		public void handleMsg(KBEngine.Packet packet)  //如果该Agent由Actor的话,接收到解析后的packet消息后通过,Actor的SendMsg发送到重载的ReceiveMsg处理
        {
			if (actor != null)
			{
				actor.SendMsg(packet);
			}
			var proto = packet.protoBody as CGPlayerCmd;
			var cmd = proto.Cmd;
			var size = 2 + packet.msglen;
			mReceivePacketCount += 1;
			mReceivePacketSizeCount += (ulong)size;
			LogHelper.LogReceivePacket(string.Format("cmd={0} size={1}", cmd, size));
		}

		void OnReceive(IAsyncResult result)  //当接收完成,调用OnReceive时,其会通过ServerMsgReader的process对二进制报文进行解析,解析完成后会调用handleMsg进行进一步处理
        {
			int bytes = 0;
			if (mSocket == null)
			{
				LogHelper.Log("Error", "SocketClosed");
				Close();
				return;
			}
			try
			{
				bytes = mSocket.EndReceive(result);

			}
			catch (Exception exception)
			{
				Debug.LogError(exception.Message);
				Close();
			}
			if (bytes <= 0)
			{
				Debug.LogError("bytes " + bytes);
				Close();
			}
			else {
				//MessageReader
				//BeginReceive
				uint num = (uint)bytes;
				msgReader.process(mTemp, num);
				if (mSocket != null)
				{
					try
					{
						mSocket.BeginReceive(mTemp, 0, mTemp.Length, SocketFlags.None, OnReceive, mSocket);
					}
					catch (Exception exception2)
					{
						Util.Log(exception2.Message);
						Close();
					}
				}
			}
		}

		private int closeReq = 0;
		public void Close()  //断开该Agent的连接
		{
			if (Interlocked.Increment(ref closeReq) != 1)
			{
				return;
			}

			if (isClose)
			{
				return;
			}
			isClose = true;

			LogHelper.Log("Agent", "CloseAgent");
			if (mSocket != null && mSocket.Connected)
			{
				Debug.LogError("CloseSocket");
				try
				{
					mSocket.Shutdown(SocketShutdown.Both);
					mSocket.Close();
				}
				catch (Exception exception)
				{
					Debug.LogError(Util.FlattenException(exception));
					//Util.PrintStackTrace();
				}
			}
			mSocket = null;

			if (actor != null)
			{
				actor.SendMsg(string.Format("close"));
			}

			//watchDog.Close(id);
			ClosePlayerActor(id);
			if (mSocket != null)
			{
				var ip = mSocket.RemoteEndPoint as IPEndPoint;
				LogHelper.LogClientLogout(string.Format("ip={0}", ip.Address));
			}
			if (server != null)
			{
				server.RemoveAgent(this);
			}
			if (udpAgent != null)
			{
				udpAgent.Close();
			}
		}


		public void SendUDPBytes(byte[] bytes)
		{
			if (udpAgent != null && useUDP && !udpAgent.CheckClose())
			{
				mSendPacketCount++;
				mSendPacketSizeCount += (ulong)bytes.Length;
				udpAgent.SendBytes(bytes);
			}
			else
			{
				SendBytes(bytes);
			}
		}

		public void SendUDPPacket(IBuilderLite retpb, byte flowId, byte errorCode)
		{
			if (udpAgent != null && useUDP)
			{
				udpAgent.SendPacket(retpb);
			}
			else
			{
				SendPacket(retpb, flowId, errorCode);
			}
		}

		public void ForceUDP(IBuilderLite retpb, byte flowId, byte errorCode)
		{
			if (udpAgent != null)
			{
				udpAgent.SendPacket(retpb);
			}
		}

		public bool useUDP = false;
		public void UseUDP()
		{
			if (!lostYet)
			{
				useUDP = true;
			}
		}

		public bool useKCP = false;
		public void UseKCP()
		{
			useKCP = true;
		}

		/// <summary>
		/// KCP连接一旦断开 需要重发一些数据给客户端
		/// 通过TCP发送数据 reInit 协议重新初始化
		/// 跟随初始化数据
		/// </summary>
		public void KCPLost()
		{
			useKCP = false;
		}

		private bool lostYet = false;
		public void UDPLost()
		{
			lostYet = true;
			useUDP = false;
		}

		public void SendBytes(byte[] bytes)  //发送数据
		{
			mSendPacketCount += 1;
			mSendPacketSizeCount += (ulong)bytes.Length;

			var mb = new MsgBuffer() { position = 0, buffer = bytes, bundle = null };
			var send = false;
			lock (msgBuffer)
			{
				msgBuffer.Add(mb);
				if (msgBuffer.Count == 1)
				{
					send = true;
				}
			}
			if (send)
			{
				try
				{
					mSocket.BeginSend(mb.buffer, mb.position, mb.Size, SocketFlags.None, OnSend, null);
				}
				catch (Exception exception)
				{
					Debug.LogError(exception.Message);
					Close();
				}
			}
		}

		/// <summary>
		/// 内部Actor将Agent要发送的消息推送给客户端 
		/// SendPacket 应该以SendBuff行驶发送
		/// 同一个Socket的Write Read只能加入一次 epoll 
		/// Read在初始化的时候加入
		/// Write在每次要写入的时候加入
		/// </summary>
		public void SendPacket(IBuilderLite retpb, byte flowId, byte errorCode)  //序列化Protobuff后发送数据
		{
			if (isClose)
			{
				return;
			}

			var proto = retpb as GCPlayerCmd.Builder;
			var result = proto.Result;
			ServerBundle bundle;
			var bytes = ServerBundle.sendImmediateError(retpb, flowId, errorCode, out bundle);
			//Debug.Log ("SendBytes: " + bytes.Length);
			mSendPacketCount += 1;
			mSendPacketSizeCount += (ulong)bytes.Length;
			LogHelper.LogSendPacket(string.Format("actor={0} result={1} size={2}", id, result, bytes.Length));

			var mb = new MsgBuffer() { position = 0, buffer = bytes, bundle = bundle };
			var send = false;
			lock (msgBuffer)
			{
				msgBuffer.Add(mb);
				if (msgBuffer.Count == 1)   //当msgBuffer为空并新加入一个消息时,我们才开始发送,不然在OnSend的回调中一直在发送buff,两边同时发送会有问题,
                {
					send = true;
				}
			}
			if (send)
			{
				try
				{
					mSocket.BeginSend(mb.buffer, mb.position, mb.Size, SocketFlags.None, OnSend, null);
				}
				catch (Exception exception)
				{
					Debug.LogError(exception.Message);
					Close();
				}
			}
		}

		private void OnSend(IAsyncResult result)
		{
			int num = 0;  //发送的字节数
			try
			{
				num = mSocket.EndSend(result);
			}
			catch (Exception exception)
			{
				num = 0;
				Close();
				Debug.LogError(exception.Message);
				return;
			}

			if (mSocket != null && mSocket.Connected)
			{
				MsgBuffer mb = null;
				lock (msgReader)
				{
					mb = msgBuffer[0];
				}
				MsgBuffer nextBuffer = null;
				if (mb.Size == num)  //发送成功
				{
					lock (msgBuffer)
					{
						msgBuffer.RemoveAt(0);
						if (msgBuffer.Count > 0)
						{
							nextBuffer = msgBuffer[0];
						}
					}
					ServerBundle.ReturnBundle(mb.bundle);
				}
				else if (mb.Size > num)  //发送了一部分,还有一部分没发
				{
					mb.position += num;
					nextBuffer = msgBuffer[0];
				}
				else  //出错了
				{
					ServerBundle.ReturnBundle(mb.bundle);
					lock (msgBuffer)
					{
						msgBuffer.RemoveAt(0);
						if (msgBuffer.Count > 0)
						{
							nextBuffer = msgBuffer[0];
						}
					}
				}

				if (nextBuffer != null)
				{
					try
					{
						mSocket.BeginSend(nextBuffer.buffer, nextBuffer.position, nextBuffer.Size, SocketFlags.None,
							new AsyncCallback(OnSend), null);   //这里一直同步调用BeginSend和OnSend,直至nextBuffer == null,也就是所有消息全部发送完成,msgBuffer.Count == 0
                    }
					catch (Exception exception)
					{
						Debug.LogError(exception.Message);
						Close();
					}
				}

			}
			else
			{
				Close();
			}
		}

		public JSONClass GetJsonStatus()
		{
			var sj = new SimpleJSON.JSONClass();

			var jsonObj = new JSONClass();
			jsonObj.Add("id", new JSONData(id));
			if (mSocket != null)
			{
				var ip = mSocket.RemoteEndPoint as IPEndPoint;
				jsonObj.Add("ip", new JSONData(ip.ToString()));
				jsonObj.Add("Active", new JSONData("true"));
				jsonObj.Add("ReceivePackets", new JSONData(mReceivePacketCount));
				jsonObj.Add("ReceivePacketsSize", new JSONData(mReceivePacketSizeCount));
				jsonObj.Add("SendPackets", new JSONData(mSendPacketCount));
				jsonObj.Add("SendPacketsSize", new JSONData(mSendPacketSizeCount));
				jsonObj.Add("MsgQueueLength", new JSONData(msgBuffer.Count));
			}
			else
			{
				jsonObj.Add("Active", new JSONData("false"));
			}

			sj.Add("Agent", jsonObj);
			return sj;
		}
	}

}
