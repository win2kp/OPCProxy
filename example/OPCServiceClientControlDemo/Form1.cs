using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace OPCServiceClientControlDemo
{
    public partial class Form1 :Form
    {
        public Form1 ()
        {
            InitializeComponent();
        }

        private void button1_Click (object sender, EventArgs e)
        {
            this.opcServiceClientControl1.Server = "127.0.0.1";
            this.opcServiceClientControl1.Port = 9100;
            if (!this.opcServiceClientControl1.Connect())
            {
                MessageBox.Show("连接服务器错误！");
            }
        }

        private void button2_Click (object sender, EventArgs e)
        {
            if (this.opcServiceClientControl1.Connected)
                this.opcServiceClientControl1.SetValue("Test1", "true");
            else
                MessageBox.Show("还没有连接到服务器");
        }

        private void button3_Click (object sender, EventArgs e)
        {
            if (this.opcServiceClientControl1.Connected)
                this.opcServiceClientControl1.GetValue("Test2");
            else
                MessageBox.Show("还没有连接到服务器");
        }

        private void button5_Click (object sender, EventArgs e)
        {
            timer1.Start();
        }

        private void button4_Click (object sender, EventArgs e)
        {
            timer2.Start();

        }

        private void timer1_Tick (object sender, EventArgs e)
        {
            if (this.opcServiceClientControl1.Connected)
                this.opcServiceClientControl1.SetValue("Test1", "true");
        }

        private void timer2_Tick (object sender, EventArgs e)
        {
            if (this.opcServiceClientControl1.Connected)
                this.opcServiceClientControl1.GetValue("Test2");
        }
    }
}
