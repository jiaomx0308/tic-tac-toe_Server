using System;
using System.Collections.Generic;
using SimpleJSON;

namespace MyLib 
{

    //一个获取ProtoBuff协议的类,通过生成的nameMap.json,获得modeid messageid和对应的名字
    public class SaveGame
    {
        public static SaveGame saveGame;
        public JSONClass msgNameIdMap;

        private Dictionary<string, Util.Pair> MsgNameToId= new Dictionary<string, Util.Pair>();
        private Dictionary<int, string> ModuleIdToName = new Dictionary<int, string>();
        private Dictionary<int, string> ModuleMsgIdToName = new Dictionary<int, string>(); 

        public SaveGame()
        {
            saveGame = this;
            var file = System.IO.File.ReadAllText("nameMap.json");
            msgNameIdMap = JSON.Parse(file).AsObject;
            InitMsgName();
        }

        private void InitMsgName()
        {
            foreach (KeyValuePair<string, JSONNode> m in msgNameIdMap)
            {
                var msgToId =  m.Value.AsObject;
                foreach (KeyValuePair<string, JSONNode> j  in msgToId)
                {
                    int a = msgToId["id"].AsInt;
                    int b = j.Value.AsInt;
                    MsgNameToId[j.Key] = new Util.Pair((byte)a, (byte)b);

                    var mmId = a << 8+b;
                    ModuleMsgIdToName[mmId] = j.Key;
                }
                ModuleIdToName[msgToId["id"].AsInt] = m.Key;
            }
        }

        public Util.Pair GetMsgID(string msgName) {
            /*
            foreach (KeyValuePair<string, JSONNode> m in msgNameIdMap) {
                if(m.Value[msgName] != null) {
                    int a = m.Value["id"].AsInt;
                    int b = m.Value[msgName].AsInt;
                    return new Util.Pair((byte)a, (byte)b);     
                }
            }
            */
            if (MsgNameToId.ContainsKey(msgName))
            {
                return MsgNameToId[msgName];
            }
            return null;
        }

        public string getModuleName(int moduleId) {
            Debug.Log ("find Module Name is "+moduleId);
            /*
            foreach (KeyValuePair<string, JSONNode> m in msgNameIdMap) {
                var job = m.Value.AsObject;
                if(job["id"].AsInt == moduleId) {
                    return m.Key;
                }
            }
            */
            if (ModuleIdToName.ContainsKey(moduleId))
            {
                return ModuleIdToName[moduleId];
            }
            Debug.Log ("name map file not found  ");
            return null;
        }

        public string getMethodName(int moduleId, int msgId) {
            /*
            var msgs = msgNameIdMap[module].AsObject;
            foreach (KeyValuePair<string, JSONNode> m in msgs) {
                if(m.Key != "id") {
                    if(m.Value.AsInt == msgId) {
                        return m.Key;
                    }
                }
            }
            */
            var mmId = moduleId << 8 + msgId;
            if (ModuleMsgIdToName.ContainsKey(mmId))
            {
                return ModuleMsgIdToName[mmId];
            }
            return null;
        }

    }
}

