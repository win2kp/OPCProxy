using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections;
using System.Xml;

namespace OPCService
{
    /// <summary>
    /// 全局结果集，用Hashtable的Synchronized方法实现线程安全
    /// </summary>
    public class ValuesContainer
    {
        /// <summary>
        /// 存储所有OPCITEM的值
        /// </summary>
        private static Hashtable _itemValues = Hashtable.Synchronized(new Hashtable());

        /// <summary>
        /// 存储所有OPCITEM的数据类型
        /// </summary>
        private static Hashtable _itemTypes = Hashtable.Synchronized(new Hashtable());

        /// <summary>
        /// 存储所有ITEM的连接品质
        /// </summary>
        private static Hashtable _itemQualities = Hashtable.Synchronized(new Hashtable());

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
        /// item备注信息
        /// </summary>
        private static Hashtable _itemDesc = Hashtable.Synchronized(new Hashtable());


        /// <summary>
        /// 为调用方提供数据接口
        /// </summary>
        public static Hashtable Values {
            get { return _itemValues; }
            set { _itemValues = value; }
        }

        /// <summary>
        /// 为调用方提供类型接口
        /// </summary>
        public static Hashtable Types
        {
            get { return _itemTypes; }
            set { _itemTypes = value; }
        }

        /// <summary>
        /// 为调用方提供链接品质接口
        /// </summary>
        public static Hashtable Qualities
        {
            get { return _itemQualities; }
            set { _itemQualities = value; }
        }

        /// <summary>
        /// 为调用方提供最后更新时间戳接口
        /// </summary>
        public static Hashtable LastUpdates
        {
            get { return _itemLastUpdate; }
            set { _itemLastUpdate = value; }
        }

        /// <summary>
        /// 为调用方提供最后同步时间戳接口
        /// </summary>
        public static Hashtable LastSyncs
        {
            get { return _itemLastSync; }
            set { _itemLastSync = value; }
        }

        /// <summary>
        /// 为调用方提供读取次数统计接口
        /// </summary>
        public static Hashtable ReadCounts
        {
            get { return _itemReadCount; }
            set { _itemReadCount = value; }
        }

        /// <summary>
        /// 为调用方提供更新次数统计接口
        /// </summary>
        public static Hashtable WriteCounts
        {
            get { return _itemUpdateCount; }
            set { _itemUpdateCount = value; }
        }

        /// <summary>
        /// 为调用放提供item描述信息的接口
        /// </summary>
        public static Hashtable ItemDescriptions
        {
            get { return _itemDesc; }
            set { _itemDesc = value; }
        }

        /// <summary>
        /// 日志输出
        /// </summary>
        private static log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// 将当前OPC的值持久化到文件中
        /// </summary>
        /// <param name="filename"></param>
        public static void Persist(string filename)
        {
            string persistFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);

            XmlDocument doc = new XmlDocument();
            doc.LoadXml("<opc />");
            foreach (string key in _itemValues.Keys)
            {
                XmlNode node = doc.CreateElement("item");

                XmlAttribute attribName = doc.CreateAttribute("name");
                attribName.InnerText = key;
                node.Attributes.SetNamedItem(attribName);

                XmlAttribute attribType = doc.CreateAttribute("type");
                attribType.InnerText = _itemTypes[key].ToString();
                node.Attributes.SetNamedItem(attribType);

                node.InnerText = _itemValues[key].ToString();
                doc.DocumentElement.AppendChild(node);
            }

            doc.Save(persistFile);
        }

        /// <summary>
        /// 从持久化文件中加载OPC值
        /// </summary>
        /// <param name="filename">要读取的持久化文件</param>
        /// <returns>是否读取成功</returns>
        public static bool Load (string filename, bool update = false)
        {
            string persistFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(persistFile);
            } catch (Exception ex)
            {
                log.Error("加载持久化文件出错：" + ex.Message);
                return false;
            }

            foreach (XmlNode node in doc.DocumentElement.SelectNodes("item"))
            {
                string blockName = node.Attributes.GetNamedItem("name").InnerText;
                string value = node.InnerText;
                string type = node.Attributes.GetNamedItem("type").InnerText;
                if (_itemValues.ContainsKey(blockName))
                {
                    _itemValues[blockName] = value;
                    _itemTypes[blockName] = type;
                } else
                {
                    _itemValues.Add(blockName, value);
                    _itemTypes.Add(blockName, type);
                }

                Program.Listener_OPCItemUpdated(blockName, value, type);
            }

            return true;
        }

    }

}
