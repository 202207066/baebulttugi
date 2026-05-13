namespace test1
{
    partial class Main
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            menuStrip1 = new MenuStrip();
            Food_ToolStripMenuItem = new ToolStripMenuItem();
            Food_Ins_ToolStripMenuItem = new ToolStripMenuItem();
            Food_Upd_ToolStripMenuItem = new ToolStripMenuItem();
            Menu_ToolStripMenuItem = new ToolStripMenuItem();
            Menu_Ins_ToolStripMenuItem = new ToolStripMenuItem();
            Menu_Upd_ToolStripMenuItem = new ToolStripMenuItem();
            testToolStripMenuItem = new ToolStripMenuItem();
            toolStripMenuItem2 = new ToolStripMenuItem();
            toolStripMenuItem3 = new ToolStripMenuItem();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(32, 32);
            menuStrip1.Items.AddRange(new ToolStripItem[] { Food_ToolStripMenuItem, Menu_ToolStripMenuItem, testToolStripMenuItem });
            menuStrip1.Location = new Point(2, 30);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Padding = new Padding(3, 1, 0, 1);
            menuStrip1.Size = new Size(672, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // Food_ToolStripMenuItem
            // 
            Food_ToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { Food_Ins_ToolStripMenuItem, Food_Upd_ToolStripMenuItem });
            Food_ToolStripMenuItem.Name = "Food_ToolStripMenuItem";
            Food_ToolStripMenuItem.Size = new Size(55, 22);
            Food_ToolStripMenuItem.Text = "식재료";
            // 
            // Food_Ins_ToolStripMenuItem
            // 
            Food_Ins_ToolStripMenuItem.Name = "Food_Ins_ToolStripMenuItem";
            Food_Ins_ToolStripMenuItem.Size = new Size(138, 22);
            Food_Ins_ToolStripMenuItem.Text = "식재료 입력";
            Food_Ins_ToolStripMenuItem.Click += Food_Ins_ToolStripMenuItem_Click;
            // 
            // Food_Upd_ToolStripMenuItem
            // 
            Food_Upd_ToolStripMenuItem.Name = "Food_Upd_ToolStripMenuItem";
            Food_Upd_ToolStripMenuItem.Size = new Size(138, 22);
            Food_Upd_ToolStripMenuItem.Text = "식재료 수정";
            Food_Upd_ToolStripMenuItem.Click += Food_Upd_ToolStripMenuItem_Click;
            // 
            // Menu_ToolStripMenuItem
            // 
            Menu_ToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { Menu_Ins_ToolStripMenuItem, Menu_Upd_ToolStripMenuItem });
            Menu_ToolStripMenuItem.Name = "Menu_ToolStripMenuItem";
            Menu_ToolStripMenuItem.Size = new Size(43, 22);
            Menu_ToolStripMenuItem.Text = "메뉴";
            // 
            // Menu_Ins_ToolStripMenuItem
            // 
            Menu_Ins_ToolStripMenuItem.Name = "Menu_Ins_ToolStripMenuItem";
            Menu_Ins_ToolStripMenuItem.Size = new Size(126, 22);
            Menu_Ins_ToolStripMenuItem.Text = "매뉴 입력";
            Menu_Ins_ToolStripMenuItem.Click += Menu_Ins_ToolStripMenuItem_Click;
            // 
            // Menu_Upd_ToolStripMenuItem
            // 
            Menu_Upd_ToolStripMenuItem.Name = "Menu_Upd_ToolStripMenuItem";
            Menu_Upd_ToolStripMenuItem.Size = new Size(126, 22);
            Menu_Upd_ToolStripMenuItem.Text = "메뉴 수정";
            Menu_Upd_ToolStripMenuItem.Click += Menu_Upd_ToolStripMenuItem_Click;
            // 
            // testToolStripMenuItem
            // 
            testToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripMenuItem2, toolStripMenuItem3 });
            testToolStripMenuItem.Name = "testToolStripMenuItem";
            testToolStripMenuItem.Size = new Size(38, 22);
            testToolStripMenuItem.Text = "test";
            // 
            // toolStripMenuItem2
            // 
            toolStripMenuItem2.Name = "toolStripMenuItem2";
            toolStripMenuItem2.Size = new Size(102, 22);
            toolStripMenuItem2.Text = "11";
            toolStripMenuItem2.Click += toolStripMenuItem2_Click;
            // 
            // toolStripMenuItem3
            // 
            toolStripMenuItem3.Name = "toolStripMenuItem3";
            toolStripMenuItem3.Size = new Size(102, 22);
            toolStripMenuItem3.Text = "1213";
            toolStripMenuItem3.Click += toolStripMenuItem3_Click;
            // 
            // Main
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(676, 355);
            Controls.Add(menuStrip1);
            IsMdiContainer = true;
            MainMenuStrip = menuStrip1;
            Margin = new Padding(2, 1, 2, 1);
            Name = "Main";
            Padding = new Padding(2, 30, 2, 1);
            Text = "Form1";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip1;
        private ToolStripMenuItem Food_ToolStripMenuItem;
        private ToolStripMenuItem Food_Ins_ToolStripMenuItem;
        private ToolStripMenuItem Menu_ToolStripMenuItem;
        private ToolStripMenuItem Food_Upd_ToolStripMenuItem;
        private ToolStripMenuItem Menu_Ins_ToolStripMenuItem;
        private ToolStripMenuItem Menu_Upd_ToolStripMenuItem;
        private ToolStripMenuItem testToolStripMenuItem;
        private ToolStripMenuItem toolStripMenuItem2;
        private ToolStripMenuItem toolStripMenuItem3;
    }
}
