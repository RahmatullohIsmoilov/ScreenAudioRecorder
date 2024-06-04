namespace ScreenAudioRecorder
{
    partial class Form1
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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            btn_record = new Button();
            btn_stop = new Button();
            timer1 = new System.Windows.Forms.Timer(components);
            lbl_Timer = new Label();
            SuspendLayout();
            // 
            // btn_record
            // 
            btn_record.Location = new Point(77, 192);
            btn_record.Name = "btn_record";
            btn_record.Size = new Size(113, 48);
            btn_record.TabIndex = 0;
            btn_record.Text = "Start";
            btn_record.UseVisualStyleBackColor = true;
            btn_record.Click += BtnRecord_Clicked;
            // 
            // btn_stop
            // 
            btn_stop.Enabled = false;
            btn_stop.Location = new Point(285, 192);
            btn_stop.Name = "btn_stop";
            btn_stop.Size = new Size(113, 48);
            btn_stop.TabIndex = 1;
            btn_stop.Text = "Stop";
            btn_stop.UseVisualStyleBackColor = true;
            btn_stop.Click += BtnStop_Clicked;
            // 
            // timer1
            // 
            timer1.Tick += TimerTick;
            // 
            // lbl_Timer
            // 
            lbl_Timer.AutoSize = true;
            lbl_Timer.Font = new Font("Lucida Console", 36F, FontStyle.Bold, GraphicsUnit.Point, 0);
            lbl_Timer.Location = new Point(77, 57);
            lbl_Timer.Name = "lbl_Timer";
            lbl_Timer.Size = new Size(321, 60);
            lbl_Timer.TabIndex = 2;
            lbl_Timer.Text = "00:00:00";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(511, 293);
            Controls.Add(lbl_Timer);
            Controls.Add(btn_stop);
            Controls.Add(btn_record);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Screen Audio Recorder";
            Load += OnFormLoad;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btn_record;
        private Button btn_stop;
        private System.Windows.Forms.Timer timer1;
        private Label lbl_Timer;
    }
}
