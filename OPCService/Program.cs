using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OPCAutomation;
using System.Collections;
using log4net;
using System.IO;
using System.Xml;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using System.Runtime.InteropServices;


namespace OPCService
{
    /// <summary>
    /// 服务器主程序
    /// </summary>
    class Program
    {
        [DllImport("kernel32.dll",CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Auto)]
        private extern static void FreeConsole ();

        /// <summary>
        /// KEPWare控制器
        /// </summary>
        public static KEPWareController controller = new KEPWareController();

        /// <summary>
        /// 日志
        /// </summary>
        private static log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// 服务器加载的配置文件路径
        /// </summary>
        private static string configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "opcservice.xml");

        /// <summary>
        /// 指示服务器是否处于DEBUG模式（会输出OPC更新的信息到控制台）
        /// </summary>
        private static bool _debug = false;

        /// <summary>
        /// 指示服务器是否运行于服务模式（没有控制台）
        /// </summary>
        private static bool _daemon = false;

        /// <summary>
        /// 服务器监听的端口
        /// </summary>
        private static int _port = 9100;

        /// <summary>
        /// 指示服务器是否正在重新加载配置文件
        /// </summary>
        private static bool _reloading = false;

        /// <summary>
        /// 是否将OPC的值持久化保存到文件以便下次启动时加载
        /// </summary>
        private static bool _persistence = false;

        /// <summary>
        /// 服务器监听的通道名称
        /// </summary>
        private static string _channelName = "";

        /// <summary>
        /// 服务器监听的设备名称
        /// </summary>
        private static string _deviceName = "";

        /// <summary>
        /// 服务器连接的主机名
        /// </summary>
        private static string _host = "";

        /// <summary>
        /// 服务器连接的KEPWARE服务实例名称
        /// </summary>
        private static string _server = "";

        /// <summary>
        /// 客户端经过xx秒后没有任何传输将被断开
        /// </summary>
        private static int _clientTimeout = 300;

        /// <summary>
        /// 监视器线程
        /// </summary>
        private static Thread _monitor = null;

        /// <summary>
        /// 监视器刷新延迟
        /// </summary>
        private static int _refreshRate = 1000;

        /// <summary>
        /// 获取AppConfig文件的路径
        /// </summary>
        private static string _appConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Process.GetCurrentProcess().ProcessName + ".exe.config");

        /// <summary>
        /// 服务器启动时间戳
        /// </summary>
        private static DateTime _start = DateTime.Now;

        /// <summary>
        /// 保存各个OPCITEM的连接品质
        /// </summary>
        private static Hashtable _qualities = Hashtable.Synchronized(new Hashtable());

        /// <summary>
        /// 存储所有item的最近更新时间戳
        /// </summary>
        private static Hashtable _itemLastUpdate = Hashtable.Synchronized(new Hashtable());

        /// <summary>
        /// 存储所有item的最近同步时间戳
        /// </summary>
        private static Hashtable _itemLastSync = Hashtable.Synchronized(new Hashtable());

        /// <summary>
        /// 存储所有item的更新次数
        /// </summary>
        private static Hashtable _itemUpdateCount = Hashtable.Synchronized(new Hashtable());

        /// <summary>
        /// 存储所有item的读取次数
        /// </summary>
        private static Hashtable _itemReadCount = Hashtable.Synchronized(new Hashtable());

        /// <summary>
        /// 存储所有item的描述信息
        /// </summary>
        private static Hashtable _itemDesc = Hashtable.Synchronized(new Hashtable());

        /// <summary>
        /// 服务器TCP监听器
        /// </summary>
        private static TCPListener _listener = null;

        /// <summary>
        /// 服务器版本号
        /// </summary>
        private static string _version = "";

        /// <summary>
        /// KEPWARE连接器版本号(OPCAutomationLib.dll)
        /// </summary>
        private static string _version1 = "";


        /// <summary>
        /// 主程序
        /// </summary>
        /// <param name="args">命令行参数列表</param>
        static void Main (string[] args)
        {
            // 处理所有未捕获的异常
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;


            // 获取服务器和连接器版本
            Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            _version = assembly.GetName().Version.ToString();
            Assembly assembly1 = Assembly.GetAssembly(typeof(KEPWareController));
            _version1 = assembly1.GetName().Version.ToString();

            // 让log4net监控配置文件的变化
            FileInfo appConfigInfo = new FileInfo(_appConfig);
            log4net.Config.XmlConfigurator.ConfigureAndWatch(appConfigInfo);

            Console.WriteLine("****************************************************************");
            Console.WriteLine("OPC代理服务器");
            Console.WriteLine("服务器版本：" + _version);
            Console.WriteLine("Kepware连接器版本:" + _version1);
            Console.WriteLine("GNU GENERAL PUBLIC LICENSE V3");
            Console.WriteLine("****************************************************************");
            Console.WriteLine("");

            // 检查命令行参数，输出用法信息
            if (args.Length == 0)
            {
                Console.WriteLine(@"
错误：未指定有效的启动参数，正确的使用方法如下： opcservice -c <OPC配置文件名.xml> [-d] [-s]
-c <OPC配置文件名> 必须参数，指定服务器加载的配置文件定义
-d 调试模式，添加此参数服务器将输出更详细的日志
-s 服务模式，添加此参数服务器已静默方式启动，将关闭控制台及日志输出（文件日志不受影响）
                ");
                Console.ReadKey();
                Environment.Exit(0);
            }

            // 处理命令行参数
            for (int n = 0; n < args.Length; n++)
            {
                // 处理要加载的配置文件名
                if (args[n] == "-c" || args[n] == "/c") configFile = args[n+1];

                // 是否启用debug模式（将输出更详细的调试日志）
                if (args[n] == "-d" || args[n] == "/d") _debug = true;

                // 是否启用服务模式 （没有控制台)
                if (args[n] == "-s" || args[n] == "/c") _daemon = true;
            }

            // 服务模式重定向控制台输出到标准错误
            // 并且取消控制台窗口
            if (_daemon)
            {
                Console.WriteLine("\n\r请点击关闭按钮关闭此控制台窗口进入无人值守模式!");
                Console.SetOut(new StreamWriter(Console.OpenStandardError(), System.Text.Encoding.UTF8));
                FreeConsole();
            }



            List<string> blockNames = new List<string>();
            Hashtable blockTypes = new Hashtable();
            Hashtable blockQualities = new Hashtable();

            // 从配置文件中加载监听设置
            if (!LoadConfiguration(configFile, ref _host, ref _server, ref blockTypes, ref blockNames, ref _channelName, ref _deviceName, ref _port, ref _persistence, ref _qualities, ref _clientTimeout, ref _itemLastUpdate, ref _itemReadCount, ref _itemUpdateCount, ref _itemLastSync, ref _itemDesc))
            {
                log.Error("配置文件读取错误！请检查文件格式再试！");
                Environment.Exit(-1);
            }

            // 服务器启动
            log.Info("服务器正在启动");

            // 查找OPC服务
            log.Info("开始查找本机安装的KEPWARE服务");
            List<string> servers = controller.FindServers();
            if (servers.Count < 1)
            {
                log.Error("没有发现本机安装了可用的OPC服务器，服务器将退出");
                Environment.Exit(-1);
            }

            // 检查本机是否存在配置文件中指定的KEPWARE服务器
            bool found = false;
            foreach (string s in servers)
            {
                if (s == _server) found = true;
            }

            if (!found)
            {
                log.Error("没有找到配置中指定的KEPWARE服务器 " + _server);
                Environment.Exit(-1);
            }


            // 开始订阅OPC更新
            log.Info("检测到OPC服务器 " + _server + ", 正在连接");
            InitializeSubscription(_server, _channelName, _deviceName, blockNames, blockTypes);

            // 启动网络组件
            _listener = new TCPListener(_port, _clientTimeout);
            _listener.OPCItemUpdated +=Listener_OPCItemUpdated;
            _listener.Start();

            log.Info("网络服务组件已启动");

            // 服务模式不使用命令行界面
            if (_daemon) Thread.Sleep(Timeout.Infinite);

            #region 命令行控制界面
            Console.WriteLine("");
            Console.WriteLine("请输入命令(输入help或者?查看可用命令列表)：");
            while (true)
            {
                Console.Write(configFile.ToUpper().Replace(".XML","") + "> ");
                string cmd2 = Console.ReadLine();
                string cmd = cmd2.ToLower();

                // 命令解释器
                if (cmd == "?" || cmd =="help")
                {
                    ShowHelp();

                }
                else if (cmd == "reload")
                {
                    Console.Write("确定要从配置文件中重新加载配置吗？(y/n):");
                    if (Console.ReadLine().ToLower() == "y")
                    {
                        _reloading = true;
                        LoadConfiguration(configFile, ref _host, ref _server, ref blockTypes, ref blockNames, ref _channelName, ref _deviceName, ref _port, ref _persistence, ref _qualities, ref _clientTimeout, ref _itemLastUpdate, ref _itemReadCount, ref _itemUpdateCount, ref _itemLastSync, ref _itemDesc);
                        InitializeSubscription(_server, _channelName, _deviceName, blockNames, blockTypes);
                        _reloading = false;
                        Console.WriteLine("设置已重新加载!(请注意如果修改了监听端口号，必须关闭并重启服务程序！)");
                    }

                }
                else if (cmd =="exit")
                {
                    Console.Write("确定要退出OPC服务器吗？生产线和MES的通信将完全中断！(y/n):");
                    if (Console.ReadLine().ToLower() == "y")
                    {
                        _listener.Terminate();
                        controller.Disconnect();
                        Console.Write("服务已关闭，按任意键退出 ...");
                        Console.ReadKey();
                        Environment.Exit(0);
                    }
                }
                else if (cmd == "show")
                {
                    Console.WriteLine();
                    ShowTagList();
                    Console.WriteLine();
                }
                else if (cmd.StartsWith("set"))
                {
                    if (cmd.Split(' ').Length < 2)
                    {
                        Console.WriteLine("命令语法错误! 输入help查看用法");
                        continue;
                    }
                    try
                    {
                        string blockName = cmd2.Replace("=", " ").Split(' ')[1];
                        string blockValue = cmd2.Replace("=", " ").Split(' ')[2];
                        Console.Write("确定要将OPCITEM " + blockName + " 的值更改为 " + blockValue +" 吗?(y/n):");
                        if (Console.ReadLine().ToLower() == "y")
                            Listener_OPCItemUpdated(blockName, blockValue, "STRING");
                    }
                    catch
                    {
                        Console.WriteLine("命令语法错误! 输入help查看用法");
                        continue;
                    }
                }
                else if (cmd == "save")
                {
                    string filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DateTime.Now.ToString("yyyyMMddHHmmss") + ".opc");
                    ValuesContainer.Persist(filename);
                    Console.WriteLine("文件已保存到 " + filename);

                }
                else if (cmd.StartsWith("load"))
                {
                    if (cmd.Split(' ').Length < 2)
                    {
                        Console.WriteLine("命令语法错误! 输入help查看用法");
                        continue;
                    }
                    string filename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, cmd2.Split(' ')[1]);
                    if (File.Exists(filename))
                    {
                        ValuesContainer.Load(filename, true);
                        Console.WriteLine("OPC文件已加载并应用到所有的Item");
                    }
                    else
                    {
                        Console.WriteLine("指定的文件 " + filename + " 不存在!");
                    }
                }
                else if (cmd == "status")
                {
                    Console.WriteLine("");
                    TimeSpan span = DateTime.Now.Subtract(_start);
                    Console.WriteLine("服务器版本：" + _version);
                    Console.WriteLine("KEPWARE连接器版本：" + _version1);
                    Console.WriteLine("服务器地址：" + GetIPAddress() + ":" + _port);
                    Console.WriteLine("配置文件：" + configFile);
                    Console.WriteLine("通道名称：" + _channelName);
                    Console.WriteLine("设备名称：" + _deviceName);
                    Console.WriteLine("Kepware主机：" + _host);
                    Console.WriteLine("Kepware服务：" + _server);
                    Console.WriteLine("持久化存储：" + _persistence.ToString());
                    Console.WriteLine("客户端无通信超时：" + _clientTimeout + " 秒");
                    Console.WriteLine("OPC组名称：" + controller.GroupName);
                    Console.WriteLine("已正常运行：" + span.Days + "天" + span.Hours + "小时" + span.Minutes + "分钟" + span.Seconds + "秒");
                    Console.WriteLine("当连接客户端：" + _listener.Clients.Count + "个");
                    for(int n = 0; n < _listener.Clients.Count; n++)
                    {
                        TimeSpan heartbeatTs = DateTime.Now.Subtract(_listener.Clients[n].LastHeartbeat);
                        string heartbeat = heartbeatTs.Minutes + "分" + heartbeatTs.Seconds + "秒" + heartbeatTs.Milliseconds + "毫秒";
                        Console.WriteLine(_listener.Clients[n].IPAddress + " - 上次通信：" + heartbeat);
                    }
                    Console.WriteLine("");
                }
                else if (cmd == "clear")
                {
                    Console.Clear();
                }
                else if (cmd == "monitor")
                {
                    // 启动监视器
                    _monitor = new Thread(new ThreadStart(MonitorHandler));
                    _monitor.IsBackground = true;
                    _monitor.Start();

                    // 控制刷新速度
                    bool quit = false;
                    while (!quit)
                    {
                        ConsoleKeyInfo info = Console.ReadKey();
                        if (info.KeyChar == '=' || info.KeyChar == '+')
                        {
                            // 增加刷新速度
                            if (_refreshRate >= 150) _refreshRate -= 100;
                            else { if (_refreshRate > 20) _refreshRate -= 20; }

                        } else if (info.KeyChar == '-')
                        {
                            // 降低刷新速度
                            if (_refreshRate < 150) _refreshRate += 20;
                            else { _refreshRate += 100; }

                        } else if (info.KeyChar == 'c' || info.KeyChar == 'C')
                        {
                            // 退出监视器
                            quit = true;
                            Console.Clear();
                        }
                    }

                    _monitor.Abort();
                    _monitor = null;
                }
                else if (cmd == "") { }
                else
                {
                    ShowHelp();
                }

            }
            #endregion
        }

        /// <summary>
        /// 处理所有未捕获的异常，防止服务器crash
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void CurrentDomain_UnhandledException (object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            log.Error("服务器AppDomain发生未捕获的异常!");
            string message = ex.ToString();
            foreach (string line in message.Split('\r'))
            {
                log.Error(line);
            }
        }


        /// <summary>
        /// 监视器线程
        /// </summary>
        private static void MonitorHandler()
        {
            while (true)
            {
                TimeSpan span = DateTime.Now.Subtract(_start);
                Console.Clear();
                Console.WriteLine("KEPWARE OPC标记状态监视器");
                Console.WriteLine("监控区域: " + configFile.ToUpper().Replace(".XML", "") + "." + _channelName + "." + _deviceName);
                Console.WriteLine("服务器已运行: " + span.Days + "天" + span.Hours + "小时" + span.Minutes + "分钟" + span.Seconds + "秒");
                Console.WriteLine("刷新间隔：" + _refreshRate + " 毫秒\n\r");
                ShowTagList(true);
                Thread.Sleep(_refreshRate);
            }
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private static void ShowHelp()
        {
            Console.WriteLine(@"
  OPC服务器运行时支持的命令列表如下：

    * help/?                - 查看帮助信息
    * status                - 查看服务器运行状态
    * reload                - 重新从配置文件中加载所有配置并立刻使用新配置进行服务
    * exit                  - 退出OPC服务器
    * show                  - 显示所有OPCItem的值
    * monitor               - 监视所有item的变化
    * set <item>=<value>    - 更改OPCItem的值 （用法： set OPCItem名称=要写入的值, 注意OPCItem名称区分大小写）
    * save                  - 将当前的OPCItem值写入磁盘文件中（存放在服务器所在文件夹，以 yyyyMMddHHmmss.opc 命名
    * load <filename.opc>   - 载入服务器目录下保存的opc文件并将其中的值恢复到所有OPCItem中
    * clear                 - 清除控制台输出
                    ");
        }

        /// <summary>
        /// 显示所有标记状态
        /// </summary>
        /// <param name="monitor">监视器模式</param>
        private static void ShowTagList (bool monitor = false)
        {
            string[] tail = new string[] {"|", "/", "--", "\\"};
            Hashtable values = (Hashtable)ValuesContainer.Values.Clone();
            int maxlen = 0;
            foreach (string key in values.Keys) { if (key.Length > maxlen) maxlen = key.Length; }

            Console.WriteLine(FixWidth("KepwareTagName", 32) + FixWidth("Value", 12) + FixWidth("Type", 10) + FixWidth("Quality", 11) + FixWidth("Reads", 10) + FixWidth("Writes", 10)  + FixWidth("LastWrite", 16) + FixWidth(" LastKEPSync",16));
            Console.WriteLine("**********************************************************************************************************************");


            foreach (string key in values.Keys)
            {
                TimeSpan span = DateTime.Now.Subtract((DateTime)ValuesContainer.LastUpdates[key]);
                string timestr = "-" + new string(' ',16);
                TimeSpan span1 = DateTime.Now.Subtract((DateTime)ValuesContainer.LastSyncs[key]);
                string timestr1 = "-" + new string(' ', 16);
                if ((DateTime)ValuesContainer.LastUpdates[key] != DateTime.MinValue)
                    timestr = FixWidth(span.Minutes + "分" + span.Seconds + "秒" + span.Milliseconds + "毫秒", 16);
                if ((DateTime)ValuesContainer.LastSyncs[key] != DateTime.MinValue)
                    timestr1 = FixWidth(span1.Minutes + "分" + span1.Seconds + "秒" + span1.Milliseconds + "毫秒", 16);

                string keyStr = key;
                if (ValuesContainer.ItemDescriptions[key].ToString() != "") keyStr = ValuesContainer.ItemDescriptions[key].ToString();
                Console.WriteLine(FixWidth(keyStr, 32) + FixWidth(ValuesContainer.Values[key].ToString(), 12) + FixWidth(ValuesContainer.Types[key].ToString(), 10) + FixWidth(ValuesContainer.Qualities[key].ToString(), 11) + FixWidth(ValuesContainer.ReadCounts[key].ToString(), 10) + FixWidth(ValuesContainer.WriteCounts[key].ToString(), 10) + timestr + timestr1);
            }

            int t = DateTime.Now.Second % 4;
            Console.ForegroundColor = ConsoleColor.Yellow;
            if (monitor) Console.WriteLine("\n\rOPC监视器运行中 [按+加快速度,按-减低速度,按c退出]  " + tail[t] + "\n\r");
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        /// <summary>
        /// 获取本机的IPV4地址
        /// </summary>
        /// <returns></returns>
        private static string GetIPAddress()
        {
            foreach (IPAddress addr in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (addr.ToString().Length <= 15 && addr.ToString() != "127.0.0.1") return addr.ToString();
            }
            return "127.0.0.1";
        }

        /// <summary>
        /// 在KEPWARE服务器上设置订阅
        /// </summary>
        /// <param name="server">KEPWARE服务器名称</param>
        /// <param name="channelName">订阅的通道</param>
        /// <param name="deviceName">订阅的设备</param>
        /// <param name="blockNames">要订阅的ITEM名称集</param>
        /// <param name="blockTypes">要订阅的ITEM数据类型集</param>
        private static void InitializeSubscription(string server, string channelName, string deviceName, List<string> blockNames, Hashtable blockTypes)
        {
            // 重新创建KEPWARE控制器对象
            if (controller.Connected)
            {
                controller.Disconnect();
                log.Info("已从KEPWARE服务断开");
                controller = null;
                controller = new KEPWareController();
                controller.BadBlockDetected +=Controller_BadBlockDetected;
            }

            // 连接到OPC服务
            string serverName = server;

            if (controller.Connect(serverName))
            {
                log.Info("已连接到KEPWARE服务" + serverName);
            }
            else
            {
                log.Error("连接失败！服务器将退出！");
                Environment.Exit(-1);
            }


            // 设置OPC订阅信息
            controller.ChannelName = channelName;
            log.Info("已将通道名称设置为 " + controller.ChannelName);
            controller.DeviceName = deviceName;
            log.Info("已将设备名称设置为 " + controller.DeviceName);
            controller.AddItems(blockNames.ToArray());
            controller.DataChange+=Controller_DataChange;

            // 初始化全局结果集
            Hashtable hashtable = new Hashtable();
            foreach (string key in blockNames) { hashtable.Add(key, ""); }
            ValuesContainer.Values = hashtable;
            ValuesContainer.Types = blockTypes;
            ValuesContainer.Qualities = _qualities;
            ValuesContainer.ReadCounts = _itemReadCount;
            ValuesContainer.WriteCounts = _itemUpdateCount;
            ValuesContainer.LastUpdates = _itemLastUpdate;
            ValuesContainer.LastSyncs = _itemLastSync;
            ValuesContainer.ItemDescriptions = _itemDesc;

            string persistFile = configFile.Replace(".xml", ".opc");
            // 尝试读取之前保存的OPC状态
            try
            {
                // 检查服务器是否启用持久化
                if (_persistence)
                {
                    ValuesContainer.Load(persistFile, true);
                    log.Info("已恢复持久化文件 " + persistFile);

                }
            }
            catch (Exception ex)
            {
                log.Error("读取持久化文件 " + persistFile + " 出错:" + ex.Message);
            }

            // 启动监听服务
            try
            {
                controller.Subscribe();
                log.Info("OPC监听服务已启动");
            }
            catch (Exception ex)
            {
                log.Info("无法启动监听服务！" + ex.Message);
                controller.Disconnect();
                Environment.Exit(-1);
            }
        }

        /// <summary>
        /// 把连接点的品质更新保存到全局结果集
        /// </summary>
        /// <param name="blockName">数据块名称</param>
        /// <param name="status">链接品质编码</param>
        private static void Controller_BadBlockDetected (string blockName, int status)
        {
            ValuesContainer.Qualities[blockName] = status;
        }

        /// <summary>
        /// 处理TCP监听线程返回的ITEM更新事件
        /// </summary>
        /// <param name="blockName"></param>
        /// <param name="newValue"></param>
        public static void Listener_OPCItemUpdated (string blockName, string newValue, string clientType)
        {

            if (!ValuesContainer.Values.ContainsKey(blockName))
            {
                log.Error("OPC服务器未配置此OPCItem " + blockName);
                return;
            }

            string type = ValuesContainer.Types[blockName].ToString();
            while(_reloading) { System.Threading.Thread.Sleep(20); }
            try
            {
                bool writeStatus = false;
                log.Info("正在更新OPCItem " + blockName + ":" + type +  "=" + newValue);
                switch (type)
                {
                    case "BOOL":
                        writeStatus = controller.SetValue(blockName, bool.Parse(newValue));
                        break;
                    case "BYTE":
                        writeStatus = controller.SetValue(blockName, byte.Parse(newValue));
                        break;
                    case "CHAR":
                        writeStatus = controller.SetValue(blockName, char.Parse(newValue));
                        break;
                    case "WORD":
                        writeStatus = controller.SetValue(blockName, short.Parse(newValue));
                        break;
                    case "LONG":
                        writeStatus = controller.SetValue(blockName, int.Parse(newValue));
                        break;
                    case "DWORD":
                        writeStatus = controller.SetValue(blockName, int.Parse(newValue));
                        break;
                    case "STRING":
                        writeStatus = controller.SetValue(blockName, newValue);
                        break;
                }

                if (writeStatus)
                {
                    ValuesContainer.Values[blockName] = newValue;
                    ValuesContainer.LastUpdates[blockName] = DateTime.Now;
                    ValuesContainer.WriteCounts[blockName] = (int)ValuesContainer.WriteCounts[blockName] + 1;
                    if (_persistence) ValuesContainer.Persist(configFile.Replace(".xml", ".opc"));
                }
                else
                    log.Error("写入OPCITEM " + blockName + ":" + type + " 失败，更改已经被取消");

            }
            catch (Exception ex)
            {
                log.Error("写入OPCITEM " + blockName + ":" + type + " 失败: " + ex.Message);
            }

        }

        /// <summary>
        /// 把更新的OPC ITEM数据保存到全局容器以供网络服务线程访问
        /// </summary>
        /// <param name="values">更新的结果集</param>
        private static void Controller_DataChange (Dictionary<string, string> values)
        {
            Hashtable hashtable = new Hashtable();
            foreach (string key in values.Keys)
            {
                hashtable.Add(key, values[key]);
            }

            log.Debug("接收到KEPWARE的更新数据");
            foreach (string key in ValuesContainer.Values.Keys)
            {
                if (ValuesContainer.Values[key].ToString() != hashtable[key].ToString())
                {
                    ValuesContainer.LastSyncs[key] = DateTime.Now;

                    if (_debug)
                        log.Debug(key + ": " + ValuesContainer.Values[key] + "=>" + hashtable[key]);
                }
            }

            foreach (string key in hashtable.Keys)
            {
                ValuesContainer.Values[key] = hashtable[key].ToString();
            }

            if (_persistence) ValuesContainer.Persist(configFile.Replace(".xml", ".opc"));

        }


        /// <summary>
        /// 从配置文件中加载OPC监控信息
        /// </summary>
        /// <param name="blockNames">传出所有要监控的地址块</param>
        /// <param name="channelName">传出要监控的通道名称</param>
        /// <param name="deviceName">传出要监控的设备名称</param>
        /// <returns>指示配置文件是否加载成功</returns>
        private static bool LoadConfiguration (string configFile, ref string host, ref string server, ref Hashtable blockTypes, ref List<string> blockNames, ref string channelName, ref string deviceName, ref int port, ref bool persistence, ref Hashtable qualities, ref int clientTimeout, ref Hashtable lastUpdates, ref Hashtable readCount, ref Hashtable writeCount, ref Hashtable lastSync, ref Hashtable itemDesc)
        {
            blockTypes.Clear();
            blockNames.Clear();
            qualities.Clear();
            lastUpdates.Clear();
            readCount.Clear();
            writeCount.Clear();
            lastSync.Clear();
            itemDesc.Clear();

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile);
            log.Info("开始加载配置文件 " + configPath);

            if (!File.Exists(configPath))
            {
                log.Error("加载配置文件 " + configPath +"错误！文件不存在");
                Environment.Exit(-1);
            }

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(configPath);
            } catch (Exception ex)
            {
                log.Error("读取配置文件 " + configPath + "出错!" + ex.Message);
                Environment.Exit(-1);
            }

            try
            {
                channelName = doc.DocumentElement.Attributes.GetNamedItem("channel").InnerText;
                deviceName =  doc.DocumentElement.Attributes.GetNamedItem("device").InnerText;
                host = doc.DocumentElement.Attributes.GetNamedItem("host").InnerText;
                server = doc.DocumentElement.Attributes.GetNamedItem("server").InnerText;
                port = int.Parse(doc.DocumentElement.Attributes.GetNamedItem("port").InnerText);
                persistence = bool.Parse(doc.DocumentElement.Attributes.GetNamedItem("persistence").InnerText);
                clientTimeout = int.Parse(doc.DocumentElement.Attributes.GetNamedItem("timeout").InnerText);

                // 更新监听器的客户端无传输超时断开设置
                try
                {
                    _listener.ClientTimeout = clientTimeout;
                }
                catch { }
               
            } catch
            {
                log.Error("配置文件中缺少关键配置项，请检查opcservice标记是否配置了channel, device, host, server, port, persistence, timeout 等关键属性");
                Environment.Exit(-1);
            }

            foreach (XmlNode node in doc.DocumentElement.SelectNodes("item[@enabled='1']"))
            {
                blockNames.Add(node.Attributes.GetNamedItem("name").InnerText);
                blockTypes.Add(node.Attributes.GetNamedItem("name").InnerText, node.Attributes.GetNamedItem("type").InnerText);
                qualities.Add(node.Attributes.GetNamedItem("name").InnerText, OPCQualities.Good);
                lastUpdates.Add(node.Attributes.GetNamedItem("name").InnerText, DateTime.MinValue);
                readCount.Add(node.Attributes.GetNamedItem("name").InnerText, 0);
                writeCount.Add(node.Attributes.GetNamedItem("name").InnerText, 0);
                lastSync.Add(node.Attributes.GetNamedItem("name").InnerText, DateTime.MinValue);
                itemDesc.Add(node.Attributes.GetNamedItem("name").InnerText, node.InnerText);
                log.Info("已配置OPCITEM: " + node.Attributes.GetNamedItem("name").InnerText + ":" + node.Attributes.GetNamedItem("type").InnerText);

            }

            return true;
        }

        /// <summary>
        /// 把指定的字符串调整至规定长度（补空格或者截断）
        /// </summary>
        /// <param name="s">要调整的字符串</param>
        /// <param name="targetWidth">目标长度</param>
        /// <returns></returns>
        private static string FixWidth(string s, int targetWidth)
        {
            string ret = s;
            int different = targetWidth - System.Text.Encoding.GetEncoding("GB2312").GetByteCount(s);
            string postfix = "";

            if (different > 0) {
                postfix = new string(' ', different);
            } else if (different < 0)
            {
                ret = ret.Substring(0, ret.Length - different);
            }
            return ret + postfix;
        }

    }
}
