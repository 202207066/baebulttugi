using System;
using System.Drawing;
using System.Windows.Forms;
using MaterialSkin;
using MaterialSkin.Controls;

namespace test1
{
    public partial class Main : MaterialForm
    {
        public Main()
        {
            InitializeComponent();

            var materialSkinManager = MaterialSkinManager.Instance;
            materialSkinManager.AddFormToManage(this);

            materialSkinManager.Theme = MaterialSkinManager.Themes.LIGHT;

            // 1. 색상 설정 (Primary.BlueGrey800이 메인 색상입니다)
            materialSkinManager.ColorScheme = new ColorScheme(
                Primary.BlueGrey800, Primary.BlueGrey900,
                Primary.BlueGrey500, Accent.LightBlue200,
                TextShade.WHITE
            );

            // 2. [확장 로직] 메뉴바 배경색을 상단 바와 통일하여 영역 넓히기
            // 이 설정을 통해 상단 진회색 영역이 메뉴바까지 확장되어 보입니다.
            if (this.MainMenuStrip != null)
            {
                this.MainMenuStrip.BackColor = ColorTranslator.FromHtml("#263238"); // BlueGrey800 색상값
                this.MainMenuStrip.ForeColor = Color.White; // 글자색 흰색
                this.MainMenuStrip.Padding = new Padding(10, 10, 0, 10); // 메뉴바 상하 여백을 늘려 더 넓게 보이게 함
            }

            this.WindowState = FormWindowState.Maximized;
        }

        // --- 이하 메뉴 클릭 이벤트 동일 ---
        private void Food_Ins_ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Food_Ins fi = new Food_Ins();
            fi.StartPosition = FormStartPosition.Manual;
            fi.Location = new Point(this.Location.X + 40, this.Location.Y + 100);
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
            Menu_Upd mu = new Menu_Upd();
            mu.StartPosition = FormStartPosition.Manual;
            mu.Location = new Point(this.Location.X + 40, this.Location.Y + 100);
            mu.ShowDialog();
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            test11 t1 = new test11();
            t1.StartPosition = FormStartPosition.Manual;
            t1.Location = new Point(this.Location.X + 40, this.Location.Y + 100);
            t1.ShowDialog();
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            test12 t2 = new test12();
            t2.StartPosition = FormStartPosition.Manual;
            t2.Location = new Point(this.Location.X + 40, this.Location.Y + 100);
            t2.ShowDialog();
        }
    }
}