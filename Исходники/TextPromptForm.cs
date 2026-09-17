using System.Drawing;
using System.Windows.Forms;

namespace RkdEstimator
{
    internal sealed class TextPromptForm : Form
    {
        private readonly TextBox input;

        private TextPromptForm(string title, string label, string value)
        {
            Text = title;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(440, 142);
            BackColor = Color.White;

            Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(18, 16), ForeColor = Color.FromArgb(60, 68, 80) });
            input = new TextBox { Text = value ?? "", Location = new Point(20, 42), Width = 400, Font = new Font("Segoe UI", 11f) };
            Controls.Add(input);

            Button ok = MakeButton("Сохранить", Color.FromArgb(37, 112, 238), Color.White, 116);
            ok.Location = new Point(180, 91); ok.DialogResult = DialogResult.OK;
            Button cancel = MakeButton("Отмена", Color.FromArgb(235, 238, 242), Color.FromArgb(55, 64, 76), 116);
            cancel.Location = new Point(304, 91); cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
        }

        public static string Show(IWin32Window owner, string title, string label, string value)
        {
            using (TextPromptForm form = new TextPromptForm(title, label, value))
            {
                form.Shown += delegate { form.input.SelectAll(); form.input.Focus(); };
                return form.ShowDialog(owner) == DialogResult.OK ? form.input.Text.Trim() : null;
            }
        }

        private static Button MakeButton(string text, Color back, Color fore, int width)
        {
            return new Button { Text = text, Width = width, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = back, ForeColor = fore, Font = new Font("Segoe UI Semibold", 9.2f), FlatAppearance = { BorderSize = 0 } };
        }
    }
}
