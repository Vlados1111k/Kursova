using System.ComponentModel;
using ParkingCourseWork.Domain;
using ParkingCourseWork.Infrastructure;

namespace ParkingCourseWork.WinFormsUi;

public class MainForm : Form
{
    private readonly ParkingManager _manager = new();
    private readonly JsonParkingStorage _storage = new();
    private readonly string _dataPath = Path.Combine(AppContext.BaseDirectory, "parking_state.json");

    private readonly DataGridView _placesGrid = new();
    private readonly DataGridView _sessionsGrid = new();
    private readonly DataGridView _paymentsGrid = new();
    private readonly DataGridView _finesGrid = new();

    private readonly Label _occupancyLabel = new();
    private readonly Label _activeLabel = new();
    private readonly Label _completedLabel = new();
    private readonly Label _debtLabel = new();
    private readonly Label _clockLabel = new();
    private readonly Label _tariffLabel = new();

    private readonly TextBox _plateTextBox = new();
    private readonly ComboBox _placeCombo = new();

    private readonly NumericUpDown _paymentAmount = new();
    private readonly ComboBox _paymentMethodCombo = new();

    private readonly TextBox _sessionSearchTextBox = new();
    private readonly ComboBox _sessionStatusFilterCombo = new();
    private readonly CheckBox _onlyDebtCheckBox = new();

    private readonly Label _selectedSessionTitleLabel = new();
    private readonly Label _selectedSessionInfoLabel = new();
    private readonly Label _selectedAmountsLabel = new();
    private readonly Label _selectedTimeLabel = new();

    private readonly Label _hintLabel = new();

    private readonly System.Windows.Forms.Timer _timer = new();

    private SplitContainer? _rootSplit;
    private SplitContainer? _topSplit;

    private readonly BindingList<ParkingPlaceRow> _placeRows = new();
    private readonly BindingList<SessionRow> _sessionRows = new();
    private readonly BindingList<PaymentRow> _paymentRows = new();
    private readonly BindingList<FineRow> _fineRows = new();

    private Guid? _lastSelectedSessionId;

    public MainForm()
    {
        Text = "Паркування: місця, оплата, штрафи";
        Width = 1560;
        Height = 920;
        MinimumSize = new Size(1280, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10f);

        BuildUi();
        Shown += (_, _) => ApplySplitLayoutSettings();
        Resize += (_, _) => ApplySplitLayoutSettings();

        _manager.StateChanged += RefreshAllBindings;

        _paymentMethodCombo.DataSource = Enum.GetValues(typeof(PaymentMethod));

        _sessionStatusFilterCombo.Items.AddRange(new object[]
        {
            "Усі сесії",
            "Активні",
            "Завершені"
        });
        _sessionStatusFilterCombo.SelectedIndex = 0;

        _timer.Interval = 1000;
        _timer.Tick += (_, _) => UpdateDashboard();
        _timer.Start();

        if (File.Exists(_dataPath))
        {
            TryLoadFromFile(true);
        }
        else
        {
            _manager.InitializeDefaultPlaces();
        }

        RefreshAllBindings();
    }

    private void ApplySplitLayoutSettings()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        BeginInvoke(new Action(() =>
        {
            if (_rootSplit is not null && !_rootSplit.IsDisposed)
            {
                _rootSplit.Panel1MinSize = 320;
                _rootSplit.Panel2MinSize = 540;
                SafeSetSplitterDistance(_rootSplit, 400);
            }

            if (_topSplit is not null && !_topSplit.IsDisposed)
            {
                _topSplit.Panel1MinSize = 300;
                _topSplit.Panel2MinSize = 360;
                SafeSetSplitterDistance(_topSplit, 430);
            }
        }));
    }

    private static void SafeSetSplitterDistance(SplitContainer split, int desired)
    {
        var total = split.Orientation == Orientation.Vertical ? split.ClientSize.Width : split.ClientSize.Height;

        if (total <= 0)
        {
            return;
        }

        var min = split.Panel1MinSize;
        var max = total - split.Panel2MinSize - split.SplitterWidth;

        if (max < min)
        {
            return;
        }

        var value = Math.Max(min, Math.Min(desired, max));
        split.SplitterDistance = value;
    }

    private void BuildUi()
    {
        Controls.Add(BuildRootLayout());
    }

    private Control BuildRootLayout()
    {
        _rootSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            IsSplitterFixed = false
        };

        var split = _rootSplit!;

        split.Panel1.Controls.Add(BuildLeftPanel());
        split.Panel2.Controls.Add(BuildRightPanel());

        return split;
    }

    private Control BuildLeftPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.FromArgb(248, 249, 252) };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        var titleCard = new Panel
        {
            Width = 360,
            Height = 80,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12)
        };
        titleCard.Paint += PaintCardBorder;

        var title = new Label
        {
            Text = "Система керування паркуванням",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font(Font.FontFamily, 12.5f, FontStyle.Bold)
        };
        var subtitle = new Label
        {
            Text = "Практична частина курсової роботи (ООП, C# WinForms)",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray
        };

        titleCard.Controls.Add(subtitle);
        titleCard.Controls.Add(title);
        flow.Controls.Add(titleCard);

        flow.Controls.Add(BuildDashboardCard());
        flow.Controls.Add(BuildSessionActionsGroup());
        flow.Controls.Add(BuildPaymentGroup());
        flow.Controls.Add(BuildFinesGroup());
        flow.Controls.Add(BuildDataGroup());
        flow.Controls.Add(BuildSelectedSessionGroup());

        panel.Controls.Add(flow);
        return panel;
    }

    private Control BuildDashboardCard()
    {
        var card = new Panel
        {
            Width = 360,
            Height = 210,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12)
        };
        card.Paint += PaintCardBorder;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7
        };

        for (var i = 0; i < 7; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 14.2857f));
        }

        var header = new Label
        {
            Text = "Оперативна інформація",
            Font = new Font(Font.FontFamily, 11f, FontStyle.Bold),
            Dock = DockStyle.Fill
        };

        PrepareStatLabel(_occupancyLabel);
        PrepareStatLabel(_activeLabel);
        PrepareStatLabel(_completedLabel);
        PrepareStatLabel(_debtLabel);
        PrepareStatLabel(_tariffLabel);
        PrepareStatLabel(_clockLabel);

        _clockLabel.Font = new Font(Font.FontFamily, 9.5f, FontStyle.Regular);

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(_occupancyLabel, 0, 1);
        layout.Controls.Add(_activeLabel, 0, 2);
        layout.Controls.Add(_completedLabel, 0, 3);
        layout.Controls.Add(_debtLabel, 0, 4);
        layout.Controls.Add(_tariffLabel, 0, 5);
        layout.Controls.Add(_clockLabel, 0, 6);

        card.Controls.Add(layout);
        return card;
    }

    private Control BuildSessionActionsGroup()
    {
        var group = CreateGroup("Керування сесією", 240);
        var layout = CreateGroupLayout(group, 2);

        layout.Controls.Add(new Label { Text = "Номер авто", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _plateTextBox.Dock = DockStyle.Fill;
        _plateTextBox.PlaceholderText = "AA1234BB";
        _plateTextBox.TextChanged += (_, _) => _plateTextBox.CharacterCasing = CharacterCasing.Upper;
        _plateTextBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                StartSession();
                e.SuppressKeyPress = true;
            }
        };
        layout.Controls.Add(_plateTextBox, 1, 0);

        layout.Controls.Add(new Label { Text = "Вільне місце", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _placeCombo.Dock = DockStyle.Fill;
        _placeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        layout.Controls.Add(_placeCombo, 1, 1);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0) };
        actions.Controls.Add(MakePrimaryButton("Почати сесію", (_, _) => StartSession(), 150));
        actions.Controls.Add(MakeButton("Завершити сесію", (_, _) => EndSelectedSession(), 150));
        layout.Controls.Add(actions, 0, 2);
        layout.SetColumnSpan(actions, 2);

        var quick = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0) };
        quick.Controls.Add(MakeButton("Обрати місце із таблиці", (_, _) => SyncPlaceSelectionFromGrid(), 150));
        quick.Controls.Add(MakeButton("Оновити списки", (_, _) => RefreshAllBindings(), 150));
        layout.Controls.Add(quick, 0, 3);
        layout.SetColumnSpan(quick, 2);

        return group;
    }

    private Control BuildPaymentGroup()
    {
        var group = CreateGroup("Оплата", 190);
        var layout = CreateGroupLayout(group, 2);

        layout.Controls.Add(new Label { Text = "Сума, грн", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        _paymentAmount.Dock = DockStyle.Fill;
        _paymentAmount.DecimalPlaces = 2;
        _paymentAmount.Maximum = 100000;
        _paymentAmount.Minimum = 0;
        _paymentAmount.Value = 50;
        layout.Controls.Add(_paymentAmount, 1, 0);

        layout.Controls.Add(new Label { Text = "Метод", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _paymentMethodCombo.Dock = DockStyle.Fill;
        _paymentMethodCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        layout.Controls.Add(_paymentMethodCombo, 1, 1);

        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0) };
        row.Controls.Add(MakePrimaryButton("Оплатити", (_, _) => PaySelectedSession(), 150));
        row.Controls.Add(MakeButton("Заповнити борг", (_, _) => FillOutstandingToPaymentBox(), 150));
        layout.Controls.Add(row, 0, 2);
        layout.SetColumnSpan(row, 2);

        return group;
    }

    private Control BuildFinesGroup()
    {
        var group = CreateGroup("Штрафи", 150);
        var layout = CreateGroupLayout(group, 1);

        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0) };
        row.Controls.Add(MakeButton("Штраф: несплата", (_, _) => IssueFineNoPayment(), 150));
        row.Controls.Add(MakeButton("Штраф: перевищення", (_, _) => IssueFineOverstay(), 150));
        layout.Controls.Add(row, 0, 0);

        _hintLabel.Dock = DockStyle.Fill;
        _hintLabel.AutoSize = true;
        _hintLabel.ForeColor = Color.DimGray;
        _hintLabel.Text = "Штраф накладається на обрану сесію в таблиці сесій.";
        layout.Controls.Add(_hintLabel, 0, 1);

        return group;
    }

    private Control BuildDataGroup()
    {
        var group = CreateGroup("Дані та сервіс", 190);
        var layout = CreateGroupLayout(group, 1);

        var row1 = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0) };
        row1.Controls.Add(MakeButton("Створити місця (20)", (_, _) => _manager.InitializeDefaultPlaces(), 150));
        row1.Controls.Add(MakeButton("Зберегти JSON", (_, _) => SaveToFile(), 150));
        layout.Controls.Add(row1, 0, 0);

        var row2 = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0) };
        row2.Controls.Add(MakeButton("Завантажити JSON", (_, _) => TryLoadFromFile(false), 150));
        row2.Controls.Add(MakeButton("Оновити таблиці", (_, _) => RefreshAllBindings(), 150));
        layout.Controls.Add(row2, 0, 1);

        var pathLabel = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ForeColor = Color.DimGray,
            Text = $"Файл даних: {_dataPath}"
        };
        layout.Controls.Add(pathLabel, 0, 2);

        return group;
    }

    private Control BuildSelectedSessionGroup()
    {
        var group = CreateGroup("Обрана сесія", 190);
        var layout = CreateGroupLayout(group, 1);

        _selectedSessionTitleLabel.Dock = DockStyle.Fill;
        _selectedSessionTitleLabel.AutoSize = true;
        _selectedSessionTitleLabel.Font = new Font(Font.FontFamily, 10f, FontStyle.Bold);

        _selectedSessionInfoLabel.Dock = DockStyle.Fill;
        _selectedSessionInfoLabel.AutoSize = true;

        _selectedAmountsLabel.Dock = DockStyle.Fill;
        _selectedAmountsLabel.AutoSize = true;

        _selectedTimeLabel.Dock = DockStyle.Fill;
        _selectedTimeLabel.AutoSize = true;
        _selectedTimeLabel.ForeColor = Color.DimGray;

        layout.Controls.Add(_selectedSessionTitleLabel, 0, 0);
        layout.Controls.Add(_selectedSessionInfoLabel, 0, 1);
        layout.Controls.Add(_selectedAmountsLabel, 0, 2);
        layout.Controls.Add(_selectedTimeLabel, 0, 3);

        return group;
    }

    private Control BuildRightPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = Color.White };
        panel.Controls.Add(BuildRightLayout());
        return panel;
    }

    private Control BuildRightLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));

        root.Controls.Add(BuildFilterBar(), 0, 0);

        _topSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical
        };
        var topSplit = _topSplit!;
        topSplit.Panel1.Controls.Add(BuildPlacesPanel());
        topSplit.Panel2.Controls.Add(BuildSessionsPanel());
        root.Controls.Add(topSplit, 0, 1);

        var bottomTabs = new TabControl { Dock = DockStyle.Fill };
        var paymentsTab = new TabPage("Оплати");
        var finesTab = new TabPage("Штрафи");

        paymentsTab.Controls.Add(BuildGridHost(_paymentsGrid, "Журнал оплат обраної сесії"));
        finesTab.Controls.Add(BuildGridHost(_finesGrid, "Журнал штрафів обраної сесії"));

        bottomTabs.TabPages.Add(paymentsTab);
        bottomTabs.TabPages.Add(finesTab);

        root.Controls.Add(bottomTabs, 0, 2);

        ConfigurePlacesGrid();
        ConfigureSessionsGrid();
        ConfigurePaymentsGrid();
        ConfigureFinesGrid();

        return root;
    }

    private Control BuildFilterBar()
    {
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 76,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(247, 250, 255),
            Margin = new Padding(0, 0, 0, 8)
        };
        card.Paint += PaintCardBorder;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 2
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label { Text = "Пошук (номер)", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(new Label { Text = "Фільтр статусу", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);

        _sessionSearchTextBox.Dock = DockStyle.Fill;
        _sessionSearchTextBox.PlaceholderText = "Введіть номер авто";
        _sessionSearchTextBox.TextChanged += (_, _) => FillSessions();
        layout.Controls.Add(_sessionSearchTextBox, 1, 1);

        _sessionStatusFilterCombo.Dock = DockStyle.Fill;
        _sessionStatusFilterCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _sessionStatusFilterCombo.SelectedIndexChanged += (_, _) => FillSessions();
        layout.Controls.Add(_sessionStatusFilterCombo, 3, 1);

        _onlyDebtCheckBox.Text = "Лише з боргом";
        _onlyDebtCheckBox.AutoSize = true;
        _onlyDebtCheckBox.CheckedChanged += (_, _) => FillSessions();
        layout.Controls.Add(_onlyDebtCheckBox, 4, 1);

        layout.Controls.Add(MakeButton("Скинути фільтр", (_, _) => ResetSessionFilters(), 120), 5, 1);
        layout.Controls.Add(MakeButton("Завершити (з таблиці)", (_, _) => EndSelectedSession(), 120), 6, 1);
        layout.Controls.Add(MakePrimaryButton("Оплатити", (_, _) => PaySelectedSession(), 110), 7, 1);

        card.Controls.Add(layout);
        return card;
    }

    private Control BuildPlacesPanel()
    {
        return BuildGridHost(_placesGrid, "Паркувальні місця");
    }

    private Control BuildSessionsPanel()
    {
        return BuildGridHost(_sessionsGrid, "Сесії паркування");
    }

    private Control BuildGridHost(DataGridView grid, string title)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0) };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            Padding = new Padding(10, 8, 10, 6),
            BackColor = Color.FromArgb(245, 247, 250)
        };
        header.Paint += PaintCardBorder;

        header.Controls.Add(new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold)
        });

        var body = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 6, 0, 0)
        };

        body.Controls.Add(grid);

        panel.Controls.Add(body);
        panel.Controls.Add(header);

        return panel;
    }

    private static void PrepareStatLabel(Label label)
    {
        label.Dock = DockStyle.Fill;
        label.AutoSize = true;
    }

    private GroupBox CreateGroup(string title, int height)
    {
        return new GroupBox
        {
            Text = title,
            Width = 360,
            Height = height,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(10)
        };
    }

    private static TableLayoutPanel CreateGroupLayout(Control parent, int columns)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = columns,
            RowCount = 8,
            AutoSize = false
        };

        if (columns == 2)
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        }
        else
        {
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        }

        for (var i = 0; i < 8; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        parent.Controls.Add(layout);
        return layout;
    }

    private Button MakeButton(string text, EventHandler onClick, int width = 140)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 34,
            Margin = new Padding(0, 0, 6, 6),
            FlatStyle = FlatStyle.Flat
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 214);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(246, 248, 252);
        button.Click += onClick;
        return button;
    }

    private Button MakePrimaryButton(string text, EventHandler onClick, int width = 140)
    {
        var button = MakeButton(text, onClick, width);
        button.BackColor = Color.FromArgb(48, 107, 255);
        button.ForeColor = Color.White;
        button.FlatAppearance.BorderColor = Color.FromArgb(48, 107, 255);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, 95, 238);
        return button;
    }

    private void PaintCardBorder(object? sender, PaintEventArgs e)
    {
        if (sender is not Control c)
        {
            return;
        }

        var rect = c.ClientRectangle;
        rect.Width -= 1;
        rect.Height -= 1;
        using var pen = new Pen(Color.FromArgb(220, 225, 232));
        e.Graphics.DrawRectangle(pen, rect);
    }

    private void ConfigurePlacesGrid()
    {
        ApplyGridBaseStyle(_placesGrid);
        _placesGrid.DataSource = _placeRows;
        _placesGrid.CellClick += (_, _) => SyncPlaceSelectionFromGrid();
        _placesGrid.CellDoubleClick += (_, _) => SelectActiveSessionByCurrentPlace();
        _placesGrid.DataBindingComplete += (_, _) => ConfigurePlacesColumns();
        _placesGrid.RowPrePaint += PlacesGrid_RowPrePaint;
    }

    private void ConfigureSessionsGrid()
    {
        ApplyGridBaseStyle(_sessionsGrid);
        _sessionsGrid.DataSource = _sessionRows;
        _sessionsGrid.SelectionChanged += (_, _) =>
        {
            _lastSelectedSessionId = GetSelectedSession()?.Id;
            RefreshSelectedSessionDetails();
        };
        _sessionsGrid.CellDoubleClick += (_, _) => EndSelectedSession();
        _sessionsGrid.DataBindingComplete += (_, _) => ConfigureSessionsColumns();
        _sessionsGrid.RowPrePaint += SessionsGrid_RowPrePaint;
    }

    private void ConfigurePaymentsGrid()
    {
        ApplyGridBaseStyle(_paymentsGrid);
        _paymentsGrid.DataSource = _paymentRows;
        _paymentsGrid.DataBindingComplete += (_, _) => ConfigurePaymentsColumns();
    }

    private void ConfigureFinesGrid()
    {
        ApplyGridBaseStyle(_finesGrid);
        _finesGrid.DataSource = _fineRows;
        _finesGrid.DataBindingComplete += (_, _) => ConfigureFinesColumns();
        _finesGrid.RowPrePaint += FinesGrid_RowPrePaint;
    }

    private static void ApplyGridBaseStyle(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoGenerateColumns = true;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowHeadersVisible = false;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(244, 246, 249);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        grid.ColumnHeadersHeight = 34;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(226, 236, 255);
        grid.DefaultCellStyle.SelectionForeColor = Color.Black;
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253);
    }

    private void ConfigurePlacesColumns()
    {
        if (_placesGrid.Columns.Count == 0)
        {
            return;
        }

        HideColumnIfExists(_placesGrid, "Id");
        SetHeader(_placesGrid, "Код", "Код місця");
        SetHeader(_placesGrid, "Тип", "Тип");
        SetHeader(_placesGrid, "Статус", "Статус");
    }

    private void ConfigureSessionsColumns()
    {
        if (_sessionsGrid.Columns.Count == 0)
        {
            return;
        }

        HideColumnIfExists(_sessionsGrid, "Id");
        SetHeader(_sessionsGrid, "НомерАвто", "Номер авто");
        SetHeader(_sessionsGrid, "Місце", "Місце");
        SetHeader(_sessionsGrid, "Початок", "Початок");
        SetHeader(_sessionsGrid, "Кінець", "Кінець");
        SetHeader(_sessionsGrid, "Статус", "Статус");
        SetHeader(_sessionsGrid, "Нараховано", "Нараховано, грн");
        SetHeader(_sessionsGrid, "Сплачено", "Сплачено, грн");
        SetHeader(_sessionsGrid, "Борг", "Борг, грн");
        SetHeader(_sessionsGrid, "Штрафи", "К-сть штрафів");

        SetFormat(_sessionsGrid, "Початок", "dd.MM.yyyy HH:mm");
        SetFormat(_sessionsGrid, "Кінець", "dd.MM.yyyy HH:mm");
        SetFormat(_sessionsGrid, "Нараховано", "N2");
        SetFormat(_sessionsGrid, "Сплачено", "N2");
        SetFormat(_sessionsGrid, "Борг", "N2");
    }

    private void ConfigurePaymentsColumns()
    {
        if (_paymentsGrid.Columns.Count == 0)
        {
            return;
        }

        HideColumnIfExists(_paymentsGrid, "Id");
        SetHeader(_paymentsGrid, "Дата", "Дата і час");
        SetHeader(_paymentsGrid, "Сума", "Сума, грн");
        SetHeader(_paymentsGrid, "Метод", "Метод");
        SetFormat(_paymentsGrid, "Дата", "dd.MM.yyyy HH:mm:ss");
        SetFormat(_paymentsGrid, "Сума", "N2");
    }

    private void ConfigureFinesColumns()
    {
        if (_finesGrid.Columns.Count == 0)
        {
            return;
        }

        HideColumnIfExists(_finesGrid, "Id");
        SetHeader(_finesGrid, "Дата", "Дата і час");
        SetHeader(_finesGrid, "Причина", "Причина");
        SetHeader(_finesGrid, "Сума", "Сума, грн");
        SetHeader(_finesGrid, "Статус", "Статус");
        SetFormat(_finesGrid, "Дата", "dd.MM.yyyy HH:mm:ss");
        SetFormat(_finesGrid, "Сума", "N2");
    }

    private static void HideColumnIfExists(DataGridView grid, string name)
    {
        if (grid.Columns.Contains(name))
        {
            grid.Columns[name].Visible = false;
        }
    }

    private static void SetHeader(DataGridView grid, string propertyName, string header)
    {
        if (grid.Columns.Contains(propertyName))
        {
            grid.Columns[propertyName].HeaderText = header;
        }
    }

    private static void SetFormat(DataGridView grid, string propertyName, string format)
    {
        if (grid.Columns.Contains(propertyName))
        {
            grid.Columns[propertyName].DefaultCellStyle.Format = format;
        }
    }

    private void PlacesGrid_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
    {
        if (_placesGrid.Rows[e.RowIndex].DataBoundItem is not ParkingPlaceRow row)
        {
            return;
        }

        if (row.Статус == "Зайняте")
        {
            _placesGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 244, 226);
        }
        else if (row.Статус == "Недоступне")
        {
            _placesGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
        }
        else
        {
            _placesGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor = row.Тип switch
            {
                "Для осіб з інвалідністю" => Color.FromArgb(235, 245, 255),
                "Електро" => Color.FromArgb(236, 255, 240),
                _ => Color.White
            };
        }
    }

    private void SessionsGrid_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
    {
        if (_sessionsGrid.Rows[e.RowIndex].DataBoundItem is not SessionRow row)
        {
            return;
        }

        if (row.Статус == "Активна")
        {
            _sessionsGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(235, 255, 239);
        }
        else if (row.Борг > 0)
        {
            _sessionsGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.FromArgb(255, 246, 230);
        }
        else
        {
            _sessionsGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.White;
        }
    }

    private void FinesGrid_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
    {
        if (_finesGrid.Rows[e.RowIndex].DataBoundItem is not FineRow row)
        {
            return;
        }

        _finesGrid.Rows[e.RowIndex].DefaultCellStyle.BackColor = row.Статус switch
        {
            "Не сплачено" => Color.FromArgb(255, 241, 241),
            "Сплачено" => Color.FromArgb(237, 252, 240),
            _ => Color.White
        };
    }

    private void StartSession()
    {
        try
        {
            if (_placeCombo.SelectedItem is not PlaceComboItem placeItem)
            {
                throw new InvalidOperationException("Оберіть вільне місце.");
            }

            _manager.StartSession(placeItem.Id, _plateTextBox.Text);
            _plateTextBox.Clear();
            _plateTextBox.Focus();
        }
        catch (Exception ex)
        {
            ShowWarning(ex.Message);
        }
    }

    private void EndSelectedSession()
    {
        try
        {
            var session = GetSelectedSession();
            if (session is null)
            {
                throw new InvalidOperationException("Оберіть сесію в таблиці.");
            }

            _manager.EndSession(session.Id);
            _lastSelectedSessionId = session.Id;
            MessageBox.Show("Сесію завершено. Нарахування вартості виконано.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowWarning(ex.Message);
        }
    }

    private void PaySelectedSession()
    {
        try
        {
            var session = GetSelectedSession();
            if (session is null)
            {
                throw new InvalidOperationException("Оберіть сесію в таблиці.");
            }

            if (_paymentAmount.Value <= 0)
            {
                throw new InvalidOperationException("Вкажіть суму оплати більше 0.");
            }

            _manager.PaySession(session.Id, _paymentAmount.Value, (PaymentMethod)_paymentMethodCombo.SelectedItem!);
            _lastSelectedSessionId = session.Id;
            MessageBox.Show("Оплату зафіксовано.", "Оплата", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowWarning(ex.Message);
        }
    }

    private void FillOutstandingToPaymentBox()
    {
        var session = GetSelectedSession();
        if (session is null)
        {
            ShowWarning("Спочатку оберіть сесію в таблиці.");
            return;
        }

        var debt = session.GetOutstandingAmount();
        _paymentAmount.Value = Math.Min(_paymentAmount.Maximum, debt);
    }

    private void IssueFineNoPayment()
    {
        try
        {
            var session = GetSelectedSession();
            if (session is null)
            {
                throw new InvalidOperationException("Оберіть сесію в таблиці.");
            }

            _manager.IssueNoPaymentFine(session.Id);
            _lastSelectedSessionId = session.Id;
            MessageBox.Show("Штраф за несплату додано.", "Штраф", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowWarning(ex.Message);
        }
    }

    private void IssueFineOverstay()
    {
        try
        {
            var session = GetSelectedSession();
            if (session is null)
            {
                throw new InvalidOperationException("Оберіть сесію в таблиці.");
            }

            _manager.IssueOverstayFine(session.Id);
            _lastSelectedSessionId = session.Id;
            MessageBox.Show("Штраф за перевищення часу додано.", "Штраф", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            ShowWarning(ex.Message);
        }
    }

    private void SaveToFile()
    {
        try
        {
            _storage.Save(_dataPath, _manager);
            MessageBox.Show($"Стан збережено у файл:\r\n{_dataPath}", "Збереження", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Помилка збереження", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void TryLoadFromFile(bool silentOnSuccess)
    {
        try
        {
            var snapshot = _storage.Load(_dataPath);
            _manager.LoadSnapshot(snapshot);
            if (!silentOnSuccess)
            {
                MessageBox.Show("Дані успішно завантажено з JSON.", "Завантаження", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            if (!silentOnSuccess)
            {
                ShowWarning(ex.Message);
            }
        }
    }

    private void ResetSessionFilters()
    {
        _sessionSearchTextBox.Clear();
        _sessionStatusFilterCombo.SelectedIndex = 0;
        _onlyDebtCheckBox.Checked = false;
        FillSessions();
    }

    private void RefreshAllBindings()
    {
        FillPlaces();
        FillSessions();
        FillPlaceCombo();
        RefreshSelectedSessionDetails();
        UpdateDashboard();
    }

    private void FillPlaces()
    {
        _placeRows.Clear();

        foreach (var place in _manager.Places.OrderBy(p => p.Id))
        {
            _placeRows.Add(new ParkingPlaceRow
            {
                Id = place.Id,
                Код = place.Code,
                Тип = ToPlaceTypeText(place.Type),
                Статус = ToPlaceStatusText(place.Status)
            });
        }

        ConfigurePlacesColumns();
    }

    private void FillSessions()
    {
        var selectedId = _lastSelectedSessionId;
        _sessionRows.Clear();

        var query = _manager.Sessions.AsEnumerable();

        var search = _sessionSearchTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s => s.VehiclePlate.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (_sessionStatusFilterCombo.SelectedIndex == 1)
        {
            query = query.Where(s => s.Status == SessionStatus.Active);
        }
        else if (_sessionStatusFilterCombo.SelectedIndex == 2)
        {
            query = query.Where(s => s.Status == SessionStatus.Completed);
        }

        if (_onlyDebtCheckBox.Checked)
        {
            query = query.Where(s => s.GetOutstandingAmount() > 0);
        }

        foreach (var s in query.OrderByDescending(x => x.StartTime))
        {
            var place = _manager.Places.FirstOrDefault(p => p.Id == s.ParkingPlaceId);
            _sessionRows.Add(new SessionRow
            {
                Id = s.Id,
                НомерАвто = s.VehiclePlate,
                Місце = place?.Code ?? s.ParkingPlaceId.ToString(),
                Початок = s.StartTime,
                Кінець = s.EndTime,
                Статус = ToSessionStatusText(s.Status),
                Нараховано = s.AccruedAmount,
                Сплачено = s.PaidAmount,
                Борг = s.GetOutstandingAmount(),
                Штрафи = s.Fines.Count
            });
        }

        ConfigureSessionsColumns();
        RestoreSessionSelection(selectedId);
    }

    private void FillPlaceCombo()
    {
        var selectedId = (_placeCombo.SelectedItem as PlaceComboItem)?.Id;

        var freePlaces = _manager.Places
            .Where(p => p.Status == ParkingPlaceStatus.Free)
            .OrderBy(p => p.Id)
            .Select(p => new PlaceComboItem(p.Id, $"{p.Code} • {ToPlaceTypeText(p.Type)}"))
            .ToList();

        _placeCombo.DataSource = null;
        _placeCombo.DataSource = freePlaces;
        _placeCombo.DisplayMember = nameof(PlaceComboItem.Title);
        _placeCombo.ValueMember = nameof(PlaceComboItem.Id);

        if (selectedId.HasValue)
        {
            var item = freePlaces.FirstOrDefault(x => x.Id == selectedId.Value);
            if (item is not null)
            {
                _placeCombo.SelectedItem = item;
            }
        }
    }

    private void RestoreSessionSelection(Guid? preferredId)
    {
        if (_sessionsGrid.Rows.Count == 0)
        {
            return;
        }

        if (preferredId.HasValue)
        {
            foreach (DataGridViewRow row in _sessionsGrid.Rows)
            {
                if (row.DataBoundItem is SessionRow sessionRow && sessionRow.Id == preferredId.Value)
                {
                    row.Selected = true;
                    _sessionsGrid.CurrentCell = row.Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible);
                    return;
                }
            }
        }

        _sessionsGrid.Rows[0].Selected = true;
        _sessionsGrid.CurrentCell = _sessionsGrid.Rows[0].Cells.Cast<DataGridViewCell>().FirstOrDefault(c => c.Visible);
    }

    private void RefreshSelectedSessionDetails()
    {
        _paymentRows.Clear();
        _fineRows.Clear();

        var session = GetSelectedSession();
        if (session is null)
        {
            _selectedSessionTitleLabel.Text = "Сесію не обрано";
            _selectedSessionInfoLabel.Text = "Оберіть запис у таблиці «Сесії паркування».";
            _selectedAmountsLabel.Text = "Нарахування/оплати/штрафи будуть показані тут.";
            _selectedTimeLabel.Text = string.Empty;
            return;
        }

        var place = _manager.Places.FirstOrDefault(p => p.Id == session.ParkingPlaceId);

        _selectedSessionTitleLabel.Text = $"Авто: {session.VehiclePlate}";
        _selectedSessionInfoLabel.Text = $"Місце: {(place?.Code ?? session.ParkingPlaceId.ToString())} | Статус: {ToSessionStatusText(session.Status)} | Штрафів: {session.Fines.Count}";
        _selectedAmountsLabel.Text = $"Нараховано: {session.AccruedAmount:N2} грн | Сплачено: {session.PaidAmount:N2} грн | Борг: {session.GetOutstandingAmount():N2} грн";
        _selectedTimeLabel.Text = $"Початок: {session.StartTime:dd.MM.yyyy HH:mm:ss}" + (session.EndTime.HasValue ? $" | Кінець: {session.EndTime:dd.MM.yyyy HH:mm:ss}" : " | Кінець: —");

        foreach (var payment in session.Payments.OrderByDescending(p => p.PaidAt))
        {
            _paymentRows.Add(new PaymentRow
            {
                Id = payment.Id,
                Дата = payment.PaidAt,
                Сума = payment.Amount,
                Метод = ToPaymentMethodText(payment.Method)
            });
        }

        foreach (var fine in session.Fines.OrderByDescending(f => f.IssuedAt))
        {
            _fineRows.Add(new FineRow
            {
                Id = fine.Id,
                Дата = fine.IssuedAt,
                Причина = fine.Reason,
                Сума = fine.Amount,
                Статус = ToFineStatusText(fine.Status)
            });
        }

        var suggested = session.GetOutstandingAmount();
        if (suggested > 0)
        {
            _paymentAmount.Value = Math.Min(_paymentAmount.Maximum, suggested);
        }

        ConfigurePaymentsColumns();
        ConfigureFinesColumns();
    }

    private ParkingSession? GetSelectedSession()
    {
        if (_sessionsGrid.CurrentRow?.DataBoundItem is not SessionRow row)
        {
            return null;
        }

        return _manager.Sessions.FirstOrDefault(s => s.Id == row.Id);
    }

    private void SyncPlaceSelectionFromGrid()
    {
        if (_placesGrid.CurrentRow?.DataBoundItem is not ParkingPlaceRow row)
        {
            return;
        }

        var items = _placeCombo.DataSource as List<PlaceComboItem>;
        var item = items?.FirstOrDefault(x => x.Id == row.Id);
        if (item is not null)
        {
            _placeCombo.SelectedItem = item;
        }
    }

    private void SelectActiveSessionByCurrentPlace()
    {
        if (_placesGrid.CurrentRow?.DataBoundItem is not ParkingPlaceRow row)
        {
            return;
        }

        var active = _manager.Sessions
            .Where(s => s.ParkingPlaceId == row.Id && s.Status == SessionStatus.Active)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefault();

        if (active is null)
        {
            return;
        }

        _lastSelectedSessionId = active.Id;
        FillSessions();
    }

    private void UpdateDashboard()
    {
        _clockLabel.Text = $"Час системи: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

        var totalPlaces = _manager.Places.Count;
        var free = _manager.Places.Count(p => p.Status == ParkingPlaceStatus.Free);
        var occupied = _manager.Places.Count(p => p.Status == ParkingPlaceStatus.Occupied);

        var active = _manager.Sessions.Count(s => s.Status == SessionStatus.Active);
        var completed = _manager.Sessions.Count(s => s.Status == SessionStatus.Completed);
        var debt = _manager.Sessions.Sum(s => s.GetOutstandingAmount());

        _occupancyLabel.Text = $"Заповнюваність: {_manager.GetOccupancyRate():N2}% (вільно: {free}, зайнято: {occupied}, всього: {totalPlaces})";
        _activeLabel.Text = $"Активні сесії: {active}";
        _completedLabel.Text = $"Завершені сесії: {completed}";
        _debtLabel.Text = $"Сумарна заборгованість: {debt:N2} грн";
        _tariffLabel.Text = $"Тариф: {_manager.Tariff.HourlyRate:N2} грн/год, мінімум {_manager.Tariff.MinimumCharge:N2} грн, штрафи {_manager.Tariff.FineNoPayment:N2}/{_manager.Tariff.FineOverstay:N2} грн";
    }

    private void ShowWarning(string message)
    {
        MessageBox.Show(message, "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static string ToPlaceTypeText(ParkingPlaceType type)
    {
        return type switch
        {
            ParkingPlaceType.Standard => "Звичайне",
            ParkingPlaceType.Disabled => "Для осіб з інвалідністю",
            ParkingPlaceType.Electric => "Електро",
            _ => type.ToString()
        };
    }

    private static string ToPlaceStatusText(ParkingPlaceStatus status)
    {
        return status switch
        {
            ParkingPlaceStatus.Free => "Вільне",
            ParkingPlaceStatus.Occupied => "Зайняте",
            ParkingPlaceStatus.OutOfService => "Недоступне",
            _ => status.ToString()
        };
    }

    private static string ToSessionStatusText(SessionStatus status)
    {
        return status switch
        {
            SessionStatus.Active => "Активна",
            SessionStatus.Completed => "Завершена",
            _ => status.ToString()
        };
    }

    private static string ToPaymentMethodText(PaymentMethod method)
    {
        return method switch
        {
            PaymentMethod.Cash => "Готівка",
            PaymentMethod.Card => "Картка",
            PaymentMethod.MobileApp => "Мобільний застосунок",
            _ => method.ToString()
        };
    }

    private static string ToFineStatusText(FineStatus status)
    {
        return status switch
        {
            FineStatus.Unpaid => "Не сплачено",
            FineStatus.Paid => "Сплачено",
            FineStatus.Canceled => "Скасовано",
            _ => status.ToString()
        };
    }

    private sealed class PlaceComboItem
    {
        public int Id { get; }
        public string Title { get; }

        public PlaceComboItem(int id, string title)
        {
            Id = id;
            Title = title;
        }

        public override string ToString()
        {
            return Title;
        }
    }

    private sealed class ParkingPlaceRow
    {
        public int Id { get; set; }
        public string Код { get; set; } = string.Empty;
        public string Тип { get; set; } = string.Empty;
        public string Статус { get; set; } = string.Empty;
    }

    private sealed class SessionRow
    {
        public Guid Id { get; set; }
        public string НомерАвто { get; set; } = string.Empty;
        public string Місце { get; set; } = string.Empty;
        public DateTime Початок { get; set; }
        public DateTime? Кінець { get; set; }
        public string Статус { get; set; } = string.Empty;
        public decimal Нараховано { get; set; }
        public decimal Сплачено { get; set; }
        public decimal Борг { get; set; }
        public int Штрафи { get; set; }
    }

    private sealed class PaymentRow
    {
        public Guid Id { get; set; }
        public DateTime Дата { get; set; }
        public decimal Сума { get; set; }
        public string Метод { get; set; } = string.Empty;
    }

    private sealed class FineRow
    {
        public Guid Id { get; set; }
        public DateTime Дата { get; set; }
        public string Причина { get; set; } = string.Empty;
        public decimal Сума { get; set; }
        public string Статус { get; set; } = string.Empty;
    }
}
