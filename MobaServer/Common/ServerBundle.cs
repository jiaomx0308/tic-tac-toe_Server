using System.Collections;
using Google.ProtocolBuffers;
using System.Collections.Generic;
using System;

namespace MyLib
{
	public class ServerBundle 
	{
        private static Queue<ServerBundle> serverBundlePool = new Queue<ServerBundle>();

	    private static ServerBundle GetBundle()
	    {
	        lock (serverBundlePool)
	        {
	            if (serverBundlePool.Count > 0)
	            {
	                var q = serverBundlePool.Dequeue();
	                return q;
	            }
	            else
	            {
	                return new ServerBundle();
	            }
	        }
	    }

	    public ServerBundle()
	    {
	        coutStream = CodedOutputStream.CreateInstance(simpleMemoryStream);
	    }

	    public static void ReturnBundle(ServerBundle bundle)
	    {
	        if (bundle == null)
	        {
	            return;
	        }
	        lock (serverBundlePool)
	        {
                bundle.Reset();
	            serverBundlePool.Enqueue(bundle);
	        } 
	    }

		KBEngine.MemoryStream stream = new KBEngine.MemoryStream();
		public KBEngine.Message msgtype = null;
		public byte moduleId;
		public byte msgId;
		public System.Byte flowId;

	    private void Reset()
	    {
	        moduleId = 0;
	        msgId = 0;
	        msgtype = null;
	        flowId = 0;
            stream.Reset();
	    }

		void newMessage(System.Type type) {
			Debug.Log ("ServerBundle:: 开始发送消息 Message is " + type.Name);
			var pa = Util.GetMsgID (type.Name);
            if(pa == null) {
                Debug.LogError("GetMessage Id Error, please Update NameMap.json "+type.Name);
            }
			moduleId = pa.moduleId;
			msgId = pa.messageId;
			
			msgtype = null;
		}

		byte writePB(SimpleMemoryStream v, byte errorCode=0) {

			UInt16 bodyLength = (UInt16)(1 + 1 + 1+ 1 + v.Length);
			int totalLength = 2 + bodyLength;
			//checkStream (totalLength);
			//LogHelper.Log("Packet", "ServerBundle::writePB pack data is "+bodyLength+" pb length "+v.Length+" totalLength "+totalLength);
			//Debug.Log ("ServerBundle::writePB module Id msgId " + moduleId+" "+msgId);
			//stream.writeUint8 (Convert.ToByte(0xcc));
			stream.writeUint16(Convert.ToUInt16(bodyLength));
            //服务端客户端需要检测FlowId 不要超出范围  
			stream.writeUint8(Convert.ToByte(flowId));
			stream.writeUint8(Convert.ToByte(moduleId));
			stream.writeUint8 (Convert.ToByte(msgId));
			//stream.writeUint32 (Convert.ToUInt32 (123));//response time
			stream.writeUint8 (Convert.ToByte(errorCode)); // no error reponse flag
			stream.writePB (v);
			
			return flowId;
		}

        private SimpleMemoryStream simpleMemoryStream = new SimpleMemoryStream();
	    private CodedOutputStream coutStream;

		uint writePB(IMessageLite pbMsg, byte errorCode=0) {
			Debug.Log ("WritePB: "+pbMsg);
			//byte[] bytes;
            /*
			using (System.IO.MemoryStream stream = new System.IO.MemoryStream()) {
				pbMsg.WriteTo (stream);
				bytes = stream.ToArray ();
			}
            */
            simpleMemoryStream.Reset();
		    //CodedOutputStream coutStream = CodedOutputStream.CreateInstance(simpleMemoryStream);
            pbMsg.WriteTo(coutStream);
            coutStream.Flush();
			return writePB (simpleMemoryStream, errorCode);
		}

        public static byte[] sendImmediateError(IBuilderLite build, byte flowId, byte errorCode, out ServerBundle b) {
            var data = build.WeakBuild ();

            //var bundle = new ServerBundle ();
            var bundle = GetBundle();
            b = bundle;
            bundle.newMessage (data.GetType());
            bundle.flowId = flowId;
            bundle.writePB (data, errorCode);
            return bundle.stream.getbuffer();
        }

	}
}