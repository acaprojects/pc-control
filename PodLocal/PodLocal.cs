using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//
// For tray icon
//
using System.Drawing;
using System.Windows.Forms;

namespace PodLocal
{
	public class PodLocal : Form
	{
		[STAThread]
		public static void Main()
		{
			Application.Run(new PodLocal());
		}

		private NotifyIcon trayIcon;
        private Dictionary<string, string> trayStatus = new Dictionary<string, string>();
        public delegate void UpdateTray(string statusName, string message, bool exiting);
        public delegate void UpdateCameraList(string[] list, int selected);

		private ContextMenu trayMenu;
        private Label label1;
        private ComboBox cameraSelection;
        private Label label2;
        private ListBox connectionList;
        public Label cameraStatus;
        private Label serverStatus;
		public CamControl camera;

        public static PodLocal self = null;

        private void update_tray(string system, string message, bool exiting = false) {
            lock (trayIcon)
            {
                if (exiting)
                    trayIcon.Dispose();
                else
                {
                    trayStatus.Remove(system);
                    trayStatus.Add(system, message);


                    if (system == "Camera")
                    {
                        MethodInvoker action = delegate { cameraStatus.Text = "Camera " + message; };
                        cameraStatus.BeginInvoke(action);
                    }
                    else if (system == "Server")
                    {
                        MethodInvoker action = delegate { serverStatus.Text = "Server " + message; };
                        serverStatus.BeginInvoke(action);
                    }
                    

                    string popup = "";
                    foreach (KeyValuePair<string, string> status in trayStatus) {
                        popup += status.Key + " " + status.Value + "\n";
                    }

                    trayIcon.BalloonTipText = popup;
                }
            }
        }

        private void update_camera_list(string[] list, int selected) {
            MethodInvoker action = delegate {
                cameraSelection.Items.Clear();
                cameraSelection.Items.AddRange(list);
                cameraSelection.SelectedIndex = selected;
            };
            cameraSelection.BeginInvoke(action);
        }

		public PodLocal()
		{
            InitializeComponent();
            self = this;
			// Create a simple tray menu with only one item.
			trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Config", ShowConfig);
            trayMenu.MenuItems.Add("Relaunch", OnExit);

			// Create a tray icon.
			trayIcon = new NotifyIcon();
			trayIcon.Icon = new Icon(SystemIcons.Information, 40, 40);
            trayIcon.Text = "AV Control Interface";
            trayIcon.BalloonTipTitle = "AV Control Status";
            trayIcon.MouseClick += new MouseEventHandler(mouseclick);

			// Add menu to tray icon and show it.
			trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;
		}

        protected void mouseclick(object someObject, MouseEventArgs someArgs) {
            trayIcon.ShowBalloonTip(10);
        }

		protected override void OnLoad(EventArgs e)
		{
			Visible = false;		// Hide form window.
			ShowInTaskbar = false;	// Remove from taskbar.

            camera = new CamControl(update_tray, update_camera_list);
            new Server(update_tray);

			base.OnLoad(e);
		}

		private void OnExit(object sender, EventArgs e)
		{
            Application.Exit();
		}

        private void ShowConfig(object sender, EventArgs e)
        {
            Visible = true;
            ShowInTaskbar = true;
        }

		protected override void Dispose(bool isDisposing)
		{
			if (isDisposing)
			{
				// Release the icon resource.
                update_tray("TaskBar","User Initiated",true);
				if (Server.server != null)
					Server.server.Dispose();

                //
                // Relaunch the application (it can never be killed!)
                //
                System.Diagnostics.Process.Start(System.Reflection.Assembly.GetEntryAssembly().Location);
			}

			base.Dispose(isDisposing);
		}

		private void InitializeComponent()
		{
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PodLocal));
            this.label1 = new System.Windows.Forms.Label();
            this.cameraSelection = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.connectionList = new System.Windows.Forms.ListBox();
            this.cameraStatus = new System.Windows.Forms.Label();
            this.serverStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 23);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(132, 20);
            this.label1.TabIndex = 0;
            this.label1.Text = "Selected Camera";
            // 
            // cameraSelection
            // 
            this.cameraSelection.FormattingEnabled = true;
            this.cameraSelection.Location = new System.Drawing.Point(23, 46);
            this.cameraSelection.Name = "cameraSelection";
            this.cameraSelection.Size = new System.Drawing.Size(376, 21);
            this.cameraSelection.TabIndex = 1;
            this.cameraSelection.SelectedIndexChanged += new System.EventHandler(this.cameraSelection_SelectedIndexChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(12, 117);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(167, 20);
            this.label2.TabIndex = 2;
            this.label2.Text = "Connected Controllers";
            // 
            // connectionList
            // 
            this.connectionList.FormattingEnabled = true;
            this.connectionList.Location = new System.Drawing.Point(23, 239);
            this.connectionList.Name = "connectionList";
            this.connectionList.Size = new System.Drawing.Size(376, 69);
            this.connectionList.TabIndex = 3;
            // 
            // cameraStatus
            // 
            this.cameraStatus.AutoSize = true;
            this.cameraStatus.Location = new System.Drawing.Point(13, 79);
            this.cameraStatus.Name = "cameraStatus";
            this.cameraStatus.Size = new System.Drawing.Size(40, 13);
            this.cameraStatus.TabIndex = 4;
            this.cameraStatus.Text = "Status:";
            // 
            // serverStatus
            // 
            this.serverStatus.AutoSize = true;
            this.serverStatus.Location = new System.Drawing.Point(13, 146);
            this.serverStatus.Name = "serverStatus";
            this.serverStatus.Size = new System.Drawing.Size(40, 13);
            this.serverStatus.TabIndex = 5;
            this.serverStatus.Text = "Status:";
            // 
            // PodLocal
            // 
            this.ClientSize = new System.Drawing.Size(427, 192);
            this.Controls.Add(this.serverStatus);
            this.Controls.Add(this.cameraStatus);
            this.Controls.Add(this.connectionList);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.cameraSelection);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "PodLocal";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Control Configuration";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.PodLocal_Closing);
            this.ResumeLayout(false);
            this.PerformLayout();

		}

        private void cameraSelection_SelectedIndexChanged(object sender, EventArgs e)
        {
            camera.reloadCamera(cameraSelection.SelectedIndex);
        }

        private void PodLocal_Closing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Visible = false;		// Hide form window.
            ShowInTaskbar = false;	// Remove from taskbar.
        }
	}
}



    