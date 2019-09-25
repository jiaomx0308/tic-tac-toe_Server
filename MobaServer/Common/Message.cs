using Google.ProtocolBuffers;

namespace KBEngine
{
	using System; 
	using System.Collections; 
	using System.Collections.Generic;
	
	using MessageID = System.UInt16;
	using MyLib;
	
    public class Message 
    {
		public static IMessageLite handlePB(byte moduleId, System.UInt16 msgId, MemoryStream msgstream) {
			IMessageLite msg =  Util.GetMsg (moduleId, msgId, msgstream.getBytString());
			return msg;
		}
    }
} 
