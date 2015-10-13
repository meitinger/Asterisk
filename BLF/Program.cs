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
using System.Windows.Forms;
using BLF.Properties;

namespace BLF
{
    enum MessageType
    {
        None = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    static class Program
    {
        static MainForm form;

        [STAThread]
        static void Main()
        {
            // create the main form and run the app
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            form = new MainForm();
            Application.Run(form);
        }

        internal static void Synced(Action action)
        {
            // invoke the action within the main thread
            if (form.InvokeRequired)
                form.Invoke(action);
            else
                action();
        }

        internal static void ShowMessage(string title, string text, MessageType type)
        {
            // store the arguments and show the baloon tip
            form.NotifyIcon.BalloonTipTitle = title;
            form.NotifyIcon.BalloonTipText = text;
            form.NotifyIcon.BalloonTipIcon = (ToolTipIcon)type;
            form.NotifyIcon.ShowBalloonTip(Settings.Default.NotifyTimeout);
        }
    }
}
