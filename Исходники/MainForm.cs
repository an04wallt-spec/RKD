using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace RkdEstimator
{
    public sealed class MainForm : Form
    {
        private readonly bool projectEditMode;
        private readonly ProjectItem sourceProjectItem;
        private AppSettings settings;
        private readonly List<ImageItem> images = new List<ImageItem>();
        private TableLayoutPanel imageGrid;
        private Label emptyHint;
        private Label imageCounter;
        private Button moveUpButton;
        private Button moveDownButton;
        private Button removeButton;
        private int selectedImageIndex = -1;
        private TextBox calculationName;
        private string nameBeforeEdit;

        private NumericUpDown widthInput;
        private NumericUpDown heightInput;
        private NumericUpDown depthInput;
        private NumericUpDown baseRateInput;
        private NumericUpDown minimumInput;
        private NumericUpDown measurementFeeInput;
        private NumericUpDown myPriceInput;
        private Panel complexityPanel;
        private readonly List<CheckBox> complexityChecks = new List<CheckBox>();

        private Label calculatedValue;
        private Label recommendedValue;
        private Label minimumBadge;
        private Label myPriceComparison;
        private RichTextBox explanation;
        private Label statusLabel;
        private NotifyIcon trayIcon;

        public ProjectItem ProjectItemResult { get; private set; }

        private readonly Color accent = Color.FromArgb(37, 112, 238);
        private readonly Color ink = Color.FromArgb(30, 36, 46);
        private readonly Color muted = Color.FromArgb(99, 108, 121);
        private readonly Color surface = Color.White;
        private readonly Color canvas = Color.FromArgb(242, 244, 247);

        public MainForm() : this(null)
        {
        }

        internal MainForm(ProjectItem projectItem)
        {
            projectEditMode = projectItem != null;
            sourceProjectItem = projectItem;
            settings = SettingsStore.Load();
            Text = projectEditMode ? "Расчёт изделия проекта — RKD.Project 1.3" : "Оценка стоимости РКД — RKD.Project 1.3";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { Icon = SystemIcons.Application; }
            Font = new Font("Segoe UI", 9.5f);
            BackColor = canvas;
            MinimumSize = new Size(1120, 720);
            Size = new Size(1420, 900);
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;
            AllowDrop = true;

            BuildUi();
            if (!projectEditMode)
            {
                trayIcon = new NotifyIcon { Icon = Icon, Text = "Оценка стоимости РКД", Visible = true };
                trayIcon.DoubleClick += delegate
                {
                    if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                    Show();
                    Activate();
                };
            }
            ApplySettingsToInputs();
            RebuildComplexityGroups(null);
            if (projectEditMode) LoadProjectItem(sourceProjectItem);
            Recalculate();

            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            KeyDown += OnFormKeyDown;
            FormClosed += delegate
            {
                DisposeImages();
                if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
            };
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0), Padding = new Padding(0) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            Controls.Add(root);

            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(27, 34, 45), Padding = new Padding(20, 0, 18, 0), Margin = new Padding(0) };
            Label logo = new Label { Text = "РКД", AutoSize = false, Width = 66, Height = 36, Location = new Point(20, 16), BackColor = accent, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 13f) };
            Label title = new Label { Text = "Предварительная оценка стоимости", AutoSize = true, Location = new Point(100, 13), ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 15f) };
            Label subtitle = new Label { Text = "рабочая конструкторская документация · личный калькулятор", AutoSize = true, Location = new Point(102, 39), ForeColor = Color.FromArgb(166, 177, 192), Font = new Font("Segoe UI", 8.8f) };
            Panel settingsHost = new Panel { Dock = DockStyle.Right, Width = 282 };
            Button projectButton = MakeButton("Проект", Color.FromArgb(48, 58, 72), Color.White, 126, 36);
            projectButton.Location = new Point(10, 16);
            projectButton.Image = CreateHouseIcon(Color.White);
            projectButton.ImageAlign = ContentAlignment.MiddleLeft;
            projectButton.TextAlign = ContentAlignment.MiddleCenter;
            projectButton.Padding = new Padding(13, 0, 5, 0);
            projectButton.Click += delegate { OpenProjects(); };
            projectButton.Enabled = !projectEditMode;
            Button settingsButton = MakeButton("⚙  Настройки", Color.FromArgb(48, 58, 72), Color.White, 126, 36);
            settingsButton.Location = new Point(146, 16);
            settingsButton.Click += delegate { OpenSettings(); };
            settingsHost.Controls.Add(projectButton); settingsHost.Controls.Add(settingsButton);
            Label separator = new Label { AutoSize = false, Width = 1, Height = 38, Location = new Point(475, 15), BackColor = Color.FromArgb(74, 85, 101) };
            calculationName = new TextBox { Text = "Новый расчёт", Location = new Point(496, 16), Width = 600, Height = 31, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(27, 34, 45), ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 15f), ReadOnly = true, TabStop = false };
            calculationName.DoubleClick += delegate { BeginNameEdit(); };
            calculationName.KeyDown += OnCalculationNameKeyDown;
            calculationName.LostFocus += delegate { FinishNameEdit(false); };
            header.Resize += delegate { calculationName.Width = Math.Max(180, settingsHost.Left - calculationName.Left - 16); };
            header.Controls.Add(logo); header.Controls.Add(title); header.Controls.Add(subtitle); header.Controls.Add(separator); header.Controls.Add(calculationName); header.Controls.Add(settingsHost);
            root.Controls.Add(header, 0, 0);

            Panel status = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14, 5, 14, 0), Margin = new Padding(0) };
            statusLabel = new Label { Text = "Готово к расчёту", Dock = DockStyle.Fill, ForeColor = muted, Font = new Font("Segoe UI", 8.5f) };
            status.Controls.Add(statusLabel);
            root.Controls.Add(status, 0, 2);

            SplitContainer split = new SplitContainer { Dock = DockStyle.Fill, BackColor = canvas, SplitterWidth = 6, FixedPanel = FixedPanel.Panel2 };
            root.Controls.Add(split, 0, 1);
            split.Panel1MinSize = 480;
            split.Panel2MinSize = 410;
            split.SplitterDistance = Math.Max(480, split.ClientSize.Width - 510);

            BuildImageArea(split.Panel1);
            BuildCalculationArea(split.Panel2);
        }

        private void BuildImageArea(Control host)
        {
            host.Padding = new Padding(14, 14, 6, 12);
            host.BackColor = canvas;
            TableLayoutPanel card = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = surface, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
            card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            host.Controls.Add(card);

            Panel toolbar = new Panel { Dock = DockStyle.Fill };
            Label heading = new Label { Text = "Эскизы изделия", AutoSize = true, Location = new Point(2, 8), Font = new Font("Segoe UI Semibold", 12.5f), ForeColor = ink };
            toolbar.Controls.Add(heading);
            card.Controls.Add(toolbar, 0, 0);

            Panel listToolbar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 7, 0, 0) };
            moveUpButton = MakeButton("↑", Color.FromArgb(233, 237, 242), ink, 34, 28);
            moveUpButton.Location = new Point(0, 7); moveUpButton.Click += delegate { MoveImage(-1); };
            moveDownButton = MakeButton("↓", Color.FromArgb(233, 237, 242), ink, 34, 28);
            moveDownButton.Location = new Point(40, 7); moveDownButton.Click += delegate { MoveImage(1); };
            imageCounter = new Label { Text = "Нет изображений · максимум 3", AutoSize = true, Location = new Point(88, 13), ForeColor = muted };
            removeButton = MakeButton("Удалить", Color.FromArgb(247, 235, 235), Color.FromArgb(174, 54, 54), 80, 27);
            removeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            removeButton.Location = new Point(listToolbar.Width - 80, 7);
            removeButton.Click += delegate { RemoveSelectedImage(); };
            Button addButton = MakeButton("＋  Загрузить эскиз", accent, Color.White, 150, 27);
            addButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            addButton.Location = new Point(listToolbar.Width - 238, 7);
            addButton.Click += delegate { ChooseImages(); };
            listToolbar.Resize += delegate
            {
                removeButton.Left = listToolbar.ClientSize.Width - removeButton.Width;
                addButton.Left = removeButton.Left - addButton.Width - 8;
            };
            listToolbar.Controls.Add(moveUpButton); listToolbar.Controls.Add(moveDownButton); listToolbar.Controls.Add(imageCounter); listToolbar.Controls.Add(addButton); listToolbar.Controls.Add(removeButton);
            card.Controls.Add(listToolbar, 0, 2);

            Panel viewer = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(38, 43, 52), Padding = new Padding(8) };
            imageGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, BackColor = Color.FromArgb(38, 43, 52), Padding = new Padding(0), Margin = new Padding(0) };
            imageGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            emptyHint = new Label { Dock = DockStyle.Fill, Text = "Перетащите сюда до трёх эскизов\r\nили нажмите «Загрузить эскиз»\r\n\r\nJPG · PNG · BMP · GIF · TIFF", TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(184, 192, 204), Font = new Font("Segoe UI", 12f), BackColor = Color.FromArgb(38, 43, 52) };
            emptyHint.Click += delegate { ChooseImages(); };
            viewer.Controls.Add(imageGrid); viewer.Controls.Add(emptyHint);
            card.Controls.Add(viewer, 0, 1);
            UpdateImageNavigation();
        }

        private void BuildCalculationArea(Control host)
        {
            host.Padding = new Padding(6, 14, 14, 12);
            host.BackColor = canvas;
            Panel outer = new Panel { Dock = DockStyle.Fill, BackColor = surface };
            host.Controls.Add(outer);

            Panel scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(18, 16, 18, 20), BackColor = surface };
            outer.Controls.Add(scroll);
            FlowLayoutPanel stack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, BackColor = surface, Margin = new Padding(0) };
            scroll.Controls.Add(stack);
            scroll.Resize += delegate { stack.Width = Math.Max(360, scroll.ClientSize.Width - 38); };

            Label sectionTitle = SectionTitle("Расчёт стоимости");
            stack.Controls.Add(sectionTitle);

            TableLayoutPanel priceCard = new TableLayoutPanel { Width = 480, Height = 108, ColumnCount = 2, RowCount = 2, BackColor = Color.FromArgb(237, 244, 255), Margin = new Padding(0, 2, 0, 12), Padding = new Padding(15, 10, 15, 9) };
            priceCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48)); priceCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
            priceCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 27)); priceCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            priceCard.Controls.Add(new Label { Text = "Расчётная", Dock = DockStyle.Fill, ForeColor = muted, TextAlign = ContentAlignment.BottomLeft }, 0, 0);
            priceCard.Controls.Add(new Label { Text = "Рекомендуемая", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(31, 93, 191), Font = new Font("Segoe UI Semibold", 9.5f), TextAlign = ContentAlignment.BottomLeft }, 1, 0);
            calculatedValue = new Label { Text = "0 ₽", Dock = DockStyle.Fill, ForeColor = ink, Font = new Font("Segoe UI Semibold", 19f), TextAlign = ContentAlignment.MiddleLeft };
            recommendedValue = new Label { Text = "0 ₽", Dock = DockStyle.Fill, ForeColor = accent, Font = new Font("Segoe UI Semibold", 24f), TextAlign = ContentAlignment.MiddleLeft };
            priceCard.Controls.Add(calculatedValue, 0, 1); priceCard.Controls.Add(recommendedValue, 1, 1);
            stack.Controls.Add(priceCard);

            minimumBadge = new Label { Text = "", AutoSize = false, Height = 28, Width = 480, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(142, 91, 20), BackColor = Color.FromArgb(255, 247, 222), Padding = new Padding(9, 0, 0, 0), Margin = new Padding(0, -4, 0, 12), Visible = false };
            stack.Controls.Add(minimumBadge);

            stack.Controls.Add(SectionTitle("Габариты и ставка"));
            TableLayoutPanel dimensions = new TableLayoutPanel { Width = 480, Height = 186, ColumnCount = 3, RowCount = 6, Margin = new Padding(0, 2, 0, 12) };
            for (int i = 0; i < 3; i++) dimensions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            dimensions.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); dimensions.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); dimensions.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); dimensions.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            dimensions.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); dimensions.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            dimensions.Controls.Add(FieldLabel("Ширина, мм"), 0, 0); dimensions.Controls.Add(FieldLabel("Высота, мм"), 1, 0); dimensions.Controls.Add(FieldLabel("Глубина, мм"), 2, 0);
            widthInput = DimensionNumber(2000); heightInput = DimensionNumber(2200); depthInput = DimensionNumber(600);
            dimensions.Controls.Add(widthInput, 0, 1); dimensions.Controls.Add(heightInput, 1, 1); dimensions.Controls.Add(depthInput, 2, 1);
            dimensions.Controls.Add(FieldLabel("Базовая ставка, ₽"), 0, 2); dimensions.SetColumnSpan(dimensions.GetControlFromPosition(0, 2), 2);
            dimensions.Controls.Add(FieldLabel("Минимальная цена, ₽"), 2, 2);
            baseRateInput = MoneyNumber(); minimumInput = MoneyNumber();
            dimensions.Controls.Add(baseRateInput, 0, 3); dimensions.SetColumnSpan(baseRateInput, 2); dimensions.Controls.Add(minimumInput, 2, 3);
            dimensions.Controls.Add(FieldLabel("Стоимость выезда и замера, ₽"), 0, 4); dimensions.SetColumnSpan(dimensions.GetControlFromPosition(0, 4), 3);
            measurementFeeInput = MoneyNumber();
            dimensions.Controls.Add(measurementFeeInput, 0, 5); dimensions.SetColumnSpan(measurementFeeInput, 3);
            stack.Controls.Add(dimensions);

            complexityPanel = new Panel { Width = 480, Height = 1, Margin = new Padding(0, 0, 0, 12), BackColor = Color.White };
            complexityPanel.Resize += delegate
            {
                foreach (Control control in complexityPanel.Controls)
                    control.Width = Math.Max(320, complexityPanel.ClientSize.Width);
            };
            stack.Controls.Add(complexityPanel);

            stack.Controls.Add(SectionTitle("Расшифровка"));
            explanation = new RichTextBox { Width = 480, Height = 145, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(247, 248, 250), ForeColor = Color.FromArgb(57, 64, 75), Font = new Font("Consolas", 9.3f), ScrollBars = RichTextBoxScrollBars.Vertical, Margin = new Padding(0, 2, 0, 14), TabStop = false };
            stack.Controls.Add(explanation);

            stack.Controls.Add(SectionTitle("Уточняющая стоимость"));
            TableLayoutPanel custom = new TableLayoutPanel { Width = 480, Height = 72, ColumnCount = 2, RowCount = 2, Margin = new Padding(0, 2, 0, 12), BackColor = Color.FromArgb(250, 250, 251), Padding = new Padding(10, 7, 10, 6) };
            custom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52)); custom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            custom.Controls.Add(new Label { Text = "Уточнённая стоимость РКД, ₽", Dock = DockStyle.Fill, ForeColor = muted, TextAlign = ContentAlignment.BottomLeft }, 0, 0);
            myPriceComparison = new Label { Text = "", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomRight, ForeColor = muted, Font = new Font("Segoe UI Semibold", 9f) };
            custom.Controls.Add(myPriceComparison, 1, 0);
            myPriceInput = MoneyNumber(); myPriceInput.Maximum = 10000000; myPriceInput.Increment = 500; myPriceInput.ValueChanged += delegate { UpdateMyPriceComparison(); };
            custom.Controls.Add(myPriceInput, 0, 1); custom.SetColumnSpan(myPriceInput, 2);
            stack.Controls.Add(custom);

            if (projectEditMode)
            {
                Button saveToProject = MakeButton("Сохранить изделие в проект", Color.FromArgb(45, 134, 84), Color.White, 480, 42);
                saveToProject.Margin = new Padding(0, 0, 0, 8);
                saveToProject.Click += delegate
                {
                    ProjectItemResult = CaptureProjectItem();
                    DialogResult = DialogResult.OK;
                    Close();
                };
                stack.Controls.Add(saveToProject);
            }

            Button clear = MakeButton("Очистить расчёт", Color.FromArgb(235, 238, 242), Color.FromArgb(64, 72, 83), 480, 38);
            clear.Margin = new Padding(0, 0, 0, 8);
            clear.Click += delegate { ResetCalculation(); };
            stack.Controls.Add(clear);

            Button createPdf = MakeButton("Сформировать смету PDF", accent, Color.White, 480, 42);
            createPdf.Margin = new Padding(0, 0, 0, 20);
            createPdf.Click += delegate { ExportEstimatePdf(); };
            stack.Controls.Add(createPdf);

            EventHandler recalc = delegate { Recalculate(); };
            widthInput.ValueChanged += recalc; heightInput.ValueChanged += recalc; depthInput.ValueChanged += recalc;
            baseRateInput.ValueChanged += recalc; minimumInput.ValueChanged += recalc;
            measurementFeeInput.ValueChanged += recalc;

            stack.SizeChanged += delegate
            {
                int w = Math.Max(360, stack.ClientSize.Width);
                foreach (Control control in stack.Controls) control.Width = w;
            };
        }

        private void RebuildComplexityGroups(HashSet<string> checkedIds)
        {
            complexityPanel.SuspendLayout();
            complexityPanel.Controls.Clear();
            complexityChecks.Clear();
            int y = 0;
            foreach (IGrouping<string, ComplexityOption> group in settings.Options.GroupBy(o => o.Group))
            {
                Label groupTitle = new Label { Text = group.Key.ToUpperInvariant(), Location = new Point(0, y), Width = Math.Max(320, complexityPanel.Width), Height = 28, Padding = new Padding(0, 5, 8, 0), TextAlign = ContentAlignment.MiddleRight, ForeColor = Color.FromArgb(112, 120, 132), Font = new Font("Segoe UI Semibold", 8.3f), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                complexityPanel.Controls.Add(groupTitle);
                y += 28;
                foreach (ComplexityOption option in group)
                {
                    string optionText = option.Id == "site_measure" ? option.Name : option.Name + "   +" + option.Percent.ToString("0.#") + "%";
                    CheckBox box = new CheckBox { Text = optionText, Tag = option, Location = new Point(0, y), Width = Math.Max(320, complexityPanel.Width), Height = 31, Padding = new Padding(7, 0, 0, 0), ForeColor = ink, BackColor = Color.FromArgb(249, 250, 251), Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                    box.Checked = checkedIds != null && checkedIds.Contains(option.Id);
                    box.CheckedChanged += delegate { Recalculate(); };
                    complexityChecks.Add(box);
                    complexityPanel.Controls.Add(box);
                    y += 33;
                }
                y += 3;
            }
            complexityPanel.Height = y + 2;
            complexityPanel.ResumeLayout();
        }

        private void Recalculate()
        {
            if (widthInput == null || complexityChecks == null) return;
            decimal surchargePercent = GetCashlessPercent();
            IEnumerable<decimal> percents = GetSelectedComplexityPercents();
            PriceResult result = PricingCalculator.Calculate(widthInput.Value, heightInput.Value, depthInput.Value, baseRateInput.Value, minimumInput.Value, settings.RoundTo, percents, surchargePercent, GetMeasurementFee());
            calculatedValue.Text = FormatMoney(result.CalculatedPrice);
            recommendedValue.Text = FormatMoney(result.RecommendedPrice);
            minimumBadge.Visible = result.MinimumApplied;
            minimumBadge.Text = "●  Применён установленный минимум " + FormatMoney(minimumInput.Value);

            List<ComplexityOption> selected = complexityChecks.Where(c => c.Checked && ((ComplexityOption)c.Tag).Id != "payment_cashless" && ((ComplexityOption)c.Tag).Id != "site_measure").Select(c => (ComplexityOption)c.Tag).ToList();
            string details =
                "Базовая ставка                         " + FormatMoney(baseRateInput.Value).PadLeft(12) + Environment.NewLine +
                "Коэффициент габарита                   × " + result.SizeCoefficient.ToString("0.00") + Environment.NewLine +
                "Основа с учётом габарита              " + FormatMoney(result.SizeAdjustedBase).PadLeft(12) + Environment.NewLine +
                ("Коэффициенты (" + result.ComplexityPercent.ToString("0.#") + "%)").PadRight(40) + FormatMoney(result.ComplexityAmount).PadLeft(12) + Environment.NewLine +
                (result.SurchargePercent > 0m ? ("Безналичная оплата (+" + result.SurchargePercent.ToString("0.#") + "%)").PadRight(40) + FormatMoney(result.SurchargeAmount).PadLeft(12) + Environment.NewLine : "") +
                "────────────────────────────────────────────────" + Environment.NewLine +
                "Расчётная стоимость РКД                " + FormatMoney(result.WorkCalculatedPrice).PadLeft(12) + Environment.NewLine +
                "Рекомендуемая стоимость РКД            " + FormatMoney(result.WorkRecommendedPrice).PadLeft(12) + Environment.NewLine +
                (result.MeasurementAmount > 0m ? "Выезд и замер                           " + FormatMoney(result.MeasurementAmount).PadLeft(12) + Environment.NewLine : "") +
                "ИТОГО                                   " + FormatMoney(result.RecommendedPrice).PadLeft(12);
            if (selected.Count > 0)
                details += Environment.NewLine + Environment.NewLine + "Выбрано: " + string.Join(", ", selected.Select(o => o.Name + " +" + o.Percent.ToString("0.#") + "%"));
            explanation.Text = details;
            statusLabel.Text = "Габарит: " + widthInput.Value.ToString("0") + " × " + heightInput.Value.ToString("0") + " × " + depthInput.Value.ToString("0") + " мм  ·  коэффициенты: +" + result.ComplexityPercent.ToString("0.#") + "%" + (result.SurchargePercent > 0m ? "  ·  безнал: +" + result.SurchargePercent.ToString("0.#") + "%" : "") + (result.MeasurementAmount > 0m ? "  ·  замер: " + FormatMoney(result.MeasurementAmount) : "");
            UpdateMyPriceComparison();
        }

        private IEnumerable<decimal> GetSelectedComplexityPercents()
        {
            return complexityChecks.Where(c => c.Checked && ((ComplexityOption)c.Tag).Id != "payment_cashless" && ((ComplexityOption)c.Tag).Id != "site_measure")
                .Select(c => ((ComplexityOption)c.Tag).Percent);
        }

        private decimal GetCashlessPercent()
        {
            CheckBox payment = complexityChecks.FirstOrDefault(c => ((ComplexityOption)c.Tag).Id == "payment_cashless");
            return payment != null && payment.Checked ? ((ComplexityOption)payment.Tag).Percent : 0m;
        }

        private decimal GetMeasurementFee()
        {
            CheckBox measurement = complexityChecks.FirstOrDefault(c => ((ComplexityOption)c.Tag).Id == "site_measure");
            return measurement != null && measurement.Checked ? measurementFeeInput.Value : 0m;
        }

        private void BeginNameEdit()
        {
            nameBeforeEdit = calculationName.Text;
            calculationName.ReadOnly = false;
            calculationName.TabStop = true;
            calculationName.BackColor = Color.FromArgb(48, 58, 72);
            calculationName.SelectAll();
            calculationName.Focus();
        }

        private void FinishNameEdit(bool cancel)
        {
            if (calculationName.ReadOnly) return;
            if (cancel) calculationName.Text = nameBeforeEdit;
            if (string.IsNullOrWhiteSpace(calculationName.Text)) calculationName.Text = "Новый расчёт";
            calculationName.Text = calculationName.Text.Trim();
            calculationName.ReadOnly = true;
            calculationName.TabStop = false;
            calculationName.BackColor = Color.FromArgb(27, 34, 45);
        }

        private void OnCalculationNameKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { FinishNameEdit(false); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Escape) { FinishNameEdit(true); e.SuppressKeyPress = true; }
        }

        private static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Новый расчёт" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars()) result = result.Replace(invalid, '_');
            return result;
        }

        private void ApplySettingsToInputs()
        {
            if (baseRateInput == null) return;
            baseRateInput.Value = Math.Min(baseRateInput.Maximum, settings.BaseRate);
            minimumInput.Value = Math.Min(minimumInput.Maximum, settings.MinimumPrice);
            measurementFeeInput.Value = Math.Min(measurementFeeInput.Maximum, settings.MeasurementFee);
        }

        private void OpenSettings()
        {
            HashSet<string> selected = new HashSet<string>(complexityChecks.Where(c => c.Checked).Select(c => ((ComplexityOption)c.Tag).Id));
            using (SettingsForm dialog = new SettingsForm(settings))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                settings = dialog.Result;
                SettingsStore.Save(settings);
                ApplySettingsToInputs();
                RebuildComplexityGroups(selected);
                Recalculate();
                statusLabel.Text = "Настройки сохранены";
            }
        }

        private void OpenProjects()
        {
            using (ProjectForm dialog = new ProjectForm(settings))
            {
                dialog.ShowDialog(this);
            }
        }

        private void LoadProjectItem(ProjectItem item)
        {
            if (item == null) return;
            calculationName.Text = string.IsNullOrWhiteSpace(item.Name) ? "Новое изделие" : item.Name;
            widthInput.Value = Clamp(item.Width <= 0m ? 2000m : item.Width, widthInput.Minimum, widthInput.Maximum);
            heightInput.Value = Clamp(item.Height <= 0m ? 2200m : item.Height, heightInput.Minimum, heightInput.Maximum);
            depthInput.Value = Clamp(item.Depth <= 0m ? 600m : item.Depth, depthInput.Minimum, depthInput.Maximum);
            baseRateInput.Value = Clamp(item.BaseRate <= 0m ? settings.BaseRate : item.BaseRate, baseRateInput.Minimum, baseRateInput.Maximum);
            minimumInput.Value = Clamp(item.MinimumPrice <= 0m ? settings.MinimumPrice : item.MinimumPrice, minimumInput.Minimum, minimumInput.Maximum);
            myPriceInput.Value = Clamp(item.ClarifiedPrice, myPriceInput.Minimum, myPriceInput.Maximum);

            HashSet<string> selected = new HashSet<string>(item.CheckedOptionIds ?? new List<string>());
            selected.Remove("payment_cashless");
            selected.Remove("site_measure");
            foreach (CheckBox box in complexityChecks)
            {
                ComplexityOption option = (ComplexityOption)box.Tag;
                box.Checked = selected.Contains(option.Id);
                if (option.Id == "payment_cashless" || option.Id == "site_measure")
                {
                    box.Checked = false;
                    box.Enabled = false;
                    box.Text = option.Name + "   (задаётся для всего проекта)";
                }
            }
            measurementFeeInput.Enabled = false;
            if (item.ImagePaths != null) AddImages(item.ImagePaths.Where(File.Exists));
            statusLabel.Text = "Изделие открыто из проекта";
        }

        private ProjectItem CaptureProjectItem()
        {
            IEnumerable<decimal> percents = GetSelectedComplexityPercents();
            PriceResult result = PricingCalculator.Calculate(widthInput.Value, heightInput.Value, depthInput.Value,
                baseRateInput.Value, minimumInput.Value, settings.RoundTo, percents, 0m, 0m);
            decimal finalPrice = myPriceInput.Value > 0m ? myPriceInput.Value : result.WorkRecommendedPrice;
            return new ProjectItem
            {
                Name = string.IsNullOrWhiteSpace(calculationName.Text) ? "Новое изделие" : calculationName.Text.Trim(),
                Width = widthInput.Value,
                Height = heightInput.Value,
                Depth = depthInput.Value,
                BaseRate = baseRateInput.Value,
                MinimumPrice = minimumInput.Value,
                ClarifiedPrice = myPriceInput.Value,
                FinalWorkPrice = finalPrice,
                CheckedOptionIds = complexityChecks.Where(c => c.Checked)
                    .Select(c => ((ComplexityOption)c.Tag).Id)
                    .Where(id => id != "payment_cashless" && id != "site_measure").ToList(),
                ImagePaths = images.Select(i => i.Path).ToList()
            };
        }

        private static decimal Clamp(decimal value, decimal minimum, decimal maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private void ExportEstimatePdf()
        {
            IEnumerable<decimal> percents = GetSelectedComplexityPercents();
            PriceResult result = PricingCalculator.Calculate(widthInput.Value, heightInput.Value, depthInput.Value,
                baseRateInput.Value, minimumInput.Value, settings.RoundTo, percents, GetCashlessPercent(), GetMeasurementFee());
            decimal finalWorkPrice = myPriceInput.Value > 0m ? myPriceInput.Value : result.WorkRecommendedPrice;

            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Сохранить предложение стоимости";
                dialog.Filter = "PDF-файл|*.pdf";
                dialog.DefaultExt = "pdf";
                dialog.AddExtension = true;
                dialog.FileName = "КП на РКД " + SanitizeFileName(calculationName.Text) + " " + DateTime.Now.ToString("dd.MM.yyyy") + ".pdf";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    PdfEstimateGenerator.Generate(dialog.FileName, calculationName.Text.Trim(), images.Select(i => i.Image).ToList(), finalWorkPrice, result.MeasurementAmount);
                    statusLabel.Text = "PDF-смета сохранена: " + Path.GetFileName(dialog.FileName);
                    MessageBox.Show(this, "PDF-смета сформирована и сохранена.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Не удалось сформировать PDF:\r\n" + ex.Message, "Ошибка PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ChooseImages()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Выберите эскизы изделия";
                dialog.Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|Все файлы|*.*";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) == DialogResult.OK) AddImages(dialog.FileNames);
            }
        }

        private void AddImages(IEnumerable<string> paths)
        {
            int added = 0;
            int skippedForLimit = 0;
            foreach (string path in paths)
            {
                if (images.Count >= 3) { skippedForLimit++; continue; }
                try
                {
                    string full = Path.GetFullPath(path);
                    if (images.Any(i => string.Equals(i.Path, full, StringComparison.OrdinalIgnoreCase))) continue;
                    using (FileStream stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (Image source = Image.FromStream(stream))
                    {
                        Bitmap fullImage = new Bitmap(source);
                        ImageItem item = new ImageItem { Path = full, Image = fullImage };
                        images.Add(item);
                        added++;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Не удалось открыть файл:\r\n" + path + "\r\n\r\n" + ex.Message, "Ошибка изображения", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            if (added > 0)
            {
                selectedImageIndex = images.Count - added;
                RenderImageGrid();
                statusLabel.Text = "Добавлено изображений: " + added;
            }
            if (skippedForLimit > 0)
                MessageBox.Show(this, "В расчёт можно загрузить не более трёх эскизов.", "Лимит эскизов", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateImageNavigation();
        }

        private void RenderImageGrid()
        {
            imageGrid.SuspendLayout();
            imageGrid.Controls.Clear();
            imageGrid.RowStyles.Clear();
            imageGrid.RowCount = Math.Max(1, images.Count);
            if (images.Count == 0)
            {
                emptyHint.Visible = true;
                imageGrid.ResumeLayout();
                return;
            }

            emptyHint.Visible = false;
            for (int i = 0; i < images.Count; i++)
            {
                int index = i;
                imageGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / images.Count));
                Panel border = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, i == 0 ? 0 : 4, 0, i == images.Count - 1 ? 0 : 4), Padding = new Padding(3), BackColor = i == selectedImageIndex ? accent : Color.FromArgb(66, 74, 86), Cursor = Cursors.Hand };
                TableLayoutPanel card = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.FromArgb(38, 43, 52), Margin = new Padding(0), Padding = new Padding(0) };
                card.RowStyles.Add(new RowStyle(SizeType.Absolute, 25f));
                card.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                Label name = new Label { Text = (i + 1) + ". " + Path.GetFileName(images[i].Path), Dock = DockStyle.Fill, ForeColor = i == selectedImageIndex ? Color.White : Color.FromArgb(190, 198, 209), BackColor = Color.FromArgb(45, 51, 61), Padding = new Padding(7, 4, 0, 0), Font = new Font("Segoe UI", 8.5f), Cursor = Cursors.Hand };
                PictureBox picture = new PictureBox { Image = images[i].Image, Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(38, 43, 52), Cursor = Cursors.Hand };
                EventHandler select = delegate { SelectImage(index); };
                border.Click += select; card.Click += select; name.Click += select; picture.Click += select;
                card.Controls.Add(name, 0, 0); card.Controls.Add(picture, 0, 1);
                border.Controls.Add(card);
                imageGrid.Controls.Add(border, 0, i);
            }
            imageGrid.ResumeLayout();
        }

        private void SelectImage(int index)
        {
            if (index < 0 || index >= images.Count) return;
            selectedImageIndex = index;
            RenderImageGrid();
            UpdateImageNavigation();
        }

        private void MoveImage(int delta)
        {
            if (selectedImageIndex < 0 || selectedImageIndex >= images.Count) return;
            int target = selectedImageIndex + delta;
            if (target < 0 || target >= images.Count) return;
            ImageItem item = images[selectedImageIndex];
            images.RemoveAt(selectedImageIndex);
            images.Insert(target, item);
            selectedImageIndex = target;
            RenderImageGrid();
            UpdateImageNavigation();
        }

        private void RemoveSelectedImage()
        {
            if (selectedImageIndex < 0 || selectedImageIndex >= images.Count) return;
            images[selectedImageIndex].Image.Dispose();
            images.RemoveAt(selectedImageIndex);
            selectedImageIndex = images.Count == 0 ? -1 : Math.Min(selectedImageIndex, images.Count - 1);
            RenderImageGrid();
            UpdateImageNavigation();
        }

        private void UpdateImageNavigation()
        {
            bool has = images.Count > 0;
            removeButton.Enabled = has;
            moveUpButton.Enabled = has && selectedImageIndex > 0;
            moveDownButton.Enabled = has && selectedImageIndex >= 0 && selectedImageIndex < images.Count - 1;
            imageCounter.Text = has ? ((selectedImageIndex + 1) + " из " + images.Count + " · кнопки ↑↓ меняют порядок") : "Нет изображений · максимум 3";
        }

        private void ResetCalculation()
        {
            widthInput.Value = 2000; heightInput.Value = 2200; depthInput.Value = 600;
            baseRateInput.Value = Math.Min(baseRateInput.Maximum, settings.BaseRate);
            minimumInput.Value = Math.Min(minimumInput.Maximum, settings.MinimumPrice);
            measurementFeeInput.Value = Math.Min(measurementFeeInput.Maximum, settings.MeasurementFee);
            foreach (CheckBox box in complexityChecks) box.Checked = false;
            myPriceInput.Value = 0;
            calculationName.Text = "Новый расчёт";
            Recalculate();
            statusLabel.Text = "Расчёт очищен";
        }

        private void UpdateMyPriceComparison()
        {
            if (myPriceInput == null || recommendedValue == null) return;
            IEnumerable<decimal> percents = GetSelectedComplexityPercents();
            PriceResult result = PricingCalculator.Calculate(widthInput.Value, heightInput.Value, depthInput.Value, baseRateInput.Value, minimumInput.Value, settings.RoundTo, percents, GetCashlessPercent(), GetMeasurementFee());
            if (myPriceInput.Value == 0m) { myPriceComparison.Text = "не задана"; myPriceComparison.ForeColor = muted; return; }
            decimal difference = myPriceInput.Value - result.WorkRecommendedPrice;
            myPriceComparison.Text = difference == 0m ? "равна рекомендации" : (difference > 0m ? "+" : "") + FormatMoney(difference) + " к рекомендации";
            myPriceComparison.ForeColor = difference < 0m ? Color.FromArgb(181, 67, 55) : Color.FromArgb(45, 134, 84);
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null) AddImages(files.Where(IsSupportedImage));
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.O) { ChooseImages(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Up) { MoveImage(-1); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Down) { MoveImage(1); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete && selectedImageIndex >= 0) RemoveSelectedImage();
        }

        private void DisposeImages()
        {
            foreach (ImageItem item in images) item.Image.Dispose();
        }

        private static bool IsSupportedImage(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".bmp" || ext == ".gif" || ext == ".tif" || ext == ".tiff";
        }

        private static string FormatMoney(decimal amount)
        {
            return Math.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("N0", new CultureInfo("ru-RU")) + " ₽";
        }

        private Label SectionTitle(string text) { return new Label { Text = text, Width = 480, Height = 32, ForeColor = ink, Font = new Font("Segoe UI Semibold", 11.3f), TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 0, 2) }; }
        private Label FieldLabel(string text) { return new Label { Text = text, Dock = DockStyle.Fill, ForeColor = muted, TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(0, 0, 5, 2) }; }
        private NumericUpDown DimensionNumber(decimal value) { NumericUpDown n = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1, Maximum = 30000, Increment = 50, Value = value, ThousandsSeparator = true, Font = new Font("Segoe UI", 10.5f), Margin = new Padding(0, 0, 7, 3) }; return n; }
        private NumericUpDown MoneyNumber() { return new NumericUpDown { Dock = DockStyle.Fill, Minimum = 0, Maximum = 1000000, Increment = 500, ThousandsSeparator = true, Font = new Font("Segoe UI", 10.5f), Margin = new Padding(0, 0, 7, 2) }; }
        private Button MakeButton(string text, Color back, Color fore, int width, int height) { return new Button { Text = text, Width = width, Height = height, FlatStyle = FlatStyle.Flat, BackColor = back, ForeColor = fore, Font = new Font("Segoe UI Semibold", 9.2f), Cursor = Cursors.Hand, FlatAppearance = { BorderSize = 0 } }; }

        private static Bitmap CreateHouseIcon(Color color)
        {
            Bitmap bitmap = new Bitmap(18, 18, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            using (Pen pen = new Pen(color, 1.8f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLines(pen, new[] { new PointF(2f, 8f), new PointF(9f, 2f), new PointF(16f, 8f) });
                g.DrawRectangle(pen, 4f, 7.5f, 10f, 8f);
                g.DrawRectangle(pen, 8f, 11f, 3f, 4.5f);
            }
            return bitmap;
        }

        private sealed class ImageItem
        {
            public string Path { get; set; }
            public Image Image { get; set; }
        }
    }
}
