using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using OPCAutomation;
using System.Net;
using log4net;
using Microsoft.CSharp;

namespace OPCAutomation
{
    public delegate void DataChangedEventHandler (Dictionary<string, string> values);
    public delegate void BadBlockDetectedHandler (string blockName, int status);
    /// <summary>
    /// 此类用于连接KEPWARE OPC服务器并监听或设置OPC ITEM的值
    /// </summary>
    public class KEPWareController
    {
        #region 事件定义
        /// <summary>
        /// 当KEPWARE端OPCITEM的值发生变化时触发此事件
        /// </summary>
        public event DataChangedEventHandler DataChange;

        /// <summary>
        /// 当KEPWARE端OPCITEM的值发生变化时触发此事件
        /// </summary>
        /// <param name="values">最新的OPCITEM值列表副本（包含所有ITEM）</param>
        protected virtual void OnDataChanged(Dictionary<string, string> values) {
            if (DataChange != null) DataChange(values);
        }

        /// <summary>
        /// 当读取或写入时发现地址块状态不正常时触发此事件
        /// </summary>
        public event BadBlockDetectedHandler BadBlockDetected;

        /// <summary>
        /// 当KEPWARE端OPCITEM的值发生变化时触发此事件
        /// </summary>
        /// <param name="blockName">数据块名称</param>
        /// <param name="status">数据块的Quality编码</param>
        protected virtual void OnBadBlockDetected(string blockName, int status)
        {
            if (BadBlockDetected != null) BadBlockDetected(blockName, status);
        }

        #endregion

        /// <summary>
        /// 日志输出
        /// </summary>
        private static log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// OPC 服务器
        /// </summary>
        private OPCServer _server = new OPCServer();

        //private OPCServerClass _server = new OPCServerClass();

       

        /// <summary>
        /// OPC 组
        /// </summary>
        private OPCGroup _group = null;

        /// <summary>
        /// OPC组名称
        /// </summary>
        private string _groupName = "";

        /// <summary>
        /// 监听的OPCItem
        /// </summary>
        private OPCItems _items = null;

        /// <summary>
        /// 要监听的ITEM名称
        /// </summary>
        private string[] _itemNames = new string[0];

        /// <summary>
        /// 监听的所有item的客户端句柄
        /// </summary>
        private int[] _itemClientHandles = new int[0];

        /// <summary>
        /// 监听的所有item的服务器句柄
        /// </summary>
        private Dictionary<string, int> _itemServerHandles = new Dictionary<string, int>();

        /// <summary>
        /// 监听的所有item的最新值列表
        /// </summary>
        private Dictionary<string, string> _itemValues = new Dictionary<string, string>();

        /// <summary>
        /// 最大尝试写入OPC次数
        /// </summary>
        private int _writeCount = 3;

        /// <summary>
        /// 获取指定的ITEM值
        /// </summary>
        /// <param name="block">指定ITEM的名称（e.g. DB50.DBX0.1)</param>
        /// <returns></returns>
        public string GetValue(string block)
        {
            
            return _itemValues[block];
        }

        public OPCItems Items { get { return _items; } }

        public string GroupName { get { return _groupName; } }



        #region 更新OPCItem
        /// <summary>
        /// 为item指定字符串类型的值
        /// </summary>
        /// <param name="block">ITEM名称</param>
        /// <param name="value">指定的值</param>
        public bool SetValue (string block, string value)
        {
            // 获取要更新的item
            OPCItem bItem = null;

            try
            {
                bItem = _items.GetOPCItem(_itemServerHandles[block]);
            }
            catch (Exception ex)
            {
                log.Error("获取opcitem " + block + "出错: " + ex.Message);
            }

            // 检查item的连接品质
            if (bItem.Quality != (int)OPCQualities.Good)
            {
                log.Warn("地址块 " + block + " 的连接品质为 " +  ((OPCQualities)bItem.Quality).ToString() + " 更新可能无法写入");
                OnBadBlockDetected(block, bItem.Quality);
            }

            // 3次尝试更新item
            int writeCount = 0;
            do
            {
                try
                {
                    bItem.Write(value);
                }
                catch (Exception ex)
                {
                    log.Error("将值" + value + " 写入 " + block +" 时出错：" + ex.Message);
                    return false;
                }
                writeCount += 1;
                log.Debug("正在尝试第 " + writeCount + " 次写入opc " + block);
            } while ((string)bItem.Value != value && writeCount < _writeCount);

            return true;
}

        /// <summary>
        /// 为item指定WORD(16Bit)类型的值
        /// </summary>
        /// <param name="block">ITEM名称</param>
        /// <param name="value">指定的值</param>
        public bool SetValue (string block, short value)
        {
            // 获取要更新的item
            OPCItem bItem = null;

            try
            {
                bItem = _items.GetOPCItem(_itemServerHandles[block]);
            }
            catch (Exception ex)
            {
                log.Error("获取opcitem " + block + "出错: " + ex.Message);
            }

            // 检查item的连接品质
            if (bItem.Quality != (int)OPCQualities.Good)
            {
                log.Warn("地址块 " + block + " 的连接品质为 " +  ((OPCQualities)bItem.Quality).ToString() + " 更新可能无法写入");
                OnBadBlockDetected(block, bItem.Quality);
            }

            // 3次尝试更新item
            int writeCount = 0;
            do
            {
                try
                {
                    bItem.Write(value);
                }
                catch (Exception ex)
                {
                    log.Error("将值" + value + " 写入 " + block +" 时出错：" + ex.Message);
                    return false;
                }
                writeCount += 1;
                log.Debug("正在尝试第 " + writeCount + " 次写入opc " + block);

            } while (short.Parse(bItem.Value.ToString()) != value && writeCount < _writeCount);

            return true;
        }

        /// <summary>
        /// 为item指定BYTE(8Bit)类型的值
        /// </summary>
        /// <param name="block">ITEM名称</param>
        /// <param name="value">指定的值</param>
        public bool SetValue (string block, byte value)
        {
            // 获取要更新的item
            OPCItem bItem = null;

            try
            {
                bItem = _items.GetOPCItem(_itemServerHandles[block]);
            }
            catch (Exception ex)
            {
                log.Error("获取Item " + block + "出错: " + ex.Message);
            }

            // 检查item的连接品质
            if (bItem.Quality != (int)OPCQualities.Good)
            {
                log.Warn("地址块 " + block + " 的连接品质为 " +  ((OPCQualities)bItem.Quality).ToString() + " 更新可能无法写入");
                OnBadBlockDetected(block, bItem.Quality);
            }

            // 3次尝试更新item
            int writeCount = 0;
            do
            {
                try
                {
                    bItem.Write(value);
                }
                catch (Exception ex)
                {
                    log.Error("将值" + value + " 写入 " + block +" 时出错：" + ex.Message);
                    return false;
                }
                writeCount += 1;
                log.Debug("正在尝试第 " + writeCount + " 次写入opc " + block);
            } while (byte.Parse(bItem.Value.ToString()) != value && writeCount < _writeCount);

            return true;
        }

        /// <summary>
        /// 为item指定BOOL(1Bit)类型的值
        /// </summary>
        /// <param name="block">ITEM名称</param>
        /// <param name="value">指定的值</param>
        public bool SetValue (string block, bool value)
        {
            // 获取要更新的item
            OPCItem bItem = null;

            try
            {
                bItem = _items.GetOPCItem(_itemServerHandles[block]);
            }
            catch (Exception ex)
            {
                log.Error("获取opcitem " + block + "出错: " + ex.Message);
            }

            // 检查item的连接品质
            if (bItem.Quality != (int)OPCQualities.Good)
            {
                log.Warn("地址块 " + block + " 的连接品质为 " +  ((OPCQualities)bItem.Quality).ToString() + " 更新可能无法写入");
                OnBadBlockDetected(block, bItem.Quality);
            }

            // 3次尝试更新item
            int writeCount = 0;
            do
            {
                try
                {
                    bItem.Write(value ? "1" : "0");
                }
                catch (Exception ex)
                {
                    log.Error("将值" + value + " 写入 " + block +" 时出错：" + ex.Message);
                    return false;
                }
                writeCount += 1;
                log.Debug("正在尝试第 " + writeCount + " 次写入opc " + block);
            } while ((bItem.Value.ToString() == "1") != value && writeCount < _writeCount);

            return true;
        }

        /// <summary>
        /// 为item指定LONG(32Bits)类型的值
        /// </summary>
        /// <param name="block">ITEM名称</param>
        /// <param name="value">指定的值</param>
        public bool SetValue (string block, int value)
        {
            // 获取要更新的item
            OPCItem bItem = null;

            try
            {
                bItem = _items.GetOPCItem(_itemServerHandles[block]);
            }
            catch (Exception ex)
            {
                log.Error("获取opcitem " + block + "出错: " + ex.Message);
            }

            // 检查item的连接品质
            if (bItem.Quality != (int)OPCQualities.Good)
            {
                log.Warn("地址块 " + block + " 的连接品质为 " +  ((OPCQualities)bItem.Quality).ToString() + " 更新可能无法写入");
                OnBadBlockDetected(block, bItem.Quality);
            }

            // 3次尝试更新item
            int writeCount = 0;
            do
            {
                try
                {
                    bItem.Write(value);
                }
                catch (Exception ex)
                {
                    log.Error("将值" + value + " 写入 " + block +" 时出错：" + ex.Message);
                    return false;
                }
                writeCount += 1;
                log.Debug("正在尝试第 " + writeCount + " 次写入opc " + block);
            } while (int.Parse(bItem.Value.ToString()) != value && writeCount < 10);
            return true;
        }
        #endregion

        #region 属性定义

        /// <summary>
        /// 指示是否已连接到KEPWARE
        /// </summary>
        public bool Connected { get; set; } = false;

        /// <summary>
        /// 指示对象要监控的通道名称（对应KEPWARE中建立的channel）
        /// </summary>
        public string ChannelName { get; set; } = "";

        /// <summary>
        /// 指示对象要监控的设备名称（对应KEPWARE中建立的Device)
        /// </summary>
        public string DeviceName { get; set; } = "";

        /// <summary>
        /// 指示对象连接到的KEPWARE服务器名称
        /// </summary>
        public string ServerName { get; set; } = "KEPware.KEPServerEx.V4";

        /// <summary>
        /// 指示KEPWARE服务器当前的状态
        /// </summary>
        public int ServerState { get { return _server.ServerState; } }

        #endregion

        /// <summary>
        /// 添加一个要监听的OPC ITEM
        /// </summary>
        /// <param name="blockName">item名称</param>
        public void AddItem(string blockName)
        {
            string[] newItemNames = new string[_itemNames.Length+1];
            Array.Copy(_itemNames, newItemNames, _itemNames.Length);
            newItemNames[newItemNames.Length-1] = blockName;
            _itemNames = newItemNames;

            int[] newItemClientHandles = new int[_itemClientHandles.Length+1];
            Array.Copy(_itemClientHandles, newItemClientHandles, _itemClientHandles.Length);
            newItemClientHandles[newItemClientHandles.Length-1] = _itemNames.Length;
            _itemClientHandles = newItemClientHandles;

            _itemValues.Add(blockName, "");
        }

        /// <summary>
        /// 添加一组要监听的OPC ITEM
        /// </summary>
        /// <param name="blockNames">item名称</param>
        public void AddItems(string[] blockNames)
        {
            foreach (string s in blockNames) { this.AddItem(s); }
        }


        
        /// <summary>
        /// 查找本地计算机上运行的所有OPC服务器名称
        /// </summary>
        /// <returns></returns>
        public List<string> FindServers()
        {
            List<string> ret = new List<string>();
            string hostIP = "";
            //获取本地计算机IP,计算机名称
            IPHostEntry IPHost = Dns.GetHostEntry(Environment.MachineName);
            if (IPHost.AddressList.Length>0) hostIP=IPHost.AddressList[0].ToString();

            object servers = _server.GetOPCServers(hostIP);

            foreach (string server in (Array)servers)
            {
                ret.Add(server);
            }
            log.Info("已查找到所有本机安装的KEPWARE服务器");
            return ret;
        }

        /// <summary>
        /// 连接到指定的OPC服务器
        /// </summary>
        /// <param name="name">服务器名称</param>
        /// <returns></returns>
        public bool Connect(string name)
        {
            try
            {
                _server.Connect(name);
                this.Connected = true;
                this.ServerName = name;
                log.Info("已连接到KEPWARE服务器" + this.ServerName);
                

            } catch(Exception ex) {
                log.Error("连接到KEPWARE出错:" + ex.Source + ":" + ex.Message);
                log.Error(ex.StackTrace);
                return false;
            }

            try
            {

                // 创建组对象
                _groupName = "ESTSHOPCSVC." + new Random().Next();
                _group = _server.OPCGroups.Add(_groupName);
                _server.OPCGroups.DefaultGroupIsActive = true;
                _group.UpdateRate = 10;
                _group.IsActive = true;
                _group.IsSubscribed = true;
                _group.DeadBand = 0;

                _group.DataChange+=OPCGroup_DataChange;

                _items = _group.OPCItems;
            } catch (Exception ex)
            {
                log.Error("加入kepware组异常:" + ex.Source + ":" + ex.Message);
                log.Error(ex.StackTrace);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 连接到默认的KEPWARE OPC服务器
        /// </summary>
        /// <returns></returns>
        public bool Connect ()
        {
            return Connect(this.ServerName);
        }

        /// <summary>
        /// 开始监听PLC设备的数据更新
        /// </summary>
        /// <returns></returns>
        public bool Subscribe ()
        {
            if (string.IsNullOrEmpty(this.ChannelName) || string.IsNullOrEmpty(this.DeviceName))
                throw new Exception("通道名称(ChannelName)和设备名称(DeviceName)未指定！");

                for (int n = 0; n < _itemNames.Length; n++)
                {
                    OPCItem item = null;
                    try
                    {
                        item = _items.AddItem(this.ChannelName + "." + this.DeviceName + "." + _itemNames[n], _itemClientHandles[n]);
                        _itemServerHandles.Add(_itemNames[n], item.ServerHandle);
                    }
                    catch (Exception ex) { log.Error("添加订阅点错误:" + ex.Message); }

                    // 必须等待，否则后面获取初始值会报错
                    Thread.Sleep(100);

                    try
                    {
                        // 保存服务器启动时各个item的初始值
                        _itemValues[_itemNames[n]] = item.Value.ToString();
                        log.Info("已获取OPCItem初始值：" + _itemNames[n] + "=" + _itemValues[_itemNames[n]].ToString());
                    }
                    catch {
                        if (item != null)
                            log.Error("无法获取 " + _itemNames[n] + " 的初始值,当前连接品质为 " + (((OPCQualities)item.Quality).ToString()) + ", 请检查地址块的状态");
                        
                    }
                }

            return true;
        }

        /// <summary>
        /// 当KEPWARE中OPCITEM的值发生改变时激发此函数
        /// </summary>
        /// <param name="TransactionID"></param>
        /// <param name="NumItems">发生改变的数量</param>
        /// <param name="ClientHandles">改变的客户端句柄</param>
        /// <param name="ItemValues">最新的值</param>
        /// <param name="Qualities">连接质量</param>
        /// <param name="TimeStamps">时间戳</param>
        private void OPCGroup_DataChange (int TransactionID, int NumItems, ref Array ClientHandles, ref Array ItemValues, ref Array Qualities, ref Array TimeStamps)
        {
            for (int i=1; i<= NumItems; i++)
            {
                string blockName = _itemNames[Convert.ToInt32(ClientHandles.GetValue(i))-1];
                string blockValue = ItemValues.GetValue(i).ToString();
                int blockQuality = (int)Qualities.GetValue(i);
                _itemValues[blockName] = blockValue;

                if (blockQuality != (int)OPCQualities.Good)
                {
                    log.Warn("地址块 " + blockName + " 的连接品质为 " +  ((OPCQualities)blockQuality).ToString() + " 读取到的值可能不精确");
                    OnBadBlockDetected(blockName, blockQuality);
                }
            }
            // 返回所有的值列表给调用方
            OnDataChanged(this._itemValues);
        }

        /// <summary>
        /// 从服务器断开
        /// </summary>
        /// <returns></returns>
        public bool Disconnect()
        {
            try
            {
                _server.Disconnect();
                this.Connected = false;
                log.Debug("已从KEPWARE服务器断开");
                return true;
            }
            catch { return false; }
        }

    }
}
