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
    public partial class Food : Form
    {
        public Food()
        {
            InitializeComponent();
        }

        private void Food_Load(object sender, EventArgs e)
        {

        }

        private void Food_Button_Ins_Click(object sender, EventArgs e)
        {
            Food_Ins fi = new Food_Ins();
            fi.Show();   
        }

        private void Food_Button_Upd_Click(object sender, EventArgs e)
        {
            Food_Upd fu = new Food_Upd();
            fu.Show();
        }
    }
}
