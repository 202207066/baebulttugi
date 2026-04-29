using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace test1
{
    public partial class Menu : Form
    {
        public Menu()
        {
            InitializeComponent();
        }

        private void Menu_Button_Ins_Click(object sender, EventArgs e)
        {
            Menu_Ins mi = new Menu_Ins();
            mi.Show();
        }

        private void Menu_Button_Upd_Click(object sender, EventArgs e)
        {
            Menu_Upd mu = new Menu_Upd();
            mu.Show();
        }
    }
}
