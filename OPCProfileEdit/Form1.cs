using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
namespace OPCProfileEdit
{
    public partial class Form1 :Form
    {
        private bool _dirty = false;
        private string _filename = "";
        private XmlDocument doc = new XmlDocument();


        public Form1 ()
        {
            InitializeComponent();

            openFileDialog1.Filter = "XML 文件 (*.xml)|*.xml";
            openFileDialog1.Multiselect = false;
            saveFileDialog1.Filter = "XML 文件 (*.xml)|*.xml";
        }

        private void 退出ToolStripMenuItem_Click (object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void SaveXmlProfile(string filename)
        {
            doc.LoadXml("<opcservice />");
            doc.DocumentElement.SetAttribute("timeout", txtTimeout.Text);
            doc.DocumentElement.SetAttribute("server", txtServer.Text);
            doc.DocumentElement.SetAttribute("port", txtPort.Text);
            doc.DocumentElement.SetAttribute("channel", txtChannel.Text);
            doc.DocumentElement.SetAttribute("device", txtDevice.Text);
            doc.DocumentElement.SetAttribute("host", txtHost.Text);
            doc.DocumentElement.SetAttribute("persistence", chkPersistence.Checked.ToString().ToLower());

            foreach (DataGridViewRow row in dgvTags.Rows)
            {
                try
                {
                    string tag = row.Cells[0].Value.ToString();
                    if (tag.Trim() == "") continue;

                    string type = row.Cells[1].Value.ToString();
                    string enabled = Convert.ToBoolean(((DataGridViewCheckBoxCell)row.Cells[2]).Value) ? "1" : "0";
                    string memo = row.Cells[3].Value.ToString();
                    XmlElement itemNode = doc.CreateElement("item");
                    itemNode.SetAttribute("name", tag);
                    itemNode.SetAttribute("type", type);
                    itemNode.SetAttribute("enabled", enabled);
                    itemNode.InnerText = memo;
                    doc.DocumentElement.AppendChild(itemNode);
                }
                catch { }
            }

            doc.Save(filename);
            status.Text ="已保存配置文件 " + filename;
        }

        private void LoadXmlProfile(string filename)
        {
            dgvTags.Rows.Clear();
            try
            {
                doc.Load(filename);
            } catch(Exception ex)
            {
                MessageBox.Show("打开配置文件出错：" + ex.Message);
                return;
            }

            txtChannel.Text = doc.DocumentElement.Attributes.GetNamedItem("channel").InnerText;
            txtDevice.Text =  doc.DocumentElement.Attributes.GetNamedItem("device").InnerText;
            txtHost.Text = doc.DocumentElement.Attributes.GetNamedItem("host").InnerText;
            txtServer.Text = doc.DocumentElement.Attributes.GetNamedItem("server").InnerText;
            txtPort.Text = doc.DocumentElement.Attributes.GetNamedItem("port").InnerText;
            chkPersistence.Checked = (doc.DocumentElement.Attributes.GetNamedItem("persistence").InnerText.ToLower() == "true");
            txtTimeout.Text = doc.DocumentElement.Attributes.GetNamedItem("timeout").InnerText;

            foreach (XmlNode node in doc.DocumentElement.SelectNodes("item"))
            {
                DataGridViewRow row = new DataGridViewRow();
                DataGridViewComboBoxCell typeCell = new DataGridViewComboBoxCell();
                typeCell.Items.AddRange(new string[] {
                    "BOOL",
                    "BYTE",
                    "WORD",
                    "DWORD",
                    "CHAR",
                    "LONG",
                    "STRING"
                });
                typeCell.Value = node.Attributes.GetNamedItem("type").InnerText;
                DataGridViewCell[] cells = new DataGridViewCell[]
                {
                    new DataGridViewTextBoxCell() { Value = node.Attributes.GetNamedItem("name").InnerText },
                    typeCell,
                    new DataGridViewCheckBoxCell() {  Value = node.Attributes.GetNamedItem("enabled").InnerText == "1" },
                    new DataGridViewTextBoxCell() { Value = node.InnerText }
                };

                row.Cells.AddRange(cells);
                dgvTags.Rows.Add(row);

            }
            status.Text ="已加载配置文件 " + filename;
        }

        private bool ConfirmChanges()
        {
            if (_dirty)
            {
                if (MessageBox.Show("确定要放弃之前的修改吗？", "确定", MessageBoxButtons.YesNo) ==  DialogResult.No) return false;
            }
            _dirty = false;
            return true;
        }

        private void 新建ToolStripMenuItem_Click (object sender, EventArgs e)
        {
            if (!ConfirmChanges()) return;

            txtHost.Text = "127.0.0.1";
            txtServer.Text = "KEPware.KepServerEX.V5";
            txtTimeout.Text = "3600";
            txtPort.Text = "9100";
            chkPersistence.Checked = false;
            dgvTags.Rows.Clear();
            _dirty = false;
            _filename = "";

        }

        private void 打开ToolStripMenuItem_Click (object sender, EventArgs e)
        {
            if (!ConfirmChanges()) return;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                _filename = openFileDialog1.FileName;
                LoadXmlProfile(_filename);
                _dirty = false;
            }
        }

        private void 保存ToolStripMenuItem_Click (object sender, EventArgs e)
        {

            if (_filename == "")
            {
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    _filename = saveFileDialog1.FileName;
                    SaveXmlProfile(_filename);
                    _dirty = false;
                }
            } else
            {
                SaveXmlProfile(_filename);
                _dirty = false;
            }


        }

        private void 另存为ToolStripMenuItem_Click (object sender, EventArgs e)
        {
            saveFileDialog1.Filter = "XML 文件 (*.xml)|*.xml";
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                _filename = saveFileDialog1.FileName;
                SaveXmlProfile(_filename);
                _dirty = false;
            }
        }


        private void txtHost_KeyPress (object sender, KeyPressEventArgs e)
        {
            _dirty = true;
        }

        private void chkPersistence_CheckedChanged (object sender, EventArgs e)
        {
            _dirty = true;
        }

        private void button1_Click (object sender, EventArgs e)
        {
            if (MessageBox.Show("确定要删除所有选中的地址块吗?", "确定", MessageBoxButtons.OKCancel) == DialogResult.Cancel) return;

            foreach (DataGridViewRow row in dgvTags.SelectedRows)
            {
                dgvTags.Rows.Remove(row);
            }
        }

        private void Form1_FormClosing (object sender, FormClosingEventArgs e)
        {
            if (!ConfirmChanges())
            {
                e.Cancel = true;
                return;
            }
            Application.Exit();
        }

        private void dgvTags_RowsAdded (object sender, DataGridViewRowsAddedEventArgs e)
        {
            _dirty = true;
        }

        private void dgvTags_RowsRemoved (object sender, DataGridViewRowsRemovedEventArgs e)
        {
            _dirty = true;
        }
    }
}
