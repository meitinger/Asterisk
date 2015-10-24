/* Copyright (C) 2015, Manuel Meitinger
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 2 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BLF.Properties;

namespace BLF
{
    public partial class MainForm : Form
    {
        #region WTS

        private readonly IntPtr WTS_CURRENT_SERVER_HANDLE = new IntPtr(0);
        private const uint WTS_CURRENT_SESSION = 0xFFFFFFFF;

        private enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }

        private enum WTS_INFO_CLASS
        {
            WTSInitialProgram = 0,
            WTSApplicationName = 1,
            WTSWorkingDirectory = 2,
            WTSOEMId = 3,
            WTSSessionId = 4,
            WTSUserName = 5,
            WTSWinStationName = 6,
            WTSDomainName = 7,
            WTSConnectState = 8,
            WTSClientBuildNumber = 9,
            WTSClientName = 10,
            WTSClientDirectory = 11,
            WTSClientProductId = 12,
            WTSClientHardwareId = 13,
            WTSClientAddress = 14,
            WTSClientDisplay = 15,
            WTSClientProtocolType = 16,
            WTSIdleTime = 17,
            WTSLogonTime = 18,
            WTSIncomingBytes = 19,
            WTSOutgoingBytes = 20,
            WTSIncomingFrames = 21,
            WTSOutgoingFrames = 22,
            WTSClientInfo = 23,
            WTSSessionInfo = 24,
            WTSSessionInfoEx = 25,
            WTSConfigInfo = 26,
            WTSValidationInfo = 27,
            WTSSessionAddressV4 = 28,
            WTSIsRemoteSession = 29
        }

        [DllImport("Wtsapi32.dll", ExactSpelling = true)]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("Wtsapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool WTSQuerySessionInformation(IntPtr hServer, uint SessionId, WTS_INFO_CLASS WTSInfoClass, out IntPtr ppBuffer, out int pBytesReturned);

        #endregion

        [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
        private static extern int ExtractIconEx(string lpszFile, int nIconIndex, IntPtr phiconLarge, out IntPtr phiconSmall, int nIcons);

        private readonly ExtensionSyncClient[] clients;
        private readonly Extension workExtension;
        private DateTime lastTick = DateTime.Now;

        public MainForm()
        {
            // intialize the components and set stuff that can be set in the designer
            InitializeComponent();
            var icon = IntPtr.Zero;
            NotifyIcon.Icon = ExtractIconEx(Application.ExecutablePath, 0, IntPtr.Zero, out icon, 1) == 1 ? Icon.FromHandle(icon) : Icon;
            modifyMaskedTextBox.ValidatingType = typeof(TimeSpan);

            // create the clients and their restart buttons
            clients = Settings.Default.Servers.Cast<string>().Select(s => new ExtensionSyncClient(new Uri(s))).ToArray();
            foreach (var client in clients)
                restartToolStripMenuItem.DropDownItems.Add(client.BaseUri.ToString()).Tag = client;

            // set the grid datasource and create the work extension
            stateDataGridView.DataSource = Extension.All;
            workExtension = Extension.FromNumber(Settings.Default.Extension, this);
        }

        private void ModifyWork(TimeSpan timespan)
        {
            // update the work registry value
            var work = TimeSpan.Parse((string)Microsoft.Win32.Registry.GetValue(Settings.Default.RegistryKey, Settings.Default.RegistryValue, null));
            work += timespan;
            Microsoft.Win32.Registry.SetValue(Settings.Default.RegistryKey, Settings.Default.RegistryValue, work.ToString());

            // show the remaining time and set the extension
            var remaining = TimeSpan.FromHours((((int)(DateTime.Now - Settings.Default.StartDate).TotalDays / 7) + 1) * Settings.Default.HoursPerWeek) - work;
            workExtension.State = compTimeToolStripMenuItem.Checked ? ExtensionStates.Ringing : remaining > TimeSpan.Zero ? ExtensionStates.NotInUse : ExtensionStates.Busy;
            NotifyIcon.Text = timeTextBox.Text = remaining.ToString();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void workTimer_Tick(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            var duration = now - lastTick;
            lastTick = now;
            var buffer = IntPtr.Zero;
            var size = 0;
            if (!WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, WTS_CURRENT_SESSION, WTS_INFO_CLASS.WTSConnectState, out buffer, out size))
                throw new Win32Exception();
            if ((WTS_CONNECTSTATE_CLASS)Marshal.ReadInt32(buffer) == WTS_CONNECTSTATE_CLASS.WTSActive)
                ModifyWork(duration);
        }

        private void modifyButton_Click(object sender, EventArgs e)
        {
            var value = modifyMaskedTextBox.ValidateText();
            if (value == null)
            {
                modifyMaskedTextBox.SelectAll();
                modifyMaskedTextBox.Focus();
            }
            else
                ModifyWork((TimeSpan)value);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ModifyWork(TimeSpan.Zero);
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            Hide();
            foreach (var client in clients)
                client.Start();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            Show();
            Focus();
        }

        private void NotifyIcon_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(NotifyIcon.BalloonTipText))
                NotifyIcon.ShowBalloonTip(Settings.Default.NotifyTimeout);
        }

        private void restartToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            // restart the client
            var client = (ExtensionSyncClient)e.ClickedItem.Tag;
            client.Stop();
            client.Start();
        }

        private void compTimeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // toggle comp time
            compTimeToolStripMenuItem.Checked = !compTimeToolStripMenuItem.Checked;
        }
    }
}
