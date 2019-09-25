using System;
using System.Collections;
using Google.ProtocolBuffers;

namespace KBEngine
{
	//Response Packet Format
	public class Packet
	{
		public System.UInt16 msglen = 0;
		public byte flowId;
		public byte moduleId;
		public byte msgid = 0;
		public byte responseFlag;
		public IMessageLite protoBody;

		public Packet(ushort len, byte fid, byte module, byte msgid, byte resflag, IMessageLite pb) {
			//Debug.Log ("receive packet" );
			msglen = len;
			flowId = (byte)fid;
			moduleId = module;
			this.msgid = msgid;
			responseFlag = resflag;
			protoBody = pb;
            MyLib.Util.Log("Packet:: readPacket "+fid);
            MyLib.Util.Log("Packet:: readPacket " + protoBody.GetType ().FullName);
		}

		public Packet() {
		}
	}

	public class PacketHolder{
		public Packet packet;
	}

}
