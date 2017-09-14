using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OPCServiceClient
{
    /// <summary>
    /// OPCItem的Quality品质标识
    /// </summary>
    public enum OPCQualities :int
    {
        /// <summary>
        /// 值是好的
        /// </summary>
        Good = 0xc0,

        /// <summary>
        /// 值为坏的，没有标明原因
        /// </summary>
        Bad = 0,

        /// <summary>
        /// 值被覆盖。典型意思为输入失去连接和手动被强制
        /// </summary>
        LocalOverride = 0xd8,

        /// <summary>
        /// 没有指定原因说明值为什么不确定
        /// </summary>
        Uncertain = 0x40,

        /// <summary>
        /// 最后的可用值
        /// </summary>
        LastUsable = 0x44,

        /// <summary>
        /// 传感器达到了它的一个限值或者超过了它的量程
        /// </summary>
        SensorCal = 0x50,

        /// <summary>
        /// 返回值越限
        /// </summary>
        EGUExceeded = 0x54,

        /// <summary>
        /// 值有几个源，并且可用的源少于规定的品质好的源
        /// </summary>
        SubNormal = 0x58,

        /// <summary>
        /// 服务器特定的配置问题
        /// </summary>
        ConfigError = 0x04,

        /// <summary>
        /// 输入没有可用的连接
        /// </summary>
        NotConnected = 0x08,

        /// <summary>
        /// 设备故障
        /// </summary>
        DeviceFailure = 0x0c,

        /// <summary>
        /// 通讯失败。最后的值是可用的
        /// </summary>
        LastKnown = 0x14,

        /// <summary>
        /// 通讯失败，最后的值不可用
        /// </summary>
        CommFailure = 0x18,

        /// <summary>
        /// 块脱离扫描或者被锁
        /// </summary>
        OutOfService = 0x1C,

        /// <summary>
        /// 传感器故障
        /// </summary>
        SensorFailure = 0x10
    }
}
