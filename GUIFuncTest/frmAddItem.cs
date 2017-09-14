using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GUIFuncTest
{
    public partial class frmAddItem :Form
    {
        public string TagName { get { return txtTagName.Text;  } }
        public string TagType { get { return cboType.Text; } }

        public frmAddItem ()
        {
            InitializeComponent();
        }
    }
}
