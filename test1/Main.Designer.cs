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
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(32, 32);
            menuStrip1.Items.AddRange(new ToolStripItem[] { Food_ToolStripMenuItem, Menu_ToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1351, 40);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // Food_ToolStripMenuItem
            // 
            Food_ToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { Food_Ins_ToolStripMenuItem, Food_Upd_ToolStripMenuItem });
            Food_ToolStripMenuItem.Name = "Food_ToolStripMenuItem";
            Food_ToolStripMenuItem.Size = new Size(106, 38);
            Food_ToolStripMenuItem.Text = "식재료";
            // 
            // Food_Ins_ToolStripMenuItem
            // 
            Food_Ins_ToolStripMenuItem.Name = "Food_Ins_ToolStripMenuItem";
            Food_Ins_ToolStripMenuItem.Size = new Size(275, 44);
            Food_Ins_ToolStripMenuItem.Text = "식재료 입력";
            Food_Ins_ToolStripMenuItem.Click += Food_Ins_ToolStripMenuItem_Click;
            // 
            // Food_Upd_ToolStripMenuItem
            // 
            Food_Upd_ToolStripMenuItem.Name = "Food_Upd_ToolStripMenuItem";
            Food_Upd_ToolStripMenuItem.Size = new Size(275, 44);
            Food_Upd_ToolStripMenuItem.Text = "식재료 수정";
            Food_Upd_ToolStripMenuItem.Click += Food_Upd_ToolStripMenuItem_Click;
            // 
            // Menu_ToolStripMenuItem
            // 
            Menu_ToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { Menu_Ins_ToolStripMenuItem, Menu_Upd_ToolStripMenuItem });
            Menu_ToolStripMenuItem.Name = "Menu_ToolStripMenuItem";
            Menu_ToolStripMenuItem.Size = new Size(82, 38);
            Menu_ToolStripMenuItem.Text = "메뉴";
            // 
            // Menu_Ins_ToolStripMenuItem
            // 
            Menu_Ins_ToolStripMenuItem.Name = "Menu_Ins_ToolStripMenuItem";
            Menu_Ins_ToolStripMenuItem.Size = new Size(359, 44);
            Menu_Ins_ToolStripMenuItem.Text = "매뉴 입력";
            Menu_Ins_ToolStripMenuItem.Click += Menu_Ins_ToolStripMenuItem_Click;
            // 
            // Menu_Upd_ToolStripMenuItem
            // 
            Menu_Upd_ToolStripMenuItem.Name = "Menu_Upd_ToolStripMenuItem";
            Menu_Upd_ToolStripMenuItem.Size = new Size(359, 44);
            Menu_Upd_ToolStripMenuItem.Text = "메뉴 수정";
            Menu_Upd_ToolStripMenuItem.Click += Menu_Upd_ToolStripMenuItem_Click;
            // 
            // Main
            // 
            AutoScaleDimensions = new SizeF(14F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1351, 757);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "Main";
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
    }
}
