using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OPCServiceClient;
using System.Xml;


namespace GUIFuncTest
{
    public partial class Form1 :Form
    {
        private OPCServiceClient.OPCServiceClient client = null;
        private List<string> items = new List<string>();
        private List<string> types = new List<string>();
        public Form1 ()
        {
            InitializeComponent();
        }

        private void Client_BadBlockDetected (string blockName, OPCQualities status)
        {
            lblStatus.Text = blockName + "连接品质不正常，代码为 " + status;
        }

        private void button1_Click (object sender, EventArgs e)
        {
            if (btnConnect.Text == "连接")
            {
                btnConnect.Enabled = false;
                if (client == null) client = new OPCServiceClient.OPCServiceClient(txtServer.Text, int.Parse(txtPort.Text));

                client.Server = txtServer.Text;
                client.Port = int.Parse(txtPort.Text);
                client.BadBlockDetected += Client_BadBlockDetected;


                if (!client.Connect())
                {
                    MessageBox.Show("连接到服务器失败!");
                    btnConnect.Enabled = true;
                    return;
                }

                btnConnect.Text = "断开";
                txtServer.Enabled = false;
                txtPort.Enabled = false;
                btnAddItem.Enabled = true;
                btnRemoveitem.Enabled = true;
                timer1.Start();

            } else if (btnConnect.Text=="断开")
            {
                client.Disconnect();
                btnConnect.Text = "连接";
                txtServer.Enabled = true;
                txtPort.Enabled = true;
                btnAddItem.Enabled = false;
                btnRemoveitem.Enabled = false;
                timer1.Stop();
            }
            btnConnect.Enabled = true;
        }

        private void btnAddItem_Click (object sender, EventArgs e)
        {
            frmAddItem form = new frmAddItem();
            timer1.Stop();
            if (form.ShowDialog() == DialogResult.OK)
            {
                items.Add(form.TagName);
                items.Add(form.TagType);
            }
            RefreshData();
            timer1.Start();
        }


        private void RefreshData() {
            listView1.Items.Clear();
            for (int n = 0; n < items.Count; n++)
            {
                string[] cols = new string[] { items[n], "", types[n] };
                ListViewItem i = new ListViewItem(cols);
                listView1.Items.Add(i);
            }

        }
        private void timer1_Tick (object sender, EventArgs e)
        {
            if (client == null) return;
            timer1.Stop();

            Dictionary<string, string> values = client.GetValues(items);

            foreach (ListViewItem i in listView1.Items)
            {
                try
                {
                    i.SubItems[1].Text = values[i.SubItems[0].Text];
                }
                catch { timer1.Start(); }
            }

            timer1.Start();



        }

        private void btnRemoveitem_Click (object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
            {
                MessageBox.Show("请首先选中要删除的项目");
                return;
            }

            foreach (ListViewItem i in listView1.SelectedItems)
            {
                string tag = i.SubItems[0].Text;
                items.Remove(tag);
                listView1.Items.Remove(i);
            }
               
        }

        protected override void OnClosed (EventArgs e)
        {
            try
            {
                timer1.Stop();
                client.Disconnect();

            }
            catch { }

            base.OnClosed(e);
        }

        private void listView1_DoubleClick (object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
            {
                MessageBox.Show("先选择要更新的项目");
                return;
            }

            string block = listView1.SelectedItems[0].SubItems[0].Text;
            string type = listView1.SelectedItems[0].SubItems[1].Text;

            frmUpdateValue form = new frmUpdateValue();
            if (form.ShowDialog() == DialogResult.OK)
            {
                string val = form.NewValue;
                client.SetValue(block, val);
            }
        }

        private void btnLoad_Click (object sender, EventArgs e)
        {
            openFileDialog1.Filter = "*.xml|*.xml";
            XmlDocument doc = new XmlDocument();
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    doc.Load(openFileDialog1.FileName);
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); return; }
            }
            else
                return;


            timer1.Stop();
            foreach (XmlNode item in doc.DocumentElement.SelectNodes("item[@enabled='1']")) {
                items.Add(item.Attributes.GetNamedItem("name").InnerText);
                types.Add(item.Attributes.GetNamedItem("type").InnerText);

            }

            if (btnConnect.Text == "连接")
            {
                txtServer.Text = doc.DocumentElement.Attributes.GetNamedItem("host").InnerText;
                txtPort.Text = doc.DocumentElement.Attributes.GetNamedItem("port").InnerText;
            }
            RefreshData();
            timer1.Start();
        }
    }
}
