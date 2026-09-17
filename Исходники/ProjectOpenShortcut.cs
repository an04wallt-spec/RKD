using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace RkdEstimator
{
    internal static class ProjectOpenShortcut
    {
        public static void Attach(MainForm form)
        {
            if (form == null) return;

            Button projectButton = FindButton(form, "Проект");
            if (projectButton == null || projectButton.Parent == null) return;

            Control host = projectButton.Parent;
            Button settingsButton = FindButton(host, "⚙  Настройки");
            if (settingsButton == null) return;

            host.Width = 418;

            projectButton.Location = new Point(10, 16);
            projectButton.Width = 126;
            projectButton.Visible = true;

            Button openButton = new Button
            {
                Text = "Открыть проект",
                Width = 126,
                Height = 36,
                Location = new Point(146, 16),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(48, 58, 72),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 9.2f),
                Cursor = Cursors.Hand,
                Enabled = projectButton.Enabled
            };
            openButton.FlatAppearance.BorderSize = 0;
            openButton.Click += delegate { OpenSavedProject(form); };

            settingsButton.Location = new Point(282, 16);
            host.Controls.Add(openButton);

            // Docked header controls can overlap when the host grows. Keep all three
            // actions explicitly on top and in their intended left-to-right order.
            settingsButton.BringToFront();
            openButton.BringToFront();
            projectButton.BringToFront();
        }

        private static void OpenSavedProject(MainForm owner)
        {
            FieldInfo settingsField = typeof(MainForm).GetField("settings", BindingFlags.Instance | BindingFlags.NonPublic);
            if (settingsField == null) return;
            AppSettings settings = settingsField.GetValue(owner) as AppSettings;
            if (settings == null) return;

            using (ProjectForm dialog = new ProjectForm(settings))
            {
                MethodInfo openMethod = typeof(ProjectForm).GetMethod("OpenProject", BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo pathField = typeof(ProjectForm).GetField("currentPath", BindingFlags.Instance | BindingFlags.NonPublic);
                if (openMethod == null || pathField == null) return;

                openMethod.Invoke(dialog, null);
                string path = pathField.GetValue(dialog) as string;
                if (string.IsNullOrWhiteSpace(path)) return;
                dialog.ShowDialog(owner);
            }
        }

        private static Button FindButton(Control root, string text)
        {
            foreach (Control control in root.Controls)
            {
                Button button = control as Button;
                if (button != null && string.Equals(button.Text, text, StringComparison.Ordinal)) return button;
                Button nested = FindButton(control, text);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
