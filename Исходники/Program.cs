using System;
using System.Threading;
using System.Windows.Forms;

namespace RkdEstimator
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "RkdProject.Personal.V10", out created))
            {
                if (!created)
                {
                    MessageBox.Show("Программа уже запущена.", "RKD.Project", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
