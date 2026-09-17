using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace RkdEstimator
{
    public sealed class ProjectForm : Form
    {
        private readonly AppSettings settings;
        private ProjectDocument project;
        private string currentPath;
        private bool dirty;
        private TextBox projectName;
        private TreeView tree;
        private ListView summary;
        private CheckBox measurement;
        private NumericUpDown measurementFee;
        private CheckBox cashless;
        private Label subtotalValue;
        private Label extrasValue;
        private Label totalValue;
        private Label status;
        private Button editItemButton;
        private Button deleteButton;

        private readonly Color accent = Color.FromArgb(37, 112, 238);
        private readonly Color ink = Color.FromArgb(30, 36, 46);
        private readonly Color canvas = Color.FromArgb(242, 244, 247);

        public ProjectForm(AppSettings appSettings)
        {
            settings = appSettings;
            project = ProjectDocument.CreateDefault(settings.MeasurementFee);
            Text = "Проекты РКД — версия 1.3";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { Icon = SystemIcons.Application; }
            Font = new Font("Segoe UI", 9.5f);
            BackColor = canvas;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(940, 650);
            Size = new Size(1120, 760);
            BuildUi();
            LoadProjectIntoUi();
            FormClosing += OnFormClosing;
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0), Padding = new Padding(0) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            Controls.Add(root);

            Panel top = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), BackColor = Color.White };
            root.Controls.Add(top, 0, 0);
            Panel header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.FromArgb(27, 34, 45), Padding = new Padding(20, 0, 20, 0) };
            top.Controls.Add(header);
            Label mark = new Label { Text = "⌂", Width = 50, Height = 40, Location = new Point(20, 16), BackColor = accent, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Symbol", 21f, FontStyle.Bold) };
            header.Controls.Add(mark);
            header.Controls.Add(new Label { Text = "ПРОЕКТ РКД", AutoSize = true, Location = new Point(84, 13), ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 15f) });
            header.Controls.Add(new Label { Text = "помещения · изделия · сводная стоимость", AutoSize = true, Location = new Point(86, 41), ForeColor = Color.FromArgb(166, 177, 192), Font = new Font("Segoe UI", 8.8f) });

            FlowLayoutPanel fileButtons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 480, Padding = new Padding(0, 18, 0, 0), FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            fileButtons.Controls.Add(HeaderButton("Новый", NewProject));
            fileButtons.Controls.Add(HeaderButton("Открыть", OpenProject));
            fileButtons.Controls.Add(HeaderButton("Сохранить", delegate { SaveProject(false); }));
            fileButtons.Controls.Add(HeaderButton("Сохранить как", delegate { SaveProject(true); }));
            header.Controls.Add(fileButtons);

            Panel namePanel = new Panel { Dock = DockStyle.Bottom, Height = 70, BackColor = Color.White, Padding = new Padding(20, 12, 20, 8) };
            top.Controls.Add(namePanel);
            namePanel.Controls.Add(new Label { Text = "Название проекта", AutoSize = true, Location = new Point(20, 9), ForeColor = Color.FromArgb(99, 108, 121) });
            projectName = new TextBox { Location = new Point(20, 31), Height = 30, Width = 650, Font = new Font("Segoe UI Semibold", 12f), BorderStyle = BorderStyle.FixedSingle };
            projectName.TextChanged += delegate { if (project != null) { project.Name = projectName.Text; MarkDirty(); } };
            namePanel.Controls.Add(projectName);

            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, SplitterWidth = 6, FixedPanel = FixedPanel.Panel1, SplitterDistance = 360, BackColor = canvas, Padding = new Padding(12) };
            bool splitInitialized = false;
            split.SizeChanged += delegate
            {
                if (!splitInitialized && split.ClientSize.Width > 800)
                {
                    split.SplitterDistance = 360;
                    splitInitialized = true;
                }
            };
            root.Controls.Add(split, 0, 1);
            BuildTreePanel(split.Panel1);
            BuildSummaryPanel(split.Panel2);

            status = new Label { Dock = DockStyle.Fill, BackColor = Color.White, ForeColor = Color.FromArgb(99, 108, 121), Padding = new Padding(14, 6, 0, 0), Text = "Готово", Margin = new Padding(0) };
            root.Controls.Add(status, 0, 2);
        }

        private void BuildTreePanel(Control host)
        {
            host.Padding = new Padding(0, 0, 6, 0);
            TableLayoutPanel card = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(12), RowCount = 3, ColumnCount = 1 };
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
            card.Controls.Add(new Label { Text = "ПОМЕЩЕНИЯ И ИЗДЕЛИЯ", Dock = DockStyle.Fill, ForeColor = ink, Font = new Font("Segoe UI Semibold", 11.5f), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            tree = new TreeView { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10.5f), HideSelection = false, FullRowSelect = true };
            tree.AfterSelect += delegate { UpdateSelectionButtons(); };
            tree.NodeMouseDoubleClick += delegate
            {
                if (SelectedItem() != null) EditSelectedItem();
                else RenameSelectedRoom();
            };
            card.Controls.Add(tree, 0, 1);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(0, 8, 0, 0) };
            actions.Controls.Add(ActionButton("+ Помещение", AddRoom, 146));
            actions.Controls.Add(ActionButton("+ Изделие", AddItem, 146));
            editItemButton = ActionButton("Открыть расчёт", EditSelectedItem, 146);
            actions.Controls.Add(editItemButton);
            deleteButton = ActionButton("Удалить", DeleteSelected, 146);
            deleteButton.BackColor = Color.FromArgb(247, 235, 235); deleteButton.ForeColor = Color.FromArgb(174, 54, 54);
            actions.Controls.Add(deleteButton);
            card.Controls.Add(actions, 0, 2);
            host.Controls.Add(card);
        }

        private void BuildSummaryPanel(Control host)
        {
            host.Padding = new Padding(6, 0, 0, 0);
            TableLayoutPanel card = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14), RowCount = 5, ColumnCount = 1 };
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            card.Controls.Add(new Label { Text = "СВОДНАЯ ВЕДОМОСТЬ", Dock = DockStyle.Fill, ForeColor = ink, Font = new Font("Segoe UI Semibold", 11.5f), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);

            summary = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, BorderStyle = BorderStyle.FixedSingle, HeaderStyle = ColumnHeaderStyle.Nonclickable };
            summary.Columns.Add("Помещение", 185);
            summary.Columns.Add("Изделие", 280);
            summary.Columns.Add("Стоимость РКД", 140, HorizontalAlignment.Right);
            summary.Resize += delegate
            {
                int flexible = summary.ClientSize.Width - summary.Columns[0].Width - summary.Columns[2].Width - 6;
                if (flexible > 180) summary.Columns[1].Width = flexible;
            };
            card.Controls.Add(summary, 0, 1);

            TableLayoutPanel conditions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(8, 7, 8, 4), BackColor = Color.FromArgb(249, 250, 251) };
            conditions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62)); conditions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            measurement = new CheckBox { Text = "Выезд специалиста и замер", Dock = DockStyle.Fill, Cursor = Cursors.Hand };
            measurement.CheckedChanged += delegate { measurementFee.Enabled = measurement.Checked; SyncConditions(); };
            measurementFee = MoneyNumber(); measurementFee.ValueChanged += delegate { SyncConditions(); };
            cashless = new CheckBox { Text = "Оплата по безналу (+" + GetCashlessPercent().ToString("0.#") + "%)", Dock = DockStyle.Fill, Cursor = Cursors.Hand };
            cashless.CheckedChanged += delegate { SyncConditions(); };
            conditions.Controls.Add(measurement, 0, 0); conditions.Controls.Add(measurementFee, 1, 0);
            conditions.Controls.Add(cashless, 0, 1); conditions.SetColumnSpan(cashless, 2);
            card.Controls.Add(conditions, 0, 2);

            TableLayoutPanel totals = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(12, 8, 12, 6), BackColor = Color.FromArgb(237, 244, 255) };
            totals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62)); totals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            totals.Controls.Add(TotalLabel("Изделия"), 0, 0); subtotalValue = TotalValue(false); totals.Controls.Add(subtotalValue, 1, 0);
            totals.Controls.Add(TotalLabel("Замер и условия оплаты"), 0, 1); extrasValue = TotalValue(false); totals.Controls.Add(extrasValue, 1, 1);
            totals.Controls.Add(new Label { Text = "ИТОГО", Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 11f), ForeColor = ink, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
            totalValue = TotalValue(true); totals.Controls.Add(totalValue, 1, 2);
            card.Controls.Add(totals, 0, 3);

            Button pdf = MakeButton("Сформировать общее КП PDF", accent, Color.White, 260, 42);
            pdf.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pdf.Click += delegate { ExportProjectPdf(); };
            Panel pdfHost = new Panel { Dock = DockStyle.Fill }; pdf.Location = new Point(pdfHost.Width - pdf.Width, 5);
            pdfHost.Resize += delegate { pdf.Left = pdfHost.ClientSize.Width - pdf.Width; };
            pdfHost.Controls.Add(pdf); card.Controls.Add(pdfHost, 0, 4);
            host.Controls.Add(card);
        }

        private void LoadProjectIntoUi()
        {
            projectName.Text = project.Name ?? "Новый проект";
            measurement.Checked = project.IncludeMeasurement;
            measurementFee.Value = Math.Min(measurementFee.Maximum, Math.Max(0m, project.MeasurementFee));
            measurementFee.Enabled = measurement.Checked;
            cashless.Checked = project.CashlessPayment;
            RebuildViews();
            dirty = false;
            UpdateTitle();
        }

        private void RebuildViews()
        {
            tree.BeginUpdate(); tree.Nodes.Clear();
            summary.BeginUpdate(); summary.Items.Clear();
            foreach (ProjectRoom room in project.Rooms)
            {
                TreeNode roomNode = new TreeNode(room.Name) { Tag = room, NodeFont = new Font(tree.Font, FontStyle.Bold) };
                decimal roomTotal = 0m;
                foreach (ProjectItem item in room.Items)
                {
                    roomNode.Nodes.Add(new TreeNode(item.Name + "   ·   " + FormatMoney(item.FinalWorkPrice)) { Tag = item });
                    ListViewItem row = new ListViewItem(room.Name);
                    row.SubItems.Add(item.Name);
                    row.SubItems.Add(FormatMoney(item.FinalWorkPrice));
                    summary.Items.Add(row);
                    roomTotal += item.FinalWorkPrice;
                }
                tree.Nodes.Add(roomNode); roomNode.Expand();
                if (room.Items.Count > 0)
                {
                    ListViewItem subtotal = new ListViewItem("");
                    subtotal.SubItems.Add("Итого по помещению"); subtotal.SubItems.Add(FormatMoney(roomTotal));
                    subtotal.Font = new Font(summary.Font, FontStyle.Bold); subtotal.BackColor = Color.FromArgb(247, 248, 250);
                    summary.Items.Add(subtotal);
                }
            }
            tree.EndUpdate(); summary.EndUpdate();
            UpdateTotals(); UpdateSelectionButtons();
        }

        private void AddRoom()
        {
            string name = TextPromptForm.Show(this, "Новое помещение", "Название помещения", "Помещение " + (project.Rooms.Count + 1));
            if (string.IsNullOrWhiteSpace(name)) return;
            project.Rooms.Add(new ProjectRoom { Name = name, Items = new List<ProjectItem>() });
            MarkDirty(); RebuildViews(); tree.SelectedNode = tree.Nodes[tree.Nodes.Count - 1];
        }

        private void AddItem()
        {
            ProjectRoom room = SelectedRoom();
            if (room == null)
            {
                if (project.Rooms.Count == 0) { AddRoom(); room = SelectedRoom(); }
                else room = project.Rooms[0];
            }
            if (room == null) return;
            string name = TextPromptForm.Show(this, "Новое изделие", "Название изделия", "Новое изделие");
            if (string.IsNullOrWhiteSpace(name)) return;
            ProjectItem item = ProjectItem.CreateDefault(name, settings);
            using (MainForm editor = new MainForm(item))
            {
                if (editor.ShowDialog(this) != DialogResult.OK) return;
                room.Items.Add(editor.ProjectItemResult);
            }
            MarkDirty(); RebuildViews();
        }

        private void RenameSelectedRoom()
        {
            ProjectRoom room = SelectedRoom();
            if (room == null || SelectedItem() != null) return;
            string name = TextPromptForm.Show(this, "Переименовать помещение", "Название помещения", room.Name);
            if (string.IsNullOrWhiteSpace(name) || name == room.Name) return;
            room.Name = name; MarkDirty(); RebuildViews();
        }

        private void EditSelectedItem()
        {
            ProjectItem item = SelectedItem();
            if (item == null) return;
            ProjectRoom room = SelectedRoom();
            int index = room.Items.IndexOf(item);
            using (MainForm editor = new MainForm(item))
            {
                if (editor.ShowDialog(this) != DialogResult.OK) return;
                room.Items[index] = editor.ProjectItemResult;
            }
            MarkDirty(); RebuildViews();
        }

        private void DeleteSelected()
        {
            if (tree.SelectedNode == null) return;
            ProjectItem item = tree.SelectedNode.Tag as ProjectItem;
            ProjectRoom room = tree.SelectedNode.Tag as ProjectRoom;
            string subject = item != null ? item.Name : room != null ? room.Name : "выбранную запись";
            if (MessageBox.Show(this, "Удалить «" + subject + "»?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (item != null) SelectedRoom().Items.Remove(item);
            else if (room != null) project.Rooms.Remove(room);
            MarkDirty(); RebuildViews();
        }

        private ProjectRoom SelectedRoom()
        {
            if (tree.SelectedNode == null) return null;
            ProjectRoom room = tree.SelectedNode.Tag as ProjectRoom;
            if (room != null) return room;
            return tree.SelectedNode.Parent == null ? null : tree.SelectedNode.Parent.Tag as ProjectRoom;
        }

        private ProjectItem SelectedItem()
        {
            return tree.SelectedNode == null ? null : tree.SelectedNode.Tag as ProjectItem;
        }

        private void UpdateSelectionButtons()
        {
            if (editItemButton == null) return;
            editItemButton.Enabled = SelectedItem() != null;
            deleteButton.Enabled = tree.SelectedNode != null;
        }

        private void SyncConditions()
        {
            if (project == null || measurementFee == null) return;
            project.IncludeMeasurement = measurement.Checked;
            project.MeasurementFee = measurementFee.Value;
            project.CashlessPayment = cashless.Checked;
            MarkDirty(); UpdateTotals();
        }

        private void UpdateTotals()
        {
            decimal items = project.Rooms.SelectMany(r => r.Items).Sum(i => i.FinalWorkPrice);
            decimal measure = project.IncludeMeasurement ? project.MeasurementFee : 0m;
            decimal baseTotal = items + measure;
            decimal surcharge = project.CashlessPayment ? baseTotal * GetCashlessPercent() / 100m : 0m;
            decimal total = PricingCalculator.RoundUp(baseTotal + surcharge, settings.RoundTo);
            subtotalValue.Text = FormatMoney(items);
            extrasValue.Text = FormatMoney(measure + surcharge);
            totalValue.Text = FormatMoney(total);
        }

        private decimal GetCashlessPercent()
        {
            ComplexityOption option = settings.Options.FirstOrDefault(o => o.Id == "payment_cashless");
            return option == null ? 7m : option.Percent;
        }

        private void NewProject()
        {
            if (!ConfirmDiscard()) return;
            project = ProjectDocument.CreateDefault(settings.MeasurementFee); currentPath = null; LoadProjectIntoUi();
        }

        private void OpenProject()
        {
            if (!ConfirmDiscard()) return;
            using (OpenFileDialog dialog = new OpenFileDialog { Filter = "Проект РКД|*.rkdproj", Title = "Открыть проект РКД" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { project = ProjectStore.Load(dialog.FileName); currentPath = dialog.FileName; LoadProjectIntoUi(); status.Text = "Открыт: " + Path.GetFileName(currentPath); }
                catch (Exception ex) { MessageBox.Show(this, "Не удалось открыть проект:\r\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private bool SaveProject(bool saveAs)
        {
            project.Name = string.IsNullOrWhiteSpace(projectName.Text) ? "Новый проект" : projectName.Text.Trim();
            if (saveAs || string.IsNullOrWhiteSpace(currentPath))
            {
                using (SaveFileDialog dialog = new SaveFileDialog { Filter = "Проект РКД|*.rkdproj", DefaultExt = "rkdproj", AddExtension = true, FileName = SanitizeFileName(project.Name) + ".rkdproj", Title = "Сохранить проект РКД" })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                    currentPath = dialog.FileName;
                }
            }
            try
            {
                ProjectStore.Save(currentPath, project); dirty = false; UpdateTitle(); status.Text = "Проект сохранён: " + Path.GetFileName(currentPath); return true;
            }
            catch (Exception ex) { MessageBox.Show(this, "Не удалось сохранить проект:\r\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
        }

        private void ExportProjectPdf()
        {
            if (!project.Rooms.SelectMany(r => r.Items).Any())
            {
                MessageBox.Show(this, "Добавьте хотя бы одно изделие.", "Общее КП", MessageBoxButtons.OK, MessageBoxIcon.Information); return;
            }
            project.Name = string.IsNullOrWhiteSpace(projectName.Text) ? "Новый проект" : projectName.Text.Trim();
            using (SaveFileDialog dialog = new SaveFileDialog { Filter = "PDF-файл|*.pdf", DefaultExt = "pdf", AddExtension = true, FileName = "КП на РКД " + SanitizeFileName(project.Name) + " " + DateTime.Now.ToString("dd.MM.yyyy") + ".pdf", Title = "Сохранить общее коммерческое предложение" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ProjectPdfGenerator.Generate(dialog.FileName, project, GetCashlessPercent(), settings.RoundTo);
                    status.Text = "Общее КП сохранено: " + Path.GetFileName(dialog.FileName);
                    MessageBox.Show(this, "Общее коммерческое предложение сформировано.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show(this, "Не удалось сформировать PDF:\r\n" + ex.Message, "Ошибка PDF", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private bool ConfirmDiscard()
        {
            if (!dirty) return true;
            DialogResult answer = MessageBox.Show(this, "Сохранить изменения текущего проекта?", "Проект изменён", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) return false;
            return answer != DialogResult.Yes || SaveProject(false);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!ConfirmDiscard()) e.Cancel = true;
        }

        private void MarkDirty()
        {
            if (projectName == null) return;
            dirty = true; UpdateTitle();
        }

        private void UpdateTitle()
        {
            Text = "Проекты РКД — версия 1.3" + (dirty ? " *" : "") + (string.IsNullOrWhiteSpace(currentPath) ? "" : " — " + Path.GetFileName(currentPath));
        }

        private Button HeaderButton(string text, Action action)
        {
            Button button = MakeButton(text, Color.FromArgb(48, 58, 72), Color.White, text == "Сохранить как" ? 120 : 104, 36);
            button.Margin = new Padding(0, 0, 8, 0); button.Click += delegate { action(); }; return button;
        }

        private Button ActionButton(string text, Action action, int width)
        {
            Button button = MakeButton(text, Color.FromArgb(235, 238, 242), Color.FromArgb(55, 64, 76), width, 34);
            button.Margin = new Padding(0, 0, 8, 7); button.Click += delegate { action(); }; return button;
        }

        private static Button MakeButton(string text, Color back, Color fore, int width, int height)
        {
            return new Button { Text = text, Width = width, Height = height, FlatStyle = FlatStyle.Flat, BackColor = back, ForeColor = fore, Font = new Font("Segoe UI Semibold", 9.2f), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 } };
        }

        private static NumericUpDown MoneyNumber()
        {
            return new NumericUpDown { Dock = DockStyle.Fill, Minimum = 0, Maximum = 1000000, Increment = 500, ThousandsSeparator = true, Font = new Font("Segoe UI", 10.5f), Margin = new Padding(8, 2, 0, 2) };
        }

        private static Label TotalLabel(string text) { return new Label { Text = text, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(80, 89, 102), TextAlign = ContentAlignment.MiddleLeft }; }
        private static Label TotalValue(bool main) { return new Label { Text = "0 ₽", Dock = DockStyle.Fill, ForeColor = main ? Color.FromArgb(37, 112, 238) : Color.FromArgb(30, 36, 46), TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI Semibold", main ? 14f : 10f) }; }
        private static string FormatMoney(decimal amount) { return Math.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("N0", new CultureInfo("ru-RU")) + " ₽"; }
        private static string SanitizeFileName(string value) { string result = string.IsNullOrWhiteSpace(value) ? "Новый проект" : value.Trim(); foreach (char invalid in Path.GetInvalidFileNameChars()) result = result.Replace(invalid, '_'); return result; }
    }
}
