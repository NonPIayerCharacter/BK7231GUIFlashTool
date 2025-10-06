﻿using System;
using System.IO;
using System.Net;
using System.Net.Configuration;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

namespace BK7231Flasher
{
    public delegate void ProcessJSONReply(OBKDeviceAPI self);
    public delegate void ProcessCMDReply(OBKDeviceAPI self, JsonObject reply, string replyText);
    public delegate void ProcessBytesReply(byte[] data, int dataLen);
    public delegate void ProcessProgress(int done, int total);

    public class OBKDeviceAPI
    {
        int userIndex;
        string adr;
        JsonObject info;
        JsonObject status;
        JsonObject statusSTS;
        bool bGetInfoFailed;
        bool bTasmota;
        bool bGetInfoSuccess = false;
        int powerCount;
        int webRequestTimeOut = 3000;
        string userName, password;

        class GetFlashChunkArguments
        {
            public int adr;
            public int size;
            public ProcessBytesReply cb;
            public ProcessProgress cb_progress;
        }
        class SendCmndArguments
        {
            public string cmnd;
            public ProcessCMDReply cb;
        }
        
        internal string getInfoText()
        {
            string r = "";
            r += "Chipset = " + this.getChipSet() + Environment.NewLine;
           r += "ShortName = " + this.getShortName() + Environment.NewLine;
           r += "Build = " + this.getBuild() + Environment.NewLine;
           r += "MQTTHost = " + this.getMQTTHost() + Environment.NewLine;
           r += "IP = " + this.getAdr() + Environment.NewLine;
           r += "MQTTTopic = " + this.getMQTTTopic() + Environment.NewLine;
            JsonObject json = this.getInfo();
            if (json != null)
            {
               r += "MAC = " + json["mac"] + Environment.NewLine;
               r += "WebApp = " + json["webapp"] + Environment.NewLine;
               r += "Uptime = " + json["uptime_s"] + " seconds" + Environment.NewLine;
            }
            return r;
        }

        public void setWebRequestTimeOut(int t)
        {
            this.webRequestTimeOut = t;
        }
        public void setUserIndex(int i)
        {
            userIndex = i;
        }
        public int getUserIndex()
        {
            return userIndex;
        }
        public void setAdr(string s)
        {
            adr = s;
        }
        public OBKDeviceAPI()
        {
            this.adr = "";
        }
        public OBKDeviceAPI(string na)
        {
            this.adr = na;
        }
        internal void sendGetFlashChunk_TuyaCFGFromOBKDevice(ProcessBytesReply cb, ProcessProgress cb_progress)
        {
            var bkType = this.getBKType();
            this.sendGetFlashChunk(cb, cb_progress, TuyaConfig.getMagicOffset(bkType), TuyaConfig.getMagicSize(bkType));
        }
        internal void sendGetFlashChunk_OBKConfig(ProcessBytesReply cb, ProcessProgress cb_progress)
        {
            var bkType = this.getBKType();
            int ofs = OBKFlashLayout.getConfigLocation(bkType, out var sectors);
            this.sendGetFlashChunk(cb, cb_progress, ofs, sectors * BK7231Flasher.SECTOR_SIZE);
        }


        // Helper method to read the response stream fully into a byte array
        static byte[] ReadFully(Stream stream)
        {

            using (MemoryStream memoryStream = new MemoryStream())
            {
                byte[] buffer = new byte[4096];
                int bytesRead;

                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    memoryStream.Write(buffer, 0, bytesRead);
                }

                return memoryStream.ToArray();
            }
        }
        // Enable/disable useUnsafeHeaderParsing.
        // See http://o2platform.wordpress.com/2010/10/20/dealing-with-the-server-committed-a-protocol-violation-sectionresponsestatusline/
        public static bool ToggleAllowUnsafeHeaderParsing(bool enable)
        {
            //Get the assembly that contains the internal class
            Assembly assembly = Assembly.GetAssembly(typeof(SettingsSection));
            if (assembly != null)
            {
                //Use the assembly in order to get the internal type for the internal class
                Type settingsSectionType = assembly.GetType("System.Net.Configuration.SettingsSectionInternal");
                if (settingsSectionType != null)
                {
                    //Use the internal static property to get an instance of the internal settings class.
                    //If the static instance isn't created already invoking the property will create it for us.
                    object anInstance = settingsSectionType.InvokeMember("Section",
                    BindingFlags.Static | BindingFlags.GetProperty | BindingFlags.NonPublic, null, null, new object[] { });
                    if (anInstance != null)
                    {
                        //Locate the private bool field that tells the framework if unsafe header parsing is allowed
                        FieldInfo aUseUnsafeHeaderParsing = settingsSectionType.GetField("useUnsafeHeaderParsing", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (aUseUnsafeHeaderParsing != null)
                        {
                            aUseUnsafeHeaderParsing.SetValue(anInstance, enable);
                            return true;
                        }

                    }
                }
            }
            return false;
        }

        internal BKType getBKType()
        {
            string cs = getChipSet();
            switch(cs)
            {
                case "BK7231T":
                    return BKType.BK7231T;
                case "BK7231U":
                    return BKType.BK7231U;
                case "BK7231N":
                    return BKType.BK7231N;
                case "BK7236":
                    return BKType.BK7236;
                case "BK7238":
                    return BKType.BK7238;
                case "BK7252":
                    return BKType.BK7252;
                case "BK7252N":
                    return BKType.BK7252N;
                case "BK7258":
                    return BKType.BK7258;
                case "RTL8720D":
                    return BKType.RTL8720D;
                case "LN882H":
                    return BKType.LN882H;
                default:
                    return BKType.Invalid;
            }
        }

        private byte []sendGetInternal(string path)
        {
#if true
            try
            {
                string fullRequestText = "http://" + adr + path;
                WebRequest request = WebRequest.Create(fullRequestText);
                request.Timeout = webRequestTimeOut;
                if (!ToggleAllowUnsafeHeaderParsing(true))
                {
                    // Couldn't set flag. Log the fact, throw an exception or whatever.
                }
                using (WebResponse response = request.GetResponse())
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        byte[] buffer = ReadFully(stream);
                        return buffer;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
            }
            return null;
#else
            byte[] ret = null;
            try
            {
                using (var tcp = new TcpClient(adr, 80))
                using (var stream = tcp.GetStream())
                {
                    tcp.SendTimeout = 500;
                    tcp.ReceiveTimeout = 1000;
                    // Send request headers
                    var builder = new StringBuilder();
                    builder.AppendLine("GET " + path + " HTTP/1.1");
                    //builder.AppendLine("Host: any.com");
                    //builder.AppendLine("Content-Length: " + data.Length);   // only for POST request
                    builder.AppendLine("Connection: close");
                    builder.AppendLine();
                    var header = Encoding.ASCII.GetBytes(builder.ToString());
                    stream.Write(header, 0, header.Length);
                    // receive data
                    using (var memory = new MemoryStream())
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            memory.Write(buffer, 0, bytesRead);
                        }
                        memory.Position = 0;
                        byte[] tmp = new byte[16];
                        byte[] data = memory.ToArray();
                        File.WriteAllBytes("lastPacketHTTP.bin", data);
                        int index = BinaryMatch(data, Encoding.ASCII.GetBytes("\r\n\r\n")) + 4;
                        string headers = Encoding.ASCII.GetString(data, 0, index);
                        bool bIsChunked = headers.IndexOf("chunked") != -1;
                        int totalLen = 0;
                        if (bIsChunked)
                        {
                            MemoryStream merged = new MemoryStream();
                            BinaryWriter bw = new BinaryWriter(merged);
                            int cur = index;
                            int next;
                            while (true)
                            {
                                next = MiscUtils.indexOf(data, new byte[] { 0x0D, 0x0A }, cur);
                                string lenStr = Encoding.ASCII.GetString(data, cur, next - cur);
                                int len = int.Parse(lenStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                                if(len <= 0)
                                {
                                    break;
                                }
                                next += 2;
                                bw.Write(data, next, len);
                                cur = next + len;
                                cur += 2;
                            }
                            return merged.GetBuffer();
                        }
                        else
                        {
                            memory.Position = index;

                            ret = MiscUtils.subArray(data, index, data.Length - index);
                        }
                    }
                }
            }
            catch(Exception ex)
            {

            }
            return ret;
#endif
        }

        internal bool hasAdr(string s)
        {
            return this.adr == s;
        }

        internal string getAdr()
        {
            return adr;
        }
        internal string getJsonObjectSafe(JsonObject o, string parent, string key, string def = "")
        {
            if (o == null)
                return def;
            JsonObject o2;
            if(parent.Length == 0)
            {
                o2 = o;
            }
            else
            {
                if (o.TryGetPropertyValue(parent, out var o2t) == false)
                {
                    return def;
                }
                o2 = (JsonObject)o2t;
            }

            if (o2.TryGetPropertyValue(key, out var shortNameToken))
            {
                return shortNameToken.ToString();
            }
            else
            {
                return def;
            }
        }
        internal string getInfoObjectSafe(string key, string def = "")
        {
            if (info == null)
                return def;
            if (info.TryGetPropertyValue(key, out var shortNameToken))
            {
                return shortNameToken.ToString();
            }
            else
            {
                return def;
            }
        }
        internal string getMQTTHost()
        {
            if (info == null)
            {
                return "";
            }
            return getInfoObjectSafe("mqtthost");
        }
        internal string getMQTTTopic()
        {
            if (info == null)
            {
                return getJsonObjectSafe(status, "Status", "Topic");
            }
            return getInfoObjectSafe("mqtttopic");
        }
        internal string getShortName()
        {
            if(info == null)
            {
                return getJsonObjectSafe(status,"Status","DeviceName");
            }
            return getInfoObjectSafe("shortName");
        }
        internal bool hasShortName()
        {
            string s = getShortName();
            if (s.Length == 0)
                return false;
            return true;
        }
        internal string getSDK()
        {
            return getJsonObjectSafe(status, "StatusFWR", "SDK");
        }
        internal string getChipSet()
        {
            if (info == null)
            {
                return getJsonObjectSafe(status, "StatusFWR", "Hardware");
            }
            return getInfoObjectSafe("chipset");
        }

        internal void setUser(string text)
        {
            this.userName = text;
        }
        internal void setPassword(string text)
        {
            this.password = text;
        }
        internal string getMAC()
        {
            if (info == null)
            {
                return getJsonObjectSafe(status, "StatusNET", "Mac");
            }
            return getInfoObjectSafe("mac");
        }
        internal string getMACLast3BytesText()
        {
            string mac = getMAC();
            mac = mac.Replace(":", "");
            return mac.Substring(mac.Length - 6);
        }
        internal string getBuild()
        {
            if (info == null)
            {
                return getJsonObjectSafe(status, "StatusFWR", "BuildDateTime")
                    + " " + getJsonObjectSafe(status, "StatusFWR", "Core");
            }
            string r = getInfoObjectSafe("build");
            r = r.Replace("Build on ","");
            return r;
        }
        public bool hasBasicInfoReceived()
        {
            if (this.bGetInfoSuccess)
                return true;
            return false;
        }
        public bool getInfoFailed()
        {
            return bGetInfoFailed;
        }
        internal JsonObject getInfo()
        {
            return info;
        }
        public int getPowerSlotsCount()
        {
            return powerCount;
        }
        internal void clear()
        {
            statusSTS = null;
            powerCount = 0;
            adr = "";
            info = null;
            bGetInfoFailed = false;
            bGetInfoSuccess = false;
        }

        private string sendGet(string path)
        {
            byte[] res = sendGetInternal(path);
            string sResult = "";
            if (res != null)
            {
                sResult = Encoding.ASCII.GetString(res);
            }
            return sResult;
        }
        private JsonObject sendGenericJSONGet(string path, out string jsonText)
        {
            jsonText = sendGet(path);
            JsonObject jsonObject = null;
            try
            {
                int lastBraceIndex = jsonText.LastIndexOf('}');
                if (lastBraceIndex >= 0)
                {
                    jsonText = jsonText.Substring(0, lastBraceIndex + 1);
                }
                File.WriteAllText("lastHTTPJSONtext.txt", jsonText);
                // RTL8710B returns 0. for %f printf
                jsonText = jsonText.Replace("0.,", "0.0,");
                jsonText = jsonText.Replace("0.}", "0.0}");
                jsonObject = (JsonObject)JsonNode.Parse(jsonText);
            }
            catch (Exception ex)
            {

            }
            return jsonObject;
        }
        string escape(string s)
        {
            s = Uri.EscapeDataString(s);
            return s;
        }
        public bool hasDimmerSupport()
        {
            if(statusSTS == null)
            {
                return false;
            }
            if (statusSTS.ContainsKey("Dimmer"))
            {
                return true;
            }
            return false;
        }
        public bool hasColorSupport()
        {
            if (statusSTS == null)
            {
                return false;
            }
            if (statusSTS.ContainsKey("HSBColor"))
            {
                return true;
            }
            return false;
        }
        public bool hasCTSupport()
        {
            if (statusSTS == null)
            {
                return false;
            }
            if (statusSTS.ContainsKey("CT"))
            {
                return true;
            }
            return false;
        }
        string getBaseCmndString()
        {
            string r = "cm?";
            if (userName != null)
            {
                if (userName.Length > 0)
                {
                    r += "user=" + userName + "&";
                }
            }
            if (password != null)
            {
                if (password.Length > 0)
                {
                    r += "password=" + password + "&";
                }
            }
            r += "cmnd=";
            return r;
        }
        public void ThreadSendCmnd(object o)
        {
            SendCmndArguments arg = (SendCmndArguments)o;
            // format cm?cmnd= string 
            string replyStr;
            JsonObject jsonObject = sendGenericJSONGet("/" + getBaseCmndString ()+ escape(arg.cmnd), out replyStr);
            if (arg.cb != null)
            {
                arg.cb(this, jsonObject, replyStr);
            }
        }
        public void ThreadSendGetInfo(object ocb)
        {
            ProcessJSONReply cb = ocb as ProcessJSONReply;
            bGetInfoFailed = false;
            bGetInfoSuccess = false;
            string jsonText;
            this.bTasmota = true;
            // format cm?cmnd= string 
            this.status = sendGenericJSONGet("/" + getBaseCmndString() + escape("STATUS 0"), out jsonText);
            if (this.status == null)
            {
                bGetInfoFailed = true;
            }
            else
            {
                if (status.TryGetPropertyValue("StatusSTS", out var o) == false)
                {
                }
                statusSTS = (JsonObject)o;

                if(statusSTS != null)
                {
                    foreach (var property in statusSTS)
                    {
                        string key = property.Key;
                        //var value = property.Value.ToString();
                        if (key.StartsWith("POWER"))
                        {
                            powerCount++;
                        }
                    }
                }
                if (getSDK().ToLower() == "obk")
                {
                    this.bTasmota = true;
                    for (int att = 0; att < 4; att++)
                    {
                        JsonObject jsonObject = sendGenericJSONGet("/api/info", out jsonText);
                        this.info = jsonObject;
                        if (this.info == null)
                        {
                            break;
                        }
                        Thread.Sleep(50*att);
                    }
                }
                else
                {
                    this.bTasmota = true;
                }
            }
            if(this.info != null || this.status != null)
            {
                bGetInfoSuccess = true;
            }
            if (cb != null)
            {
                cb(this);
            }
        }
        public void ThreadSendGetFlashChunk(object obj)
        {
            GetFlashChunkArguments arg = obj as GetFlashChunkArguments;
            int size = arg.size;
            int adr = arg.adr;
            int end = adr + size;
            int step = 4096;
            MemoryStream res = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(res);
            int maxAttempts = 10;
            for(int ofs = adr; ofs < end; ofs += step)
            {
                int nowLen = end - ofs;
                if (nowLen > step)
                    nowLen = step;

                if (arg.cb_progress!=null)
                {
                    arg.cb_progress(ofs - adr, end - adr);
                }
                string hexString = string.Format("/api/flash/{0:X}-{1:X}", ofs, nowLen);
                for(int tr = 0; tr < maxAttempts; tr++)
                {
                    //byte [] flash = sendGetInternal("/api/flash/1e3000-2000");
                    byte[] flash = sendGetInternal(hexString);
                    if(flash == null || flash.Length != nowLen)
                    {
                        Thread.Sleep(100);
                        continue;
                    }
                    bw.Write(flash, 0, nowLen);
                    break;
                }
            }
            if (arg.cb != null)
            {
                arg.cb(res.ToArray(),(int)res.Position);
            }
        }
        internal void sendCmnd(string v, ProcessCMDReply cb)
        {
            SendCmndArguments arg = new SendCmndArguments();
            arg.cmnd = v;
            arg.cb = cb;
            startThread(ThreadSendCmnd, arg);
        }
        public void sendGetInfo(ProcessJSONReply cb)
        {
            startThread(ThreadSendGetInfo, cb);
        }
        public void sendGetFlashChunk(ProcessBytesReply cb, ProcessProgress cb_progress, int adr, int size)
        {
            GetFlashChunkArguments arg = new GetFlashChunkArguments();
            arg.cb = cb;
            arg.cb_progress = cb_progress;
            arg.adr = adr;
            arg.size = size;
            startThread(ThreadSendGetFlashChunk, arg);
        }
        private void startThread(System.Threading.ParameterizedThreadStart th, object arg)
        { 
            System.Threading.Thread thread = new System.Threading.Thread(th);
            thread.Start(arg);
        }
        private static int BinaryMatch(byte[] input, byte[] pattern)
        {
            int sLen = input.Length - pattern.Length + 1;
            for (int i = 0; i < sLen; ++i)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; ++j)
                {
                    if (input[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return i;
                }
            }
            return -1;
        }
        internal bool isTasmota()
        {
            return bTasmota;
        }
    }
}
