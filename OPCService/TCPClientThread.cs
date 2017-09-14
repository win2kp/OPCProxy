using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using log4net;
using System.Collections;

namespace OPCService
{
    /// <summary>
    /// 此事件当客户端从服务器断开或者超时是触发
    /// </summary>
    /// <param name="client">客户端对应服务线程对象</param>
    /// <param name="reason">断开的原因描述</param>
    public delegate void ClientDisconnectedEventHandler (TCPClientThread client, string reason);

    /// <summary>
    /// 客户端要求更新OPCITEM的值时触发此事件
    /// </summary>
    /// <param name="blockName">要更改的数据块</param>
    /// <param name="newValue">要更新的值</param>
    public delegate void ClientUpdatedOPCItemValueEventHandler(string blockName, string newValue, string type, TCPClientThread client);

    /// <summary>
    /// 此类用于处理客户端连接，每个客户端连接对应本类的一个对象
    /// </summary>
    public class TCPClientThread
    {
        #region 事件声明
        public event ClientDisconnectedEventHandler ClientDisconnected;
        protected virtual void OnClientDisconnected(TCPClientThread client, string reason)
        {
            if (ClientDisconnected != null) ClientDisconnected(client, reason);
        }

        public event ClientUpdatedOPCItemValueEventHandler OPCItemUpdated;
        protected virtual void OnOPCItemUpdated (string blockName, string newValue, string type, TCPClientThread client)
        {
            if (OPCItemUpdated != null) OPCItemUpdated(blockName, newValue, type, client);
        }

        #endregion  

        /// <summary>
        /// 通信套接字
        /// </summary>
        private Socket _socket = null;



        /// <summary>
        /// 服务线程
        /// </summary>
        private Thread _thread = null;

        /// <summary>
        /// 用于解析客户端请求
        /// </summary>
        private XmlDocument _doc = null;

        /// <summary>
        /// 指示客户端是否已经登陆（握手）
        /// 0 = 服务器尚未发送握手信号给客户端
        /// 1 = 服务器已发送握手信号给客户端
        /// 2 = 客户端已回复服务器握手信号
        /// 只有在_logon=2的情况下服务器才响应客户端的非握手请求
        /// </summary>
        private int _logon = 0;

        /// <summary>
        /// 日志输出
        /// </summary>
        private static log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// 客户端最后一次发送请求的时间，如果此值超过5分钟未更新
        /// 则认为客户端已断线，则对应的客户服务线程将被回收
        /// </summary>
        private DateTime _heartbeat = DateTime.Now;

        /// <summary>
        /// 针对调用方公布最后一次心跳的时间戳
        /// </summary>
        public DateTime LastHeartbeat { get { return _heartbeat; } }

        /// <summary>
        /// 存放此客户端服务线程对应的客户端IP地址
        /// </summary>
        public string IPAddress { get; set; } = "";

        /// <summary>
        /// 创建客户端服务对象
        /// </summary>
        /// <param name="socket">客户端的套接字</param>
        public TCPClientThread (Socket socket)
        {
            _socket = socket;
            _thread = new Thread(new ThreadStart(TCPClientHandler));
            _thread.IsBackground = true;

            _doc = new XmlDocument();
        }

        /// <summary>
        /// 启动服务对象
        /// </summary>
        public void Start()
        {
            _thread.Start();
            log.Info("客户端 " + this.IPAddress + " 处理线程已启动，正在处理请求");
        }

        /// <summary>
        /// 终止服务对象
        /// </summary>
        public void Terminate()
        {

            try { _socket.Close(); } catch { }
            log.Info("客户端 " + this.IPAddress + " 处理线程已关闭");
            _thread.Abort();
        }

        /// <summary>
        /// 客户端服务线程
        /// </summary>
        private void TCPClientHandler()
        {
            DateTime handshakeTime = DateTime.Now;

            while(true)
            {
                if (_socket.Connected)
                {
                    // 与客户端握手
                    if (_logon == 0)
                    {
                        log.Debug("正在与客户端 " + this.IPAddress + " 握手");
                        
                        try
                        {
                            // 向客户端发送握手信号
                            byte[] bytes = Encoding.UTF8.GetBytes("ESTSHOPCSVC.HELLO");
                            _socket.Send(bytes);
                            Thread.Sleep(100);
                        } catch (Exception ex)
                        {
                            OnClientDisconnected(this, "客户端 " + this.IPAddress + " 套接字已断开-9:" + ex.Message);
                            Thread.Sleep(Timeout.Infinite);
                        }

                        // 记录下给客户端发送握手信号的时间，此时间经过5秒仍未
                        // 收到客户端的握手响应则踢掉客户端
                        _logon = 1;
                        handshakeTime = DateTime.Now;
                    }

                    if (_socket.Available > 0)
                    {
                        // 接收客户端发送的数据
                        log.Debug("接收到客户端 " + this.IPAddress + " 发送的数据，正在解析请求信息");
                        byte[] reply = null;
                        try
                        {
                            reply = new byte[_socket.Available];
                            _socket.Receive(reply, reply.Length, SocketFlags.None);
                        }
                        catch(Exception ex)
                        {
                            OnClientDisconnected(this, "客户端 " + this.IPAddress + " 套接字已断开-0: " + ex.Message);
                            Thread.Sleep(Timeout.Infinite);
                        }

                        string replyString = Encoding.UTF8.GetString(reply);

                        // 处理客户端的握手信号
                        if (replyString.StartsWith("ESTSHOPCCLIENT.HELLO"))
                        {
                            // 客户端正确响应了握手信号
                            log.Debug("接收到客户端 " + this.IPAddress + " 发送的握手信号");
                            _logon = 2;
                        }
                        else
                        {
                            // 处理其他非握手请求
                            if (_logon != 2)
                            {
                                // 检查客户端是否已经握手
                                // 如果客户端还未发送握手信号就给出其他内容
                                // 则判定客户端非法并断开连接
                                OnClientDisconnected(this, "客户端 " + this.IPAddress + " 在握手前发送指令");
                                Thread.Sleep(Timeout.Infinite);
                            }

                            // 客户端已经成功握手
                            // 处理客户端的读取OPCITEM请求
                            if (replyString.StartsWith("ESTSHOPCSVC.READ:"))
                            {
                                // 解析要读取的OPCITEM名称
                                string blockName = replyString.Split(':')[1];
                                string value = "NaN";

                                log.Debug("客户端 " + this.IPAddress + " 请求读取 " + blockName);
                                // 检查请求的数据库是否已经存在于全局结果集中
                                // （如果不存在代表配置文件中未配置此数据块）
                                if (ValuesContainer.Values.ContainsKey(blockName))
                                    value = ValuesContainer.Values[blockName].ToString();
                                else
                                {
                                    log.Debug("服务器中未配置此地址块 " + blockName);
                                    OnClientDisconnected(this, "客户端 " + this.IPAddress + " 试图读取未配置的地址块:" + blockName);
                                    Thread.Sleep(Timeout.Infinite);
                                }

                                try
                                {
                                    // 把获取到的值和连接品质发送给客户端
                                    _socket.Send(Encoding.UTF8.GetBytes("ESTSHOPCSVC.RESULT:" + value + ":" + ValuesContainer.Qualities[blockName].ToString()));
                                    log.Debug("向客户端 " + this.IPAddress + " 发送了读取响应 " + blockName + "=" + value);
                                    ValuesContainer.ReadCounts[blockName] = (int)ValuesContainer.ReadCounts[blockName] + 1;
                                    // 更新心跳时间戳
                                    this._heartbeat = DateTime.Now;
                                }
                                catch (Exception ex)
                                {
                                    OnClientDisconnected(this, "客户端 " + this.IPAddress + " 套接字已断开-1:" + ex.StackTrace + "\n" + ex.Message + (_socket == null).ToString() + "/" + (ValuesContainer.Qualities[blockName] == null).ToString());
                                    Thread.Sleep(Timeout.Infinite);
                                }

                            }
                            // 处理客户端的写入OPCITEM请求
                            else if (replyString.StartsWith("ESTSHOPCSVC.WRITE:"))
                            {
                                // 解析要更新的OPCITEM名称和要写入的新值
                                string blockName = replyString.Split(':')[1];
                                string value = replyString.Split(':')[2];
                                string type = replyString.Split(':')[3];

                                log.Debug("接收到客户端 " + this.IPAddress + " 的OPCITEM更新请求 " + blockName + "=" + value + ", 正在转发给上层处理");

                                // 发出请求更改OPC值的事件给分派线程
                                OnOPCItemUpdated(blockName, value, type, this);

                                try
                                {
                                    // 把更改的结果和连接品质发送给客户端

                                    _socket.Send(Encoding.UTF8.GetBytes("ESTSHOPCSVC.RESULT:" + value + ":" + ValuesContainer.Qualities[blockName].ToString()));
                                    log.Debug("向客户端 " + this.IPAddress + " 发送了写入响应 " + blockName + "=" + value);

                                    // 更新心跳时间戳
                                    this._heartbeat = DateTime.Now;
                                }
                                catch (Exception ex)
                                {
                                    OnClientDisconnected(this, "客户端 " + this.IPAddress + " 套接字已断开-2:" + ex.Message);
                                    Thread.Sleep(Timeout.Infinite);
                                }

                            } else if (replyString.StartsWith("ESTSHOPCSVC.BYE"))
                            {
                                log.Info("客户端 " + this.IPAddress + " 已注销");
                                OnClientDisconnected(this, "客户端正常注销");
                                Thread.Sleep(Timeout.Infinite);

                            } else if (replyString.StartsWith("ESTSHOPCSVC.READS:"))
                            {
                                // 解析要读取的OPCITEM名称
                                string xml = replyString.Split(':')[1];
                                try
                                {
                                    _doc.LoadXml(xml);
                                }
                                catch
                                {
                                    log.Info("客户端 " + this.IPAddress + " 发送了非法请求");
                                    OnClientDisconnected(this, "发送了非法请求");
                                    Thread.Sleep(Timeout.Infinite);
                                }

                                // 检查请求的数据库是否已经存在于全局结果集中
                                // （如果不存在代表配置文件中未配置此数据块）
                                foreach (XmlNode node in _doc.DocumentElement.SelectNodes("item"))
                                {
                                    string blockName = node.Attributes.GetNamedItem("name").InnerText;
                                    if (ValuesContainer.Values.ContainsKey(blockName))
                                    {
                                        node.InnerText = ValuesContainer.Values[blockName].ToString();
                                        ValuesContainer.ReadCounts[blockName] = (int)ValuesContainer.ReadCounts[blockName] + 1;
                                        try
                                        {
                                            node.Attributes.GetNamedItem("quality").InnerText =  ValuesContainer.Qualities[blockName].ToString();
                                        } catch { }
                                    }
                                    else
                                    {
                                        log.Debug("服务器中未配置此地址块 " + blockName);
                                        OnClientDisconnected(this, "客户端 " + this.IPAddress + " 试图读取未配置的地址块:" + blockName);
                                        Thread.Sleep(Timeout.Infinite);
                                    }
                                }

                                try
                                {
                                    // 把获取到的值发送给客户端
                                    _socket.Send(Encoding.UTF8.GetBytes("ESTSHOPCSVC.RESULT:" + _doc.InnerXml));
                                    log.Debug("向客户端 " + this.IPAddress + " 发送了读取响应 " + _doc.InnerXml);

                                    // 更新心跳时间戳
                                    this._heartbeat = DateTime.Now;
                                }
                                catch (Exception ex)
                                {
                                    OnClientDisconnected(this, "客户端 " + this.IPAddress + " 套接字已断开-3: " + ex.Message);
                                    Thread.Sleep(Timeout.Infinite);
                                }

                            }
                            else if (replyString.StartsWith("ESTSHOPCSVC.WRITES:"))
                            {
                                // 解析要读取的OPCITEM名称
                                string xml = replyString.Split(':')[1];
                                try
                                {
                                    _doc.LoadXml(xml);
                                }
                                catch
                                {
                                    log.Info("客户端 " + this.IPAddress + " 发送了非法请求");
                                    OnClientDisconnected(this, "发送了非法请求");
                                    Thread.Sleep(Timeout.Infinite);
                                }

                                // 检查请求的数据库是否已经存在于全局结果集中
                                // （如果不存在代表配置文件中未配置此数据块）
                                foreach (XmlNode node in _doc.DocumentElement.SelectNodes("item"))
                                {
                                    string blockName = node.Attributes.GetNamedItem("name").InnerText;
                                    string value = node.InnerText;
                                    try
                                    {
                                        node.Attributes.GetNamedItem("quality").InnerText = ValuesContainer.Qualities[blockName].ToString();
                                    }
                                    catch { }

                                    log.Debug("接收到客户端 " + this.IPAddress + " 的OPCITEM更新请求 " + blockName + "=" + value + ", 正在转发给上层处理");

                                    // 发出请求更改OPC值的事件给分派线程
                                    OnOPCItemUpdated(blockName, value, "STRING", this);
                                }

                                try
                                {
                                    // 把获取到的值发送给客户端
                                    _socket.Send(Encoding.UTF8.GetBytes("ESTSHOPCSVC.RESULT:" + _doc.InnerXml));
                                    log.Debug("向客户端 " + this.IPAddress + " 发送了读取响应 " + _doc.InnerXml);

                                    // 更新心跳时间戳
                                    this._heartbeat = DateTime.Now;
                                }
                                catch (Exception ex)
                                {
                                    OnClientDisconnected(this, "客户端 " + this.IPAddress + " 套接字已断开-4: " + ex.Message);
                                    Thread.Sleep(Timeout.Infinite);
                                }

                            }
                        }
                    }
                    else
                        Thread.Sleep(20);

                    // 踢掉连接后超过5秒仍未响应握手信号的客户端
                    if (_logon != 2 && DateTime.Now.Subtract(handshakeTime).TotalSeconds > 60)
                    {
                        OnClientDisconnected(this, "客户端 " + this.IPAddress + " 超时未发送握手信号");
                        Thread.Sleep(Timeout.Infinite);
                    }
                }
                else
                {
                    // 客户端套接字断开
                    OnClientDisconnected(this, "客户端 " + this.IPAddress + " 套接字已断开-5");
                    Thread.Sleep(Timeout.Infinite);
                }
            }
        }
    }
}
