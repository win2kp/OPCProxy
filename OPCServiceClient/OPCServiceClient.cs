using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using System.Xml;


namespace OPCServiceClient
{
    /// <summary>
    /// 当读取或写入时发现地址块状态不正常时触发此事件
    /// </summary>
    /// <param name="blockName">地址块名称</param>
    /// <param name="status">状态编码</param>
    public delegate void BadBlockDetectedHandler (string blockName, OPCQualities status);


    public class OPCServiceClient
    {

        #region 事件定义
        /// <summary>
        /// 当读取或写入时发现地址块状态不正常时触发此事件
        /// </summary>
        public event BadBlockDetectedHandler BadBlockDetected;


        /// <summary>
        /// 当KEPWARE端OPCITEM的值发生变化时触发此事件
        /// </summary>
        /// <param name="blockName">数据块名称</param>
        /// <param name="status">数据块的Quality编码</param>
        protected virtual void OnBadBlockDetected (string blockName, OPCQualities status)
        {
            if (BadBlockDetected != null) BadBlockDetected(blockName, status);
        }

        #endregion


        /// <summary>
        /// 通信套接字
        /// </summary>
        private Socket _socket = null;

        /// <summary>
        /// 服务器远程端点
        /// </summary>
        private IPEndPoint _remoteEp = null;

        /// <summary>
        /// 用于XML解析
        /// </summary>
        private XmlDocument _doc = null;


        /// <summary>
        /// 连接的服务器
        /// </summary>
        public string Server { get; set; }

        /// <summary>
        /// 服务器端口
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 指示是否已连接到服务器
        /// </summary>
        public bool Connected { get; set; } = false;

        /// <summary>
        /// 设置连接超时
        /// </summary>
        public int Timeout { get; set; } = 5000;

        /// <summary>
        /// 创建客户端对象
        /// </summary>
        /// <param name="server">要连接的服务器地址</param>
        /// <param name="port">要连接的服务器端口</param>
        /// <param name="timeout">超时毫秒数</param>
        public OPCServiceClient (string server, int port, int timeout = 5000)
        {
            this.Server = server;
            this.Port = port;
            this.Timeout = timeout;

            _doc = new XmlDocument();
        }


        /// <summary>
        /// 将指定的主机名解析为IPV4地址
        /// </summary>
        /// <param name="host">主机名</param>
        /// <returns>IP地址</returns>
        private IPAddress GetAddress (string host)
        {
            IPAddress[] addresses = Dns.GetHostEntry(host).AddressList;
            foreach (IPAddress addr in addresses)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork) return addr;
            }
            throw new Exception("无法为主机 " + host + "解析到合法的IPV4地址，请检查网络或者DNS设置");
        }

        /// <summary>
        /// 客户端调用此方法可用于等待socket连接，读取，写入数据完成并返回数据
        /// </summary>
        /// <param name="condition">要等待为真的条件</param>
        /// <param name="timeout">最长等待的时间（毫秒）</param>
        /// <param name="doEvents">是否在等待时处理窗口消息</param>
        public void WaitDataAvailableTimeout (Socket socket, int timeout = 2000, bool doEvents = true)
        {
            try
            {
                DateTime ts = DateTime.Now;
                Thread.Sleep(50);
                while (socket.Available == 0 && DateTime.Now.Subtract(ts).TotalMilliseconds <= timeout)
                {
                    if (doEvents) Application.DoEvents();
                    Thread.Sleep(10);
                }
            } catch { Thread.Sleep(200); }
        }

        /// <summary>
        /// 连接服务器
        /// </summary>
        public bool Connect()
        {
            try
            {
                try
                {
                    // 尝试关闭上一次断开的连接
                    _socket.Close();
                    _socket = null;
                    this.Connected = false;
                }
                catch { }

                // 连接到服务器
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _socket.SendTimeout = this.Timeout;
                _socket.ReceiveTimeout = this.Timeout;

                // 地址解析
                IPAddress ip;
                if (IPAddress.TryParse(this.Server, out ip))
                    _remoteEp = new IPEndPoint(ip, this.Port);
                else
                    _remoteEp = new IPEndPoint(GetAddress(this.Server), this.Port);

                _socket.Connect(_remoteEp);

            }
            catch { return false; }

            // 等待服务器的握手信号
            this.WaitDataAvailableTimeout(_socket, this.Timeout);

            if (_socket.Available == 0)
            {
                // 没有收到服务器握手信号
                _socket.Close();
                return false;
            }

            // 处理服务器握手信号
            byte[] bytes = null;
            try
            {
                bytes = new byte[_socket.Available];
                _socket.Receive(bytes, bytes.Length, SocketFlags.None);
            }
            catch
            {
                _socket.Close();
                return false;
            }

            // 检查握手信号是否有效
            if (Encoding.UTF8.GetString(bytes).StartsWith("ESTSHOPCSVC.HELLO"))
            {
                try
                {
                    // 发送握手响应
                    byte[] send = Encoding.UTF8.GetBytes("ESTSHOPCCLIENT.HELLO");
                    _socket.Send(send);
                    this.Connected = true;
                    return true;
                }
                catch
                {
                    _socket.Close();
                    return false;
                }
            }
            else
            {
                _socket.Close();
                return false;
            }
        }

        /// <summary>
        /// 更新OPCItem值
        /// </summary>
        public bool SetValue (string block, string value, string type = "STRING")
        {

            while (!this.Connected && !_socket.Connected)
            {
                this.Connect();
                Application.DoEvents();
                Thread.Sleep(20);
            }

            bool done = false;

            while (!done)
            {
                try
                {
                    string cmd = "ESTSHOPCSVC.WRITE:" + block + ":" + value + ":" + type;
                    _socket.Send(Encoding.UTF8.GetBytes(cmd));
                    done = true;
                }
                catch { this.Connect(); Application.DoEvents(); Thread.Sleep(20); }
            }

            this.WaitDataAvailableTimeout(_socket, this.Timeout);

            try
            {
                if (_socket.Available == 0) return false;
            } catch { return false; }

            byte[] bytes = new byte[_socket.Available];

            done = false;
            while (!done)
            {
                try
                {
                    _socket.Receive(bytes, bytes.Length, SocketFlags.None);
                    done = true;
                }
                catch { this.Connect(); Application.DoEvents(); Thread.Sleep(20); }
            }

            string reply = Encoding.UTF8.GetString(bytes);
            if (reply.StartsWith("ESTSHOPCSVC.RESULT:"))
            {
                try
                {
                    OPCQualities quality = (OPCQualities)Enum.Parse(typeof(OPCQualities), reply.Split(':')[2]);
                    if (quality != OPCQualities.Good)
                    {
                        OnBadBlockDetected(block, quality);
                    }
                } catch { }

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 获取OPCItem值
        /// </summary>
        /// <param name="block">数据块名称</param>
        /// <returns>获得的值</returns>
        public string GetValue (string block)
        {

            while (!this.Connected && !_socket.Connected)
            {
                this.Connect();
                Application.DoEvents();
                Thread.Sleep(20);
            }

            bool done = false;
            while (!done)
            {
                try
                {
                    string cmd = "ESTSHOPCSVC.READ:" + block;
                    _socket.Send(Encoding.UTF8.GetBytes(cmd));
                    done = true;
                }
                catch { this.Connect(); Application.DoEvents(); Thread.Sleep(20); }
            }

            this.WaitDataAvailableTimeout(_socket, this.Timeout);

            try
            {
                if (_socket.Available == 0) return "ERROR";
            } catch { return "ERROR"; }

            byte[] bytes = new byte[_socket.Available];

            done = false;
            while (!done)
            {
                try
                {
                    _socket.Receive(bytes, bytes.Length, SocketFlags.None);
                    done = true;
                }
                catch { this.Connect(); Application.DoEvents(); Thread.Sleep(20); }
            }


            string reply = Encoding.UTF8.GetString(bytes);
            if (reply.StartsWith("ESTSHOPCSVC.RESULT:"))
            {
                try
                {
                    OPCQualities quality = (OPCQualities)Enum.Parse(typeof(OPCQualities), reply.Split(':')[2]);
                    if (quality != OPCQualities.Good)
                    {
                        OnBadBlockDetected(block, quality);
                    }
                }
                catch { }
                return reply.Split(':')[1];
            }
            else
                return "ERROR";
        }

        /// <summary>
        /// 获取一组OPCItem的值
        /// </summary>
        /// <param name="blocks">数据块名称列表</param>
        /// <returns>获得的值列表</returns>
        public Dictionary<string, string> GetValues (List<string> blocks)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();
            if (blocks.Count == 0) return ret;
            if (!this.Connected && !_socket.Connected) return ret;

            // 组合xml请求
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><read>";
            foreach (string key in blocks)
                xml += "<item name=\"" + key + "\" quality=\"\" />";

            xml+= "</read>";

            while (!this.Connected && !_socket.Connected)
            {
                this.Connect();
                Application.DoEvents();
                Thread.Sleep(20);
            }

            bool done = false;
            while (!done)
            {
                try
                {
                    string cmd = "ESTSHOPCSVC.READS:" + xml;
                    _socket.Send(Encoding.UTF8.GetBytes(cmd));
                    done = true;
                }
                catch { this.Connect(); Application.DoEvents(); Thread.Sleep(20); }
            }

            this.WaitDataAvailableTimeout(_socket, this.Timeout);
            try
            {
                if (_socket.Available == 0) return ret;
            } catch { return ret; }

            byte[] bytes = new byte[_socket.Available];

            done = false;
            while (!done)
            {
                try
                {
                    _socket.Receive(bytes, bytes.Length, SocketFlags.None);
                    done = true;
                }
                catch { this.Connect(); Application.DoEvents(); Thread.Sleep(20); }
            }

            string reply = Encoding.UTF8.GetString(bytes);
            if (reply.StartsWith("ESTSHOPCSVC.RESULT:"))
            {
                // 解析收到的回复
                xml = reply.Split(':')[1];
                try
                {
                    _doc.LoadXml(xml);
                }
                catch { return ret; }  
                foreach (XmlNode node in _doc.DocumentElement.SelectNodes("item"))
                {
                    ret.Add(node.Attributes.GetNamedItem("name").InnerText, node.InnerText);
                    try
                    {
                        OPCQualities quality = (OPCQualities)Enum.Parse(typeof(OPCQualities), node.Attributes.GetNamedItem("quality").InnerText);
                        if (quality != OPCQualities.Good)
                        {
                            OnBadBlockDetected(node.Attributes.GetNamedItem("name").InnerText, quality);
                        }
                    }
                    catch { }
                }
            }

            return ret;
        }

        /// <summary>
        /// 更新一组OPCItem值
        /// </summary>
        /// <param name="blocks">要更新的item列表</param>
        /// <returns>是否更新成功</returns>
        public bool SetValues (Dictionary<string, string> blocks)
        {
            if (blocks.Count == 0) return true;
            // 组合xml请求
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><write>";
            foreach (string key in blocks.Keys)
                xml += "<item name=\"" + key + "\" quality=\"\">" + blocks[key] + "</item>";

            xml+= "</write>";

            string cmd = "ESTSHOPCSVC.WRITES:" + xml;

            while (!this.Connected && !_socket.Connected)
            {
                this.Connect();
                Application.DoEvents();
                Thread.Sleep(20);
            }

            bool done = false;
            while (!done)
            {
                try
                {
                    _socket.Send(Encoding.UTF8.GetBytes(cmd));
                    done = true;
                }
                catch { this.Connect(); Application.DoEvents(); Thread.Sleep(20); }
            }

            this.WaitDataAvailableTimeout(_socket, this.Timeout);

            try { 
                if (_socket.Available == 0) return false;
            }
            catch { return false; }

            byte[] bytes = new byte[_socket.Available];

            done = false;
            while (!done)
            {
                try
                {
                    _socket.Receive(bytes, bytes.Length, SocketFlags.None);
                    done = true;
                }
                catch { this.Connect(); Application.DoEvents(); Thread.Sleep(20); }
            }


            string reply = Encoding.UTF8.GetString(bytes);

            // 解析服务器返回的信息
            bool ret = true;
            if (reply.StartsWith("ESTSHOPCSVC.RESULT:"))
            {
                xml = reply.Split(':')[1];
                try
                {
                    _doc.LoadXml(xml);
                }
                catch { return false; }
                foreach (XmlNode node in _doc.DocumentElement.SelectNodes("item"))
                {
                    if (node.InnerText != blocks[node.Attributes.GetNamedItem("name").InnerText]) ret = false;
                    try { 
                        OPCQualities quality = (OPCQualities)Enum.Parse(typeof(OPCQualities), node.Attributes.GetNamedItem("quality").InnerText);
                        if (quality != OPCQualities.Good)
                        {
                            OnBadBlockDetected(node.Attributes.GetNamedItem("name").InnerText, quality);
                        }
                    }
                    catch { }
                }
            }
            else
                ret = false;

            return ret;
        }

        /// <summary>
        /// 从服务器断开
        /// </summary>
        public void Disconnect ()
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes("ESTSHOPCSVC.BYE");
                _socket.Send(bytes, bytes.Length, SocketFlags.None);
            }
            catch { }
            finally
            {
                _socket.Close();
                _socket = null;
                this.Connected = false;
            }

        }

    }
}
