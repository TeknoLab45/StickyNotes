using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Input;
using StickyNotes.Core;

namespace StickyNotes.App;

public partial class NoteWindow : Window
{
    private readonly NoteManager _manager;
    private readonly DispatcherTimer _saveTimer;
    private bool _forceClose;
    private bool _restoring;
    private double _expandedHeight = 260;   // GÖRÜNÜR not yüksekliği (gölge payı hariç)
    private bool _applyingCollapse;
    private bool _syncingEditor;   // düzenleyici belgesi kodla yazılırken TextChanged'i yok say

    /// <summary>Her kenarda gölge payı (px). Pencere = görünür not + 2*Pad; kaydedilen konum/boyut görünür dikdörtgendir.</summary>
    public const double Pad = 12;
    private const double StripHeight = 30;
    private const double CollapsedHeight = StripHeight + 2;   // şerit + 1px kenarlık x2 (görünür)
    private const double MinExpandedHeight = 110;             // görünür

    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();

    // Görünür (gölgesiz) dikdörtgen
    public double VisibleLeft => Left + Pad;
    public double VisibleTop => Top + Pad;
    public double VisibleWidth => (ActualWidth > 0 ? ActualWidth : Width) - 2 * Pad;
    public double VisibleHeight => (ActualHeight > 0 ? ActualHeight : Height) - 2 * Pad;

    public NoteViewModel Vm { get; }
    public string NoteId => Vm.Id;
    public bool IsCollapsed => Vm.IsCollapsed;

    /// <summary>Genişletilince dönülecek yükseklik (daraltılmışken de doğru kalır).</summary>
    public double ExpandedHeight => _expandedHeight;

    public NoteWindow(NoteManager manager, NoteViewModel vm)
    {
        _manager = manager;
        Vm = vm;
        InitializeComponent();
        DataContext = vm;
        Title = "Sticky Note";

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Flush(); };

        vm.Changed += ScheduleSave;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NoteViewModel.Color)) ApplyColor();
            if (e.PropertyName == nameof(NoteViewModel.NoteOpacity)) UpdateWindowOpacity();
            if (e.PropertyName == nameof(NoteViewModel.Type)) Dispatcher.BeginInvoke(FocusBody, DispatcherPriority.Input);
        };
        LocationChanged += (_, _) => { if (!_restoring) ScheduleSave(); };
        SizeChanged += (_, _) =>
        {
            if (_restoring) return;
            if (!_applyingCollapse && !Vm.IsCollapsed && VisibleHeight >= MinExpandedHeight) _expandedHeight = VisibleHeight;
            ScheduleSave();
        };
        IsVisibleChanged += (_, _) => { if (!_restoring && IsLoaded) ScheduleSave(); };

        InputBindings.Add(new KeyBinding(new RelayCommand(_manager.NewNote), Key.N, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => _manager.ShowExplorer()), Key.X, ModifierKeys.Alt));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => _manager.ShowExplorer(true)), Key.F, ModifierKeys.Control));

        LoadEditor();
        TextEditor.TextChanged += Editor_TextChanged;
        DataObject.AddPastingHandler(TextEditor, Editor_Pasting);
        RegisterEditorCommands();

        MinHeight = MinExpandedHeight + 2 * Pad;
        ApplyColor();
        UpdateWindowOpacity();
    }

    // ---------- Opaklık ----------

    private void Window_MouseEnterLeave(object sender, MouseEventArgs e) => UpdateWindowOpacity();
    private void Window_ActiveChanged(object? sender, EventArgs e) => UpdateWindowOpacity();

    /// <summary>Fare üzerindeyken ya da not odaktayken %100; aksi halde notun seçili opaklığı.</summary>
    public void UpdateWindowOpacity()
    {
        Opacity = IsMouseOver || IsActive ? 1.0 : NoteOpacity.Normalize(Vm.NoteOpacity);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        UpdateWindowOpacity();
        // Kenara yapışma: sürükleme (WM_MOVING) ve boyutlandırma (WM_SIZING)
        var src = PresentationSource.FromVisual(this) as HwndSource;
        src?.AddHook(SnapHook);
        // Windows'un kendi kenar yapışması (Aero Snap: ekran kenarına sürükleyince yarım/çeyrek ekran) devre dışı:
        // WS_MAXIMIZEBOX olmayan pencere Aero Snap almaz; yapışmayı yalnızca biz yaparız.
        if (src != null)
        {
            const int GWL_STYLE = -16; const long WS_MAXIMIZEBOX = 0x10000;
            var style = GetWindowLongPtr(src.Handle, GWL_STYLE).ToInt64();
            SetWindowLongPtr(src.Handle, GWL_STYLE, new IntPtr(style & ~WS_MAXIMIZEBOX));
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private bool _inMoveLoop, _movingDrag;

    private IntPtr SnapHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        if (msg == NativeSnap.WM_ENTERSIZEMOVE) _inMoveLoop = true;
        else if (msg == NativeSnap.WM_EXITSIZEMOVE)
        {
            _inMoveLoop = false;
            NativeSnap.FixUp(hwnd, scale, Pad);   // döngü bittikten sonra son kez yapıştır/içeri al
        }
        else if (msg == NativeSnap.WM_WINDOWPOSCHANGING && _inMoveLoop && _movingDrag)
        {
            // İşletim sistemi sürükleme sırasında pencerenin üstünü çalışma alanı üstüne sıkıştırıyor (gölge payı yüzünden
            // görünür kenar 12 px aşağıda kalırdı); son sözü burada söyleriz.
            NativeSnap.HandlePos(lParam, scale, Pad);
            return IntPtr.Zero;
        }
        else if (msg == NativeSnap.WM_MOVING) _movingDrag = true;
        else if (msg == NativeSnap.WM_SIZING) _movingDrag = false;
        if (msg != NativeSnap.WM_MOVING && msg != NativeSnap.WM_SIZING) return IntPtr.Zero;
        if (NativeSnap.Handle(msg, wParam, lParam, scale, Pad, MinWidth - 2 * Pad, MinHeight - 2 * Pad))
            handled = true;
        return handled ? (IntPtr)1 : IntPtr.Zero;
    }

    // ---------- Ortak yazı ayarları ----------

    /// <summary>Yazı tipi/boyutu/stili (tüm notlar için ortak). Anında uygulanır.</summary>
    public void ApplySettings(AppSettings s)
    {
        TextElement.SetFontFamily(BodyHost, new FontFamily(s.FontFamily));
        TextElement.SetFontSize(BodyHost, s.FontSize);
        TextElement.SetFontWeight(BodyHost, s.FontStyle == NoteFontStyle.Bold ? FontWeights.Bold : FontWeights.Normal);
        TextElement.SetFontStyle(BodyHost, s.FontStyle == NoteFontStyle.Italic ? FontStyles.Italic : FontStyles.Normal);
    }

    // ---------- Kenar/köşe boyutlandırma (AllowsTransparency + WindowChrome çalışmadığı için) ----------

    private void Resize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || !int.TryParse(fe.Tag?.ToString(), out var dir)) return;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        e.Handled = true;
        ReleaseCapture();
        SendMessage(hwnd, 0x0112 /*WM_SYSCOMMAND*/, (IntPtr)(0xF000 + dir) /*SC_SIZE + WMSZ_*/, IntPtr.Zero);
    }

    private void ApplyColor()
    {
        BodyBorder.Background = NoteTheme.Body(Vm.Color);
        StripBg.Background = NoteTheme.Strip(Vm.Color);
    }

    /// <summary>Kayıtlı konum/boyutu uygular (ekran dışına taşmışsa içeri alır).</summary>
    public void Restore(NoteWindowState? ws, int cascadeIndex, AppSettings settings)
    {
        _restoring = true;
        try
        {
            var area = SystemParameters.WorkArea;
            if (ws != null)
            {
                // Kayıtlı değerler görünür dikdörtgendir; gölge payı eklenir (açılışta kayma olmaz)
                _expandedHeight = Math.Max(MinExpandedHeight, ws.ExpandedHeight > 0 ? ws.ExpandedHeight : ws.Height);
                Width = Math.Max(MinWidth, ws.Width + 2 * Pad);
                Height = _expandedHeight + 2 * Pad;
                Left = ws.X - Pad;
                Top = ws.Y - Pad;
                if (ws.Collapsed) ApplyCollapsed(true);
            }
            else
            {
                // Yeni not: varsayılan boyut (ayarlardan)
                _expandedHeight = Math.Max(MinExpandedHeight, settings.DefaultHeight);
                Width = Math.Max(MinWidth, settings.DefaultWidth + 2 * Pad);
                Height = _expandedHeight + 2 * Pad;
                var off = (cascadeIndex % 10) * 30;
                Left = area.Left + 108 + off;
                Top = area.Top + 108 + off;
            }
            // Çalışma alanı içinde tut + kenara yapış (sürüklemedekiyle aynı yardımcı); kaydedilen değerler görünür dikdörtgendir
            var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
            var vis = NativeSnap.SnapDip(new RectD(Left + Pad, Top + Pad, Width - 2 * Pad, Height - 2 * Pad), scale);
            Left = vis.X - Pad;
            Top = vis.Y - Pad;
            if (Math.Abs(vis.Width - (Width - 2 * Pad)) > 0.5)   // çalışma alanından büyükse küçült
            {
                Width = vis.Width + 2 * Pad;
                Height = vis.Height + 2 * Pad;
            }
        }
        finally { _restoring = false; }
    }

    private void ScheduleSave()
    {
        if (_forceClose) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>Bekleyen değişiklikleri hemen yazar.</summary>
    public void Flush()
    {
        _saveTimer.Stop();
        _manager.SaveNote(Vm, this);
    }

    public void FocusBody()
    {
        if (Vm.IsChecklist)
        {
            if (Vm.Items.Count > 0) FocusItem(Vm.Items[Vm.Items.Count - 1]);
        }
        else
        {
            TextEditor.Focus();
            TextEditor.CaretPosition = TextEditor.Document.ContentEnd;
        }
    }

    // ---------- Metin düzenleyici (RichTextBox) ----------

    /// <summary>VM'deki metin + biçimi düzenleyiciye yazar. VM'ye geri yazmaz (eski notu açmak edited_at'i değiştirmez).</summary>
    private void LoadEditor()
    {
        _syncingEditor = true;
        TextEditor.BeginChange();
        try { RichTextMapper.Write(TextEditor.Document, RichText.NormalizeNewlines(Vm.Text), Vm.Marks); }
        finally
        {
            TextEditor.EndChange();
            _syncingEditor = false;
        }
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingEditor) return;
        var (text, marks) = RichTextMapper.Read(TextEditor.Document);
        Vm.SetRich(text, marks);
    }

    /// <summary>Yalnızca düz metin yapıştırılır (biçim, resim ve dosya yapıştırılmaz).</summary>
    private void Editor_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            var s = RichText.NormalizeNewlines((string)e.DataObject.GetData(DataFormats.UnicodeText));
            e.DataObject = new DataObject(DataFormats.UnicodeText, s);
        }
        else e.CancelCommand();
    }

    /// <summary>Ctrl+B / Ctrl+U bizim biçim mantığımızı çalıştırır; desteklenmeyen RichTextBox komutları engellenir.</summary>
    private void RegisterEditorCommands()
    {
        TextEditor.CommandBindings.Add(new CommandBinding(EditingCommands.ToggleBold,
            (_, e) => { ToggleMark(MarkTypes.Bold); e.Handled = true; }));
        TextEditor.CommandBindings.Add(new CommandBinding(EditingCommands.ToggleUnderline,
            (_, e) => { ToggleMark(MarkTypes.Underline); e.Handled = true; }));

        foreach (var cmd in BlockedEditorCommands)
        {
            TextEditor.CommandBindings.Add(new CommandBinding(cmd,
                (_, e) => e.Handled = true,
                (_, e) => { e.CanExecute = false; e.Handled = true; }));
        }
    }

    private static readonly ICommand[] BlockedEditorCommands =
    {
        EditingCommands.ToggleItalic, EditingCommands.IncreaseFontSize, EditingCommands.DecreaseFontSize,
        EditingCommands.AlignLeft, EditingCommands.AlignCenter, EditingCommands.AlignRight, EditingCommands.AlignJustify,
        EditingCommands.ToggleBullets, EditingCommands.ToggleNumbering,
        EditingCommands.IncreaseIndentation, EditingCommands.DecreaseIndentation,
        EditingCommands.ToggleSubscript, EditingCommands.ToggleSuperscript
    };

    /// <summary>Seçili yazının biçimini açar/kapatır (seçim yoksa hiçbir şey yapmaz).</summary>
    private void ToggleMark(string type)
    {
        if (TextEditor.Selection.IsEmpty) return;
        var doc = TextEditor.Document;
        var (text, marks) = RichTextMapper.Read(doc);
        var start = RichTextMapper.GetOffset(doc, TextEditor.Selection.Start);
        var end = RichTextMapper.GetOffset(doc, TextEditor.Selection.End);
        var updated = RichText.Toggle(marks, type, start, end, text.Length);

        TextEditor.BeginChange();
        _syncingEditor = true;
        try { RichTextMapper.Write(doc, text, updated); }
        finally
        {
            TextEditor.EndChange();
            _syncingEditor = false;
        }
        TextEditor.Selection.Select(RichTextMapper.GetPointer(doc, start), RichTextMapper.GetPointer(doc, end));
        Vm.SetRich(text, updated);
    }

    private void EditorMenu_Opened(object sender, RoutedEventArgs e)
    {
        var hasSelection = !TextEditor.Selection.IsEmpty;
        MenuBold.IsEnabled = hasSelection;
        MenuUnderline.IsEnabled = hasSelection;
        MenuStrike.IsEnabled = hasSelection;
        MenuBold.IsChecked = MenuUnderline.IsChecked = MenuStrike.IsChecked = false;
        if (!hasSelection) return;

        var doc = TextEditor.Document;
        var (text, marks) = RichTextMapper.Read(doc);
        var start = RichTextMapper.GetOffset(doc, TextEditor.Selection.Start);
        var end = RichTextMapper.GetOffset(doc, TextEditor.Selection.End);
        MenuBold.IsChecked = RichText.IsFullyMarked(marks, MarkTypes.Bold, start, end);
        MenuUnderline.IsChecked = RichText.IsFullyMarked(marks, MarkTypes.Underline, start, end);
        MenuStrike.IsChecked = RichText.IsFullyMarked(marks, MarkTypes.Strike, start, end);
    }

    private void MenuBold_Click(object sender, RoutedEventArgs e) => ToggleMark(MarkTypes.Bold);
    private void MenuUnderline_Click(object sender, RoutedEventArgs e) => ToggleMark(MarkTypes.Underline);
    private void MenuStrike_Click(object sender, RoutedEventArgs e) => ToggleMark(MarkTypes.Strike);

    public void CloseForReal()
    {
        _forceClose = true;
        _saveTimer.Stop();
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_forceClose)
        {
            // Alt+F4 vb. = gizle
            e.Cancel = true;
            HideNote();
            return;
        }
        base.OnClosing(e);
    }

    public void HideNote()
    {
        Hide();
        Flush();
        _manager.NotifyChanged();
    }

    private void Strip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Button || e.ButtonState != MouseButtonState.Pressed) return;
        if (e.ClickCount == 2)
        {
            ToggleCollapsed();
            e.Handled = true;
            return;
        }
        try { DragMove(); } catch (InvalidOperationException) { }
    }

    /// <summary>Daralt: yalnızca başlık satırı kalır. Genişlet: eski yüksekliğe döner.</summary>
    public void ToggleCollapsed() => SetCollapsed(!Vm.IsCollapsed);

    public void SetCollapsed(bool collapsed)
    {
        if (collapsed == Vm.IsCollapsed) return;
        if (collapsed && VisibleHeight >= MinExpandedHeight) _expandedHeight = VisibleHeight;
        ApplyCollapsed(collapsed);
        ScheduleSave();
    }

    private void ApplyCollapsed(bool collapsed)
    {
        _applyingCollapse = true;
        try { ApplyCollapsedCore(collapsed); }
        finally { _applyingCollapse = false; }
    }

    private void ApplyCollapsedCore(bool collapsed)
    {
        var target = _expandedHeight;
        Vm.IsCollapsed = collapsed;
        if (collapsed)
        {
            MinHeight = CollapsedHeight + 2 * Pad;
            MaxHeight = CollapsedHeight + 2 * Pad;   // daraltılmışken dikey boyutlanamaz
            Height = CollapsedHeight + 2 * Pad;
            StripBg.CornerRadius = new CornerRadius(5);
        }
        else
        {
            MaxHeight = double.PositiveInfinity;
            MinHeight = MinExpandedHeight + 2 * Pad;
            Height = target + 2 * Pad;
            StripBg.CornerRadius = new CornerRadius(5, 5, 0, 0);
            _expandedHeight = target;
        }
        CollapseGlyph.Text = collapsed ? "" : "";
        CollapseButton.ToolTip = collapsed ? "Genişlet" : "Daralt";
        if (!collapsed) Dispatcher.BeginInvoke(FocusBody, DispatcherPriority.Input);
    }

    private void CollapseButton_Click(object sender, RoutedEventArgs e) => ToggleCollapsed();

    private void NewButton_Click(object sender, RoutedEventArgs e) => _manager.NewNote();

    private void HideButton_Click(object sender, RoutedEventArgs e) => HideNote();

    private void DeleteButton_Click(object sender, RoutedEventArgs e) => _manager.DeleteNote(Vm.Id);

    private void TypeButton_Click(object sender, RoutedEventArgs e)
    {
        Vm.ToggleType();
        if (!Vm.IsChecklist) LoadEditor();   // checklist → metin: düzenleyiciyi yeni içerikle doldur
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = ColorButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var c in NoteColors.All)
        {
            var color = c;
            var item = new MenuItem
            {
                Header = NoteTheme.Label(color),
                IsChecked = Vm.Color == color,
                Icon = new System.Windows.Shapes.Ellipse { Width = 16, Height = 16, Fill = NoteTheme.Strip(color), Stroke = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0)), StrokeThickness = 1 },
                Tag = color
            };
            item.Click += (_, _) => Vm.Color = color;
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        // %100 → %20, 10'ar adım; 9 seviye menüyü uzatacağı için alt menüde
        var opMenu = new MenuItem { Header = "Opaklık  (%" + (int)Math.Round(Vm.NoteOpacity * 100) + ") " };
        AutomationProperties.SetAutomationId(opMenu, "OpacityMenu");
        foreach (var lvl in NoteOpacity.Levels)
        {
            var level = lvl;
            var oi = new MenuItem
            {
                Header = "%" + (int)Math.Round(level * 100),
                IsChecked = Math.Abs(Vm.NoteOpacity - level) < 0.005,
                Tag = level
            };
            oi.Click += (_, _) => Vm.NoteOpacity = level;
            opMenu.Items.Add(oi);
        }
        menu.Items.Add(opMenu);
        menu.Items.Add(new Separator());
        var def = new MenuItem { Header = "Bu notu varsayılan yap" };
        AutomationProperties.SetAutomationId(def, "MakeDefaultMenu");
        def.Click += (_, _) => _manager.MakeDefault(this);
        menu.Items.Add(def);
        menu.IsOpen = true;
    }

    // ---------- Checklist klavye davranışı ----------

    private void ItemText_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not ChecklistItemViewModel item) return;
        var idx = Vm.Items.IndexOf(item);
        if (idx < 0) return;

        switch (e.Key)
        {
            case Key.Enter:
                {
                    // İmleçten sonrasını yeni maddeye taşı
                    var caret = tb.CaretIndex;
                    var rest = tb.Text.Substring(caret);
                    if (rest.Length > 0) item.Text = tb.Text.Substring(0, caret);
                    var n = Vm.InsertItem(idx + 1, rest);
                    FocusItem(n, 0);
                    e.Handled = true;
                    break;
                }
            case Key.Back when tb.Text.Length == 0 && tb.SelectionLength == 0:
                {
                    if (Vm.Items.Count > 1)
                    {
                        Vm.Items.RemoveAt(idx);
                        FocusItem(Vm.Items[Math.Max(0, idx - 1)]);
                    }
                    e.Handled = true;
                    break;
                }
            case Key.Up when idx > 0:
                FocusItem(Vm.Items[idx - 1]);
                e.Handled = true;
                break;
            case Key.Down when idx < Vm.Items.Count - 1:
                FocusItem(Vm.Items[idx + 1], 0);
                e.Handled = true;
                break;
        }
    }

    private void FocusItem(ChecklistItemViewModel item, int? caret = null)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ItemsHost.UpdateLayout();
            if (ItemsHost.ItemContainerGenerator.ContainerFromItem(item) is not DependencyObject cont) return;
            var tb = FindChild<TextBox>(cont);
            if (tb == null) return;
            tb.Focus();
            Keyboard.Focus(tb);
            tb.CaretIndex = caret ?? tb.Text.Length;
        }, DispatcherPriority.Loaded);
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var c = VisualTreeHelper.GetChild(parent, i);
            if (c is T t) return t;
            var r = FindChild<T>(c);
            if (r != null) return r;
        }
        return null;
    }
}
