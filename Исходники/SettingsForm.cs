using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RkdEstimator
{
    public sealed class SettingsForm : Form
    {
        private readonly AppSettings settings;
        private readonly NumericUpDown baseRate;
        private readonly NumericUpDown minimum;
        private readonly NumericUpDown measurementFee;
        private readonly NumericUpDown roundTo;
        private readonly DataGridView grid;

        public SettingsForm(AppSettings current)
        {
            settings = Clone(current);
            Text = "Настройки расчёта";
            Font = new Font("Segoe UI", 9.5f);
            BackColor = Color.FromArgb(246, 247, 249);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(820, 720);
            MinimumSize = new Size(700, 600);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0), Padding = new Padding(0) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64f));
            Controls.Add(root);

            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24, 14, 24, 8), Margin = new Padding(0) };
            Label title = new Label { Text = "Настройки расчёта", AutoSize = true, Font = new Font("Segoe UI Semibold", 17f), ForeColor = Color.FromArgb(29, 35, 45), Location = new Point(22, 12) };
            Label subtitle = new Label { Text = "Изменения применятся сразу после сохранения", AutoSize = true, ForeColor = Color.FromArgb(105, 113, 125), Location = new Point(24, 43) };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            root.Controls.Add(header, 0, 0);

            Panel footer = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16, 13, 18, 12), Margin = new Padding(0) };
            Button save = MakeButton("Сохранить", Color.FromArgb(40, 116, 240), Color.White, 120);
            save.Dock = DockStyle.Right;
            save.Click += delegate { ApplyAndClose(); };
            Button cancel = MakeButton("Отмена", Color.FromArgb(233, 236, 241), Color.FromArgb(53, 62, 74), 100);
            cancel.Dock = DockStyle.Right;
            cancel.Margin = new Padding(0, 0, 10, 0);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Button reset = MakeButton("Вернуть стандартные", Color.FromArgb(250, 238, 238), Color.FromArgb(176, 55, 55), 170);
            reset.Dock = DockStyle.Left;
            reset.Click += delegate { LoadIntoControls(AppSettings.CreateDefault()); };
            footer.Controls.Add(save);
            footer.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 10 });
            footer.Controls.Add(cancel);
            footer.Controls.Add(reset);
            root.Controls.Add(footer, 0, 2);

            TableLayoutPanel content = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 12), ColumnCount = 1, RowCount = 3, Margin = new Padding(0) };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 82f));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.Controls.Add(content, 0, 1);

            TableLayoutPanel values = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 2, BackColor = Color.White, Padding = new Padding(14, 10, 14, 8), Margin = new Padding(0) };
            for (int i = 0; i < 8; i++) values.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));
            values.Controls.Add(MakeLabel("Базовая ставка, ₽"), 0, 0);
            values.Controls.Add(MakeLabel("Минимум, ₽"), 2, 0);
            values.Controls.Add(MakeLabel("Округлять до"), 4, 0);
            values.Controls.Add(MakeLabel("Замер, ₽"), 6, 0);
            baseRate = MakeNumber(0, 1000000, 100, 0);
            minimum = MakeNumber(0, 1000000, 100, 0);
            roundTo = MakeNumber(1, 10000, 100, 0);
            measurementFee = MakeNumber(0, 1000000, 500, 0);
            values.Controls.Add(baseRate, 0, 1); values.SetColumnSpan(baseRate, 2);
            values.Controls.Add(minimum, 2, 1); values.SetColumnSpan(minimum, 2);
            values.Controls.Add(roundTo, 4, 1); values.SetColumnSpan(roundTo, 2);
            values.Controls.Add(measurementFee, 6, 1); values.SetColumnSpan(measurementFee, 2);
            content.Controls.Add(values, 0, 0);

            Label gridTitle = new Label { Text = "Коэффициенты расчёта", Dock = DockStyle.Fill, Padding = new Padding(0, 16, 0, 0), Font = new Font("Segoe UI Semibold", 11f), ForeColor = Color.FromArgb(40, 47, 58), Margin = new Padding(0) };
            content.Controls.Add(gridTitle, 0, 1);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight = 42,
                Margin = new Padding(0)
            };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(237, 240, 244), ForeColor = Color.FromArgb(55, 63, 74), Font = new Font("Segoe UI Semibold", 9f), Padding = new Padding(6), SelectionBackColor = Color.FromArgb(237, 240, 244) };
            grid.EnableHeadersVisualStyles = false;
            grid.RowTemplate.Height = 32;
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Группа", DataPropertyName = "Group", ReadOnly = true, Width = 210, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Усложнение", DataPropertyName = "Name", ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "%", DataPropertyName = "Percent", Width = 82, SortMode = DataGridViewColumnSortMode.NotSortable });
            content.Controls.Add(grid, 0, 2);

            LoadIntoControls(settings);
            AcceptButton = save;
            CancelButton = cancel;
        }

        public AppSettings Result { get { return settings; } }

        private void LoadIntoControls(AppSettings source)
        {
            baseRate.Value = Clamp(source.BaseRate, baseRate.Minimum, baseRate.Maximum);
            minimum.Value = Clamp(source.MinimumPrice, minimum.Minimum, minimum.Maximum);
            roundTo.Value = Clamp(source.RoundTo, roundTo.Minimum, roundTo.Maximum);
            measurementFee.Value = Clamp(source.MeasurementFee, measurementFee.Minimum, measurementFee.Maximum);
            grid.DataSource = source.Options.Select(o => new ComplexityOption(o.Id, o.Group, o.Name, o.Percent)).ToList();
        }

        private void ApplyAndClose()
        {
            grid.EndEdit();
            List<ComplexityOption> rows = grid.DataSource as List<ComplexityOption>;
            if (rows == null || rows.Any(o => o.Percent < 0m || o.Percent > 200m))
            {
                MessageBox.Show(this, "Коэффициент должен быть от 0 до 200%.", "Проверьте значения", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            settings.BaseRate = baseRate.Value;
            settings.MinimumPrice = minimum.Value;
            settings.RoundTo = roundTo.Value;
            settings.MeasurementFee = measurementFee.Value;
            settings.Options = rows;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static AppSettings Clone(AppSettings value)
        {
            return new AppSettings
            {
                BaseRate = value.BaseRate,
                MinimumPrice = value.MinimumPrice,
                MeasurementFee = value.MeasurementFee,
                RoundTo = value.RoundTo,
                Options = value.Options.Select(o => new ComplexityOption(o.Id, o.Group, o.Name, o.Percent)).ToList()
            };
        }

        private static decimal Clamp(decimal value, decimal min, decimal max) { return Math.Max(min, Math.Min(max, value)); }
        private static Label MakeLabel(string text) { return new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, ForeColor = Color.FromArgb(91, 99, 111) }; }
        private static NumericUpDown MakeNumber(decimal min, decimal max, decimal increment, int decimals) { return new NumericUpDown { Dock = DockStyle.Fill, Minimum = min, Maximum = max, Increment = increment, DecimalPlaces = decimals, ThousandsSeparator = true, Font = new Font("Segoe UI", 10.5f) }; }
        private static Button MakeButton(string text, Color back, Color fore, int width) { return new Button { Text = text, Width = width, Height = 36, FlatStyle = FlatStyle.Flat, BackColor = back, ForeColor = fore, Font = new Font("Segoe UI Semibold", 9.5f), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 } }; }
    }
}
