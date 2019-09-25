using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Google.ProtocolBuffers;
using System.Diagnostics;
using System.Text;
using log4net;
using SimpleJSON;

namespace MyLib
{
	public class MsgBuffer
	{
		public int position = 0;
		public System.Byte[] buffer;
		public ServerBundle bundle;
		public IPEndPoint remoteEnd;

		public int Size
		{
			get
			{
				return buffer.Length - position;
			}
		}
	}



	/// <summary>
	/// Socket服务器
	/// EventLoop 启动
	/// 分发 确保在SocketServer所在的线程安全么？
	/// 避免线程全部使用Message投递机制 Actor的Message
	/// 
	/// Actor 要比较简单的调用另外一个Actor的方法
	/// 将方法调用转化为Message发送
	///     隐藏：PushMessage和HandlerMsg的代码
	/// 
	/// HandlerMsg可以在类初始化的时候构建Method到Msg映射
	/// 通过Attribute调用方法的时候自动调用SendMsg方法 最后再调用实际的方法
	/// </summary>
	public class SocketServer : Actor
	{
		TcpListener mListener;  //侦听的 TCP 网络客户端的连接
        int mListenerPort;

		public Thread mThread;
		public Dictionary<uint, Agent> agents = new Dictionary<uint, Agent>();

		public bool AcceptConnnection = true;

		private UdpClient udpClient;


		public int AgentCount
		{
			get
			{
				var count = 0;
				lock (agents)
				{
					count = agents.Count;
				}
				return count;
			}
		}

		public bool Start(int tcpPort)   //建立TCP和UDP连接
		{
			LogHelper.Log("Server", "ServerPort: " + tcpPort);
			try
			{
				mListenerPort = tcpPort;
				mListener = new TcpListener(IPAddress.Any, tcpPort);
				mListener.Server.NoDelay = true;
				mListener.Start(50);   //开始监听连接,socket bind listener,  //这里设置了监听数量为50个
			}
			catch (Exception exception)
			{
				//Util.Log (exception.Message);
				LogHelper.Log("Error", exception.Message);
				return false;
			}

			var udpPort = ServerConfig.instance.configMap["UDPPort"].AsInt;
			LogHelper.Log("UDP", "UDPStart: " + udpPort);
			remoteUDPPort = new IPEndPoint(IPAddress.Any, udpPort);  //从ServerConfig.json读取的udpPort
            udpClient = new UdpClient(remoteUDPPort);
			udpClient.BeginReceive(OnReceiveUDP, null);  //以异步方式从远程主机接收数据报。OnReceiveUDP为接收到消息后的处理函数

            //Debug.Log ("GetWatchDog " + dog);
            LogHelper.Log("Actor", "ServerStartSuc");
			mThread = new Thread(new ThreadStart(this.ThreadFunction));
			mThread.Start();
			return true;
		}

		private Dictionary<IPEndPoint, UDPAgent> udpAgents = new Dictionary<IPEndPoint, UDPAgent>();

		private IPEndPoint remoteUDPPort;
		private void OnReceiveUDP(IAsyncResult result)//接收到UDP消息
		{
			if (udpClient == null)
			{
				return;
			}
			try
			{
				var udpPort = new IPEndPoint(IPAddress.Any, 0);
				var bytes = udpClient.EndReceive(result, ref udpPort);
				if (bytes.Length > 0)
				{
					UDPAgent ag1 = null;
					lock (udpAgents)
					{
						//远程客户端不支持UDP连接 网络无法连接上 UDP穿透失败
						if (!udpAgents.ContainsKey(udpPort))
						{
							var ag = new UDPAgent(udpPort, this, udpClient);
							udpAgents.Add(udpPort, ag);
						}
						ag1 = udpAgents[udpPort];
					}
					if (ag1 != null)
					{
						ag1.ReceiveData(bytes);
					}
				}
				else
				{
					LogHelper.Log("UDP", "Error Receive 0");
				}

				udpClient.BeginReceive(OnReceiveUDP, null);  //开启下一次UDP消息连接
			}
			catch (Exception exp)
			{
				LogHelper.Log("Error", exp.ToString());
			}
		}

		private Queue<MsgBuffer> msgBuffers = new Queue<MsgBuffer>();
		public void SendUDPPacket(MsgBuffer mb)
		{
			var send = false;
			lock (msgBuffers)
			{
				msgBuffers.Enqueue(mb);
				if (msgBuffers.Count == 1)
				{
					send = true;
				}
			}
			if (send)
			{
				try
				{
					udpClient.BeginSend(mb.buffer, mb.buffer.Length, mb.remoteEnd, OnSend, null);
				}
				catch (Exception exp)
				{
					LogHelper.Log("UDP", exp.ToString());
					DequeueMsg();
				}
			}
		}

		private void DequeueMsg()
		{
			lock (msgBuffers)
			{
				if (msgBuffers.Count > 0)
				{
					msgBuffers.Dequeue();
				}
			}
		}

		private void OnSend(IAsyncResult result)
		{
			bool error = false;
			try
			{
				udpClient.EndSend(result);
			}
			catch (Exception exp)
			{
				LogHelper.Log("Error", exp.ToString());
				DequeueMsg();
				error = true;
			}

			if (udpClient != null)
			{
				MsgBuffer nextBuffer = null;
				lock (msgBuffers)
				{
					if (!error)
					{
						msgBuffers.Dequeue();
					}
					if (msgBuffers.Count > 0)
					{
						nextBuffer = msgBuffers.Peek();
					}
				}

				if (nextBuffer != null)
				{
					try
					{
						udpClient.BeginSend(nextBuffer.buffer, nextBuffer.buffer.Length, nextBuffer.remoteEnd, OnSend, null);
					}
					catch (Exception exp)
					{
						LogHelper.Log("UDP", exp.ToString());
						DequeueMsg();
					}
				}
			}
		}



		void AddAgent(Socket socket)  //每次建立连接使,把socket加入agents的字典统一管理
        {
			var item = new Agent(socket);
			item.server = this;
			lock (agents)
			{
				agents.Add(item.id, item);
			}
			item.StartReceiving();  //开始接收数据
		}

		public void RemoveAgent(Agent agent)  //在字典中删除Agent
        {
			lock (agents)
			{
				agents.Remove(agent.id);
			}
			//RemoveUdpAgent(agent.udpAgent);
		}

		public void RemoveUdpAgent(UDPAgent agent)
		{
			lock (udpAgents)
			{
				udpAgents.Remove(agent.remoteEnd);
			}
		}

		public Agent GetAgent(uint agentId)
		{
			Agent agent = null;
			lock (agents)
			{
				var ok = agents.TryGetValue(agentId, out agent);
			}
			return agent;
		}

		private void AcceptCallback(IAsyncResult result)  //接收到Tcp消息后的处理回调,异步调用,开的另外一个线程,和调用线程无关
		{
            try
			{
				var listener = (TcpListener)result.AsyncState;
				var socket = listener.EndAcceptSocket(result); //异步接受传入的连接尝试，并创建一个新 System.Net.Sockets.Socket 来处理远程主机通信
                AddAgent(socket); //把socket加入到agents的字典中,然后等待接收消息
                listener.BeginAcceptSocket(new AsyncCallback(AcceptCallback), listener);  //开启下一次异步监听等待
			}
			catch (Exception exp)
			{
				LogHelper.Log("Error", "Accept SocketError: " + exp.ToString());
			}
		}
		private ManualResetEvent signal = new ManualResetEvent(false);

		void ThreadFunction() //异步等待接收Tcp消息,然后等待
		{
			mListener.BeginAcceptSocket(new AsyncCallback(AcceptCallback), mListener);  //BeginAcceptSocket是调用了线程池中的线程去等待,而AsyncCallback是在等待的线程中去执行的,和本调用线程没关系
            signal.WaitOne();  //在这一直等待的是为了防止主线程退出
			/*
			while (!isStop) {
				if (this.mListener != null && mListener.Pending ())
				{
					var socket = mListener.AcceptSocket ();
					AddAgent (socket);
				}
				Thread.Sleep (1);
			}
             */
		}

		public override void Stop()
		{
			base.Stop();
			signal.Set();
		}

		public override string ToString()
		{
			var sj = new SimpleJSON.JSONClass();

			var jsonObj = new JSONClass();
			jsonObj.Add("AgentCount", new JSONData(AgentCount));

			var jsonArray = new JSONArray();
			lock (agents)
			{
				foreach (var agent in agents)
				{
					jsonArray.Add("Agent", agent.Value.GetJsonStatus());
				}
			}
			jsonObj.Add("Agents", jsonArray);
			sj.Add("AgentStatus", jsonObj);
			return sj.ToString();
		}
	}
}
