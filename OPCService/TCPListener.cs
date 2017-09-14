using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using log4net;

namespace OPCService
{ 
    /// <summary>
    /// 用此事件通知主程序进行OPCITEM数据的更新
    /// </summary>
    /// <param name="blockName">更新的数据块</param>
    /// <param name="newValue">更新的值</param>
    public delegate void UpdatedOPCItemValueEventHandler (string blockName, string newValue, string type);

    /// <summary>
    /// 此类用于监听TCP网络请求并分派客户端连接处理线程
    /// </summary>
    public class TCPListener
    {
        #region 事件定义
        /// <summary>
        /// 用此事件通知主程序进行OPCITEM数据的更新
        /// </summary>
        public event UpdatedOPCItemValueEventHandler OPCItemUpdated;

        protected virtual void OnOPCItemUpdated (string blockName, string newValue, string type)
        {
            if (OPCItemUpdated != null) OPCItemUpdated(blockName, newValue, type);
        }
        #endregion

        /// <summary>
        /// 客户服务线程分派网络监听线程
        /// </summary>
        private Thread _thread = null;

        /// <summary>
        /// 客户服务线程回收线程
        /// </summary>
        private Thread _recycle = null;

        /// <summary>
        /// 监听的端口号
        /// </summary>
        private int _port = 9100;

        /// <summary>
        /// 监听套接字
        /// </summary>
        private Socket _socket = null;

        /// <summary>
        /// 存放所有已连接的客户端线程
        /// </summary>
        private List<TCPClientThread> _clients = new List<TCPClientThread>();

        /// <summary>
        /// 客户端超过以下秒数没有传输后断开服务线程
        /// </summary>
        private int _clientTimeout = 300;

        /// <summary>
        /// 日志输出
        /// </summary>
        private static log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        /// <summary>
        /// 客户端无传输超时
        /// </summary>
        public int ClientTimeout
        {
            get { return _clientTimeout;}
            set { _clientTimeout = value; }
        }
        
        public List<TCPClientThread> Clients { get { return _clients; } }

        /// <summary>
        /// 初始化监听器
        /// </summary>
        /// <param name="port">监听的端口</param>
        public TCPListener(int port = 9100, int clientTimeout = 300)
        {
            _clientTimeout = clientTimeout;
            _port = port;
            _thread = new Thread(new ThreadStart(TCPListenerHandler));
            _thread.IsBackground = true;

            _recycle = new Thread(new ThreadStart(TCPClientRecycleHandler));
            _recycle.IsBackground = true;
        }

        /// <summary>
        /// 启动监听器
        /// </summary>
        public void Start() {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint localEp = new IPEndPoint(IPAddress.Any, _port);

            _socket.Bind(localEp);
            _socket.Listen(0);
            log.Info("正在端口 " + _port + " 监听网络请求");
            _thread.Start();
            log.Info("正在启动链接回收线程");
            log.Info("客户端无传输超时已被设置为" + _clientTimeout + "秒");
            _recycle.Start();
        }

        /// <summary>
        /// 中止监听器
        /// </summary>
        public void Terminate() {
            log.Info("正在关闭所有客户端处理线程");
            foreach (TCPClientThread client in _clients)
            {
                client.Terminate();
            }

            log.Info("正在关闭网络服务组件");
            _socket.Close();
            _clients.Clear();
            _thread.Abort();
            log.Info("正在关闭连接回收线程");
            _recycle.Abort();
            log.Info("网络服务线程已中止");
        }

        /// <summary>
        /// 监听线程
        /// </summary>
        private void TCPListenerHandler()
        {
            while(true)
            {
                try
                {
                    Socket clientSocket = _socket.Accept();
                    IPEndPoint ep = (IPEndPoint) clientSocket.RemoteEndPoint;
                    log.Info("已接收到客户端 " + ep.Address.ToString() + " 连接请求，正在分派客户端处理线程");
                    TCPClientThread client = new TCPClientThread(clientSocket);
                    client.IPAddress = ep.Address.ToString();
                    _clients.Add(client);
                    client.ClientDisconnected+=Client_ClientDisconnected;
                    client.OPCItemUpdated +=Client_OPCItemUpdated;
                    client.Start();
                }
                catch(Exception ex) { log.Error(ex.Message); }
            }
        }

        /// <summary>
        /// 客户线程回收线程
        /// </summary>
        private void TCPClientRecycleHandler()
        {
            while (true)
            {
                List<TCPClientThread> remove = new List<TCPClientThread>();
                foreach (TCPClientThread client in _clients)
                {
                    // 如果心跳时间戳大于5分钟未更新则清理该客户端
                    if (DateTime.Now.Subtract(client.LastHeartbeat).TotalSeconds > _clientTimeout)
                    {
                        remove.Add(client);
                    }
                }

                // 清除所有需要回收的客户端服务线程
                foreach (TCPClientThread client in remove)
                {
                    client.Terminate();
                    _clients.Remove(client);
                    log.Info("客户端 " + client.IPAddress + " 超时未通信连接已经被回收");
                }

                Thread.Sleep(5000);
            }
        }

        /// <summary>
        /// 接收客户端处理线程返回的OPCITEM更新请求并转发给主程序
        /// </summary>
        /// <param name="blockName">要更新的地址块</param>
        /// <param name="newValue">更新的值</param>
        private void Client_OPCItemUpdated (string blockName, string newValue, string type, TCPClientThread client)
        {
            log.Debug("接收到客户端 " + client.IPAddress + " 处理线程返回的OPCITEM更新事件，正在向主程序转发");
            OnOPCItemUpdated(blockName, newValue, type);
        }

        /// <summary>
        /// 接收客户端处理线程返回的断开事件并清理客户端连接列表
        /// </summary>
        /// <param name="client">断开的客户端</param>
        /// <param name="reason">断开的原因</param>
        private void Client_ClientDisconnected (TCPClientThread client, string reason)
        {
            log.Info("检测到客户端 " + client.IPAddress + " 已断开连接，正在清理处理线程 断线原因：" + reason);
            client.Terminate();
            _clients.Remove(client);
        }
    }
}
