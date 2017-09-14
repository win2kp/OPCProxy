using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;


namespace OPCServiceClient
{
    public enum ServiceState : int
    {
        OK = 0,
        Warning = 1,
        Error = 2,
        NoConnection = 3
    }

    public partial class OPCServiceClientControl :UserControl
    {
        private OPCServiceClient _client = null;

        public OPCServiceClientControl ()
        {
            InitializeComponent();

            this.State = ServiceState.NoConnection;
            if (!this.DesignMode)
            {
                picStatus.Image = imageList1.Images["gray.png"];
                picTransmit.Image = imageList1.Images["gray.png"];
            }
        }

        public ServiceState State { get; set; }

        public string Server { get; set; }

        public int Port { get; set; }

        public bool Connected {
            get {
                if (_client != null)
                    return _client.Connected;
                else
                    return false;
                }
        }

        public bool SetValue (string blockName, string value)
        {
            if (!this.Connected)
            {
                throw new Exception("还未连接到OPC服务");
            }

            FlashLamp(true);
            return _client.SetValue(blockName, value);
        }

        public string GetValue (string blockName)
        {
            if (!this.Connected)
            {
                throw new Exception("还未连接到OPC服务");
            }

            FlashLamp();
            return _client.GetValue(blockName);
        }

        public bool SetValues(Dictionary<string, string> blocks)
        {
            if (!this.Connected)
            {
                throw new Exception("还未连接到OPC服务");
            }

            FlashLamp(true);
            return _client.SetValues(blocks);
        }

        public Dictionary<string, string> GetValues(List<string> blocks)
        {
            if (!this.Connected)
            {
                throw new Exception("还未连接到OPC服务");
            }

            FlashLamp();
            return _client.GetValues(blocks);
        }

        public bool Connect ()
        {
            _client = new OPCServiceClient(this.Server, this.Port);
            _client.BadBlockDetected +=_client_BadBlockDetected;
            bool ret = _client.Connect();
            if (!ret)
            {
                this.State = ServiceState.Error;
                picStatus.Image = imageList1.Images["red.png"];
            } else
            {
                this.State = ServiceState.OK;
                picStatus.Image = imageList1.Images["green.png"];
            }
            return ret;
        }


        public void Disconnect()
        {
            _client.Disconnect();
            picStatus.Image = imageList1.Images["gray.png"];
        }

        private void _client_BadBlockDetected (string blockName, OPCQualities status)
        {
            if (status != OPCQualities.Good)
            {
                this.State = ServiceState.Warning;
                picStatus.Image = imageList1.Images["yellow.png"];
            } else
            {

                this.State = ServiceState.OK;
                picStatus.Image = imageList1.Images["green.png"];
            }
        }

        private void FlashLamp(bool write = false)
        {
            string lamp = "green.png";
            if (write) lamp = "red.png";
            picTransmit.Image = imageList1.Images[lamp];
            timer1.Start();
        }

        private void timer1_Tick (object sender, EventArgs e)
        {
            timer1.Stop();
            picTransmit.Image = imageList1.Images["gray.png"];
        }
    }
}
