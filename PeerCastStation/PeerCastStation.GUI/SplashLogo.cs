using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PeerCastStation.GUI
{
    public partial class SplashLogo : Form
    {
        public SplashLogo()
        {
            InitializeComponent();
            int y = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height / 2 - this.Height / 2;
            int x = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / 2 - this.Width / 2;
            this.StartPosition = FormStartPosition.Manual;
            this.DesktopLocation = new Point(x, y);
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void SplashLogo_Load(object sender, EventArgs e)
        {
            timer1.Enabled = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
