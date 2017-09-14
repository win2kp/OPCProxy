using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace OPCServiceClientAsync
{
    /// <summary>
    /// 当客户端从服务器断开时触发
    /// </summary>
    public delegate void DisconnectHandler(OPCServiceClientAsync sender);

    /// <summary>
    /// 当客户端成功连接到服务器时触发
    /// </summary>
    public delegate void ConnectHandler (OPCServiceClientAsync sender);

    /// <summary>
    /// 东尚OPC代理服务器客户端
    /// </summary>
    public class OPCServiceClientAsync
    {
        #region 事件定义
        /// <summary>
        /// 当客户端从服务器断开时触发
        /// </summary>
        public event DisconnectHandler ServerDisconnected;

        protected virtual void OnServerDisconnected(OPCServiceClientAsync sender)
        {
            if (ServerDisconnected != null) ServerDisconnected(sender);
        }

        /// <summary>
        /// 当客户端成功连接到服务器时触发
        /// </summary>
        public event ConnectHandler ServerConnected;
        protected virtual void OnServerConnected(OPCServiceClientAsync sender)
        {
            if (ServerConnected != null) ServerConnected(sender);
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
        /// 连接服务器线程
        /// </summary>
        private Thread _threadConnect = null;

        /// <summary>
        /// 获取OPCitem值线程
        /// </summary>
        private Thread _threadGetValue = null;

        /// <summary>
        /// 更改OPCItem值线程
        /// </summary>
        private Thread _threadSetValue = null;

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
        /// 最后一次获取到的或者设置的OPCItem值
        /// </summary>
        public string LastValue { get; set; } = "";

        /// <summary>
        /// 创建客户端对象
        /// </summary>
        /// <param name="server">要连接的服务器地址</param>
        /// <param name="port">要连接的服务器端口</param>
        /// <param name="timeout">超时毫秒数</param>
        public OPCServiceClientAsync(string server, int port, int timeout = 5000)
        {
            this.Server = server;
            this.Port = port;
            this.Timeout = timeout;
        }

        /// <summary>
        /// 将指定的主机名解析为IPV4地址
        /// </summary>
        /// <param name="host">主机名</param>
        /// <returns>IP地址</returns>
        private IPAddress GetAddress(string host)
        {
            IPAddress[] addresses = Dns.GetHostEntry(host).AddressList;
            foreach (IPAddress addr in addresses)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork) return addr;
            }
            throw new Exception("无法为主机 " + host + "解析到合法的IPV4地址，请检查网络或者DNS设置");
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        public void Connect()
        {
            _threadConnect = new Thread(new ThreadStart(ConnectHandler));
            _threadConnect.IsBackground = true;
            _threadConnect.Start();
        }
        
        /// <summary>
        /// 连接服务器线程
        /// </summary>
        public void ConnectHandler()
        {
            while (!this.Connected)
            {
                Thread.Sleep(50);
                try
                {
                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    _socket.SendTimeout = this.Timeout;
                    _socket.ReceiveTimeout = this.Timeout;

                    IPAddress ip;
                    if (IPAddress.TryParse(this.Server, out ip))
                        _remoteEp = new IPEndPoint(ip, this.Port);
                    else
                        _remoteEp = new IPEndPoint(GetAddress(this.Server), this.Port);

                    _socket.Connect(_remoteEp);

                }
                catch { continue; }

                this.WaitStatusTimeout(_socket.Available > 0, 500);

                if (_socket.Available == 0)
                {
                    _socket.Close();
                    continue;
                }

                byte[] bytes = null;
                try
                {
                    bytes = new byte[_socket.Available];
                    _socket.Receive(bytes, bytes.Length, SocketFlags.None);
                }
                catch
                {
                    _socket.Close();
                    continue;
                }



                if (Encoding.UTF8.GetString(bytes).StartsWith("ESTSHOPCSVC.HELLO"))
                {
                    byte[] send = Encoding.UTF8.GetBytes("ESTSHOPCCLIENT.HELLO");
                    try
                    {
                        _socket.Send(send);
                        this.Connected = true;
                        OnServerConnected(this);
                        return;
                    }
                    catch
                    {
                        _socket.Close();
                        continue;
                    }
                }
                else
                {
                    _socket.Close();
                    continue;
                }
            }
        }

        /// <summary>
        /// 客户端调用此方法可用于等待连接，读取，写入数据完成并返回数据
        /// </summary>
        /// <param name="condition">要等待为真的条件</param>
        /// <param name="timeout">最长等待的时间（毫秒）</param>
        /// <param name="doEvents">是否在等待时处理窗口消息</param>
        public void WaitStatusTimeout(bool condition, int timeout = 2000, bool doEvents = true)
        {
            DateTime ts = DateTime.Now;
            while (!condition && DateTime.Now.Subtract(ts).TotalMilliseconds <= timeout)
            {
                if (doEvents) Application.DoEvents();
                System.Threading.Thread.Sleep(20);
            }
        }


        /// <summary>
        /// 更新OPCItem值线程
        /// </summary>
        private void SetValueHandler(object param)
        {
            string[] args = (string[])param;
            string block = args[0];
            string value = args[1];
            string type = args[2];

            this.WaitStatusTimeout(this.Connected, 5000);

            try
            {
                string cmd = "ESTSHOPCSVC.WRITE:" + block + ":" + value + ":" + type;
                _socket.Send(Encoding.UTF8.GetBytes(cmd));
            }
            catch
            {
                OnServerDisconnected(this);
                return;
            }

            this.WaitStatusTimeout(_socket.Available > 0, 500);

            if (_socket.Available == 0)
            {
                OnServerDisconnected(this);
                return;
            };

            byte[] bytes = new byte[_socket.Available];
            try
            {
                _socket.Receive(bytes, bytes.Length, SocketFlags.None);
            } catch
            {
                OnServerDisconnected(this);
                return;
            }

            string reply = Encoding.UTF8.GetString(bytes);
            if (reply.StartsWith("ESTSHOPCSVC.RESULT:"))
            {
                this.LastValue = reply.Split(':')[1];
                return;
            }
            
            this.LastValue = "";
        }

        /// <summary>
        /// 获取OPCItem值线程
        /// </summary>
        private void GetValueHandler(object param)
        {
            string block = param.ToString();

            this.WaitStatusTimeout(this.Connected, 5000);

            try
            {
                string cmd = "ESTSHOPCSVC.READ:" + block;
                _socket.Send(Encoding.UTF8.GetBytes(cmd));
            }
            catch
            {
                OnServerDisconnected(this);
                return;
            }

            this.WaitStatusTimeout(_socket.Available > 0, 500);

            if (_socket.Available == 0)
            {
                OnServerDisconnected(this);
                return;
            };

            byte[] bytes = new byte[_socket.Available];
            try
            {
                _socket.Receive(bytes, bytes.Length, SocketFlags.None);
            }
            catch
            {
                OnServerDisconnected(this);
                return;
            }

            string reply = Encoding.UTF8.GetString(bytes);
            if (reply.StartsWith("ESTSHOPCSVC.RESULT:"))
            {
                this.LastValue = reply.Split(':')[1];
                return;
            }

            this.LastValue = "";
        }


        /// <summary>
        /// 从服务器断开
        /// </summary>
        public void Disconnect()
        {
            try { _threadGetValue.Abort(); } catch { }
            try { _threadSetValue.Abort(); } catch { }
            try { _threadConnect.Abort(); } catch { }


            try
            {
                _socket.Close();
            }
            catch { }

            this.Connected = false;
        }

        /// <summary>
        /// 获取OPCItem的值
        /// </summary>
        /// <param name="blockName">数据块名称</param>
        public void GetValue(string blockName)
        {
            this.LastValue = "";
            _threadGetValue = new Thread(new ParameterizedThreadStart(GetValueHandler));
            _threadGetValue.IsBackground = true;
            _threadGetValue.Start(blockName);
        }

        /// <summary>
        /// 设置OPCItem的值
        /// </summary>
        /// <param name="blockName">数据块名称</param>
        /// <param name="value">要设置的值</param>
        public void SetValue(string blockName, string value)
        {
            this.LastValue = "";
            _threadSetValue = new Thread(new ParameterizedThreadStart(SetValueHandler));
            _threadSetValue.IsBackground = true;
            _threadSetValue.Start(new string[] { blockName, value, "STRING" });
        }

    }
}
