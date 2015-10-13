namespace BLF
{
    partial class MainForm
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.NotifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.notifyContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timeLabel = new System.Windows.Forms.Label();
            this.workTimer = new System.Windows.Forms.Timer(this.components);
            this.stateDataGridView = new System.Windows.Forms.DataGridView();
            this.timeTextBox = new System.Windows.Forms.TextBox();
            this.modifyLabel = new System.Windows.Forms.Label();
            this.modifyMaskedTextBox = new System.Windows.Forms.MaskedTextBox();
            this.modifyButton = new System.Windows.Forms.Button();
            this.stateLabel = new System.Windows.Forms.Label();
            this.notifyToolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.restartStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.notifyContextMenuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.stateDataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // NotifyIcon
            // 
            this.NotifyIcon.ContextMenuStrip = this.notifyContextMenuStrip;
            this.NotifyIcon.Text = "BLF";
            this.NotifyIcon.Visible = true;
            this.NotifyIcon.Click += new System.EventHandler(this.NotifyIcon_Click);
            this.NotifyIcon.DoubleClick += new System.EventHandler(this.NotifyIcon_DoubleClick);
            // 
            // notifyContextMenuStrip
            // 
            this.notifyContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.restartStripMenuItem,
            this.notifyToolStripSeparator,
            this.exitToolStripMenuItem});
            this.notifyContextMenuStrip.Name = "notifyContextMenuStrip";
            this.notifyContextMenuStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.notifyContextMenuStrip.Size = new System.Drawing.Size(153, 76);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // timeLabel
            // 
            this.timeLabel.AutoSize = true;
            this.timeLabel.Location = new System.Drawing.Point(12, 9);
            this.timeLabel.Name = "timeLabel";
            this.timeLabel.Size = new System.Drawing.Size(86, 13);
            this.timeLabel.TabIndex = 0;
            this.timeLabel.Text = "Time Remaining:";
            // 
            // workTimer
            // 
            this.workTimer.Enabled = true;
            this.workTimer.Interval = 1000;
            this.workTimer.Tick += new System.EventHandler(this.workTimer_Tick);
            // 
            // stateDataGridView
            // 
            this.stateDataGridView.AllowUserToAddRows = false;
            this.stateDataGridView.AllowUserToDeleteRows = false;
            this.stateDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.stateDataGridView.Location = new System.Drawing.Point(12, 72);
            this.stateDataGridView.Name = "stateDataGridView";
            this.stateDataGridView.ReadOnly = true;
            this.stateDataGridView.Size = new System.Drawing.Size(287, 132);
            this.stateDataGridView.TabIndex = 6;
            // 
            // timeTextBox
            // 
            this.timeTextBox.Location = new System.Drawing.Point(12, 25);
            this.timeTextBox.Name = "timeTextBox";
            this.timeTextBox.ReadOnly = true;
            this.timeTextBox.Size = new System.Drawing.Size(100, 20);
            this.timeTextBox.TabIndex = 1;
            // 
            // modifyLabel
            // 
            this.modifyLabel.AutoSize = true;
            this.modifyLabel.Location = new System.Drawing.Point(118, 9);
            this.modifyLabel.Name = "modifyLabel";
            this.modifyLabel.Size = new System.Drawing.Size(41, 13);
            this.modifyLabel.TabIndex = 2;
            this.modifyLabel.Text = "Modify:";
            // 
            // modifyMaskedTextBox
            // 
            this.modifyMaskedTextBox.Location = new System.Drawing.Point(118, 25);
            this.modifyMaskedTextBox.Mask = "#00:00:00";
            this.modifyMaskedTextBox.Name = "modifyMaskedTextBox";
            this.modifyMaskedTextBox.Size = new System.Drawing.Size(100, 20);
            this.modifyMaskedTextBox.TabIndex = 3;
            // 
            // modifyButton
            // 
            this.modifyButton.Location = new System.Drawing.Point(224, 23);
            this.modifyButton.Name = "modifyButton";
            this.modifyButton.Size = new System.Drawing.Size(75, 23);
            this.modifyButton.TabIndex = 4;
            this.modifyButton.Text = "Modify";
            this.modifyButton.UseVisualStyleBackColor = true;
            this.modifyButton.Click += new System.EventHandler(this.modifyButton_Click);
            // 
            // stateLabel
            // 
            this.stateLabel.AutoSize = true;
            this.stateLabel.Location = new System.Drawing.Point(12, 56);
            this.stateLabel.Name = "stateLabel";
            this.stateLabel.Size = new System.Drawing.Size(77, 13);
            this.stateLabel.TabIndex = 5;
            this.stateLabel.Text = "Device States:";
            // 
            // notifyToolStripSeparator
            // 
            this.notifyToolStripSeparator.Name = "notifyToolStripSeparator";
            this.notifyToolStripSeparator.Size = new System.Drawing.Size(149, 6);
            // 
            // restartStripMenuItem
            // 
            this.restartStripMenuItem.Name = "restartStripMenuItem";
            this.restartStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.restartStripMenuItem.Text = "Restart";
            this.restartStripMenuItem.DropDownItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.restartStripMenuItem_DropDownItemClicked);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(311, 216);
            this.Controls.Add(this.stateLabel);
            this.Controls.Add(this.modifyButton);
            this.Controls.Add(this.modifyMaskedTextBox);
            this.Controls.Add(this.modifyLabel);
            this.Controls.Add(this.timeTextBox);
            this.Controls.Add(this.stateDataGridView);
            this.Controls.Add(this.timeLabel);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "MainForm";
            this.ShowInTaskbar = false;
            this.Text = "BLF";
            this.TopMost = true;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.Shown += new System.EventHandler(this.MainForm_Shown);
            this.notifyContextMenuStrip.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.stateDataGridView)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label timeLabel;
        private System.Windows.Forms.Timer workTimer;
        private System.Windows.Forms.DataGridView stateDataGridView;
        private System.Windows.Forms.TextBox timeTextBox;
        private System.Windows.Forms.Label modifyLabel;
        private System.Windows.Forms.MaskedTextBox modifyMaskedTextBox;
        private System.Windows.Forms.Button modifyButton;
        private System.Windows.Forms.Label stateLabel;
        private System.Windows.Forms.ContextMenuStrip notifyContextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        internal System.Windows.Forms.NotifyIcon NotifyIcon;
        private System.Windows.Forms.ToolStripMenuItem restartStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator notifyToolStripSeparator;
    }
}

