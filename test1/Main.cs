namespace test1
{
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();
            this.Size = new Size(1900, 1080);
        }

        private void Food_Ins_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Food_Ins fi = new Food_Ins();
            fi.StartPosition = FormStartPosition.Manual;
            fi.Location = new Point(this.Location.X+40, this.Location.Y+100);
            fi.ShowDialog();
        }

        private void Food_Upd_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Food_Upd fu = new Food_Upd();
            fu.StartPosition = FormStartPosition.Manual;
            fu.Location = new Point(this.Location.X + 40, this.Location.Y + 100);
            fu.ShowDialog();
            
        }

        private void Menu_Ins_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Menu_Ins mi = new Menu_Ins();
            mi.StartPosition = FormStartPosition.Manual;
            mi.Location = new Point(this.Location.X + 40, this.Location.Y + 100);
            mi.ShowDialog();
        }

        private void Menu_Upd_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Menu_Upd mu =  new Menu_Upd();
            mu.StartPosition = FormStartPosition.Manual;
            mu.Location = new Point(this.Location.X + 40, this.Location.Y + 100);
            mu.ShowDialog();
        }
    }
}
