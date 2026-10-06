using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Threading;
using MyFences.App.Interop;
using MyFences.Core;

namespace MyFences.App.Ui;

internal sealed class ItemDrag(string[] paths)
{
    public string Token { get; } = Guid.NewGuid().ToString();
    public string[] Paths { get; } = paths;
    public bool Handled { get; set; }
}
internal sealed record IconItem(string Path)
{
    public string Name => System.IO.Path.GetExtension(Path).ToLowerInvariant() is ".lnk" or ".url" ? System.IO.Path.GetFileNameWithoutExtension(Path) : System.IO.Path.GetFileName(Path);
    public bool Missing => !File.Exists(Path) && !Directory.Exists(Path);
    public ImageSource Image => ShellImages.Get(Path);
    public string Tooltip => Missing ? $"{Name}\n{Text.Get("missing")}\n{Path}" : Path;
}

internal sealed class FenceWindow : Window
{
    private const string InternalFormat = "MyFences.Grouping.Token.v1";
    private readonly AppController _controller;
    private readonly bool _preview;
    private FenceGroup _group;
    private readonly Border _surface;
    private readonly TextBlock _name, _count;
    private readonly Button _collapse;
    private readonly ListBox _items;
    private readonly StackPanel _empty;
    private readonly Grid _content;
    private AppState? _layoutBefore;
    private System.Windows.Point _mouseStart;
    private IconItem? _pressedItem;
    private bool _preserveMulti, _dragging, _cancelled;
    private Interop.Point? _desktopRelease;
    private ListBoxItem? _insertionTarget;
    private int _insertionIndex;
    public bool AllowClose { get; set; }

    public FenceWindow(AppController controller, FenceGroup group, bool preview)
    {
        _controller = controller; _group = group; _preview = preview;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Theme.ExcludeFromSwitcher(this);
        ShowActivated = false; FontFamily = new("Segoe UI"); FontSize = 12;
        MinWidth = 220; MinHeight = 36;
        var layout = new Grid(); layout.RowDefinitions.Add(new() { Height = new GridLength(36) }); layout.RowDefinitions.Add(new());
        _surface = new Border { CornerRadius = new CornerRadius(8), BorderBrush = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)), BorderThickness = new Thickness(1), Child = layout, Margin = new Thickness(1) };
        var outer = new Grid(); outer.Children.Add(_surface); Content = outer;
        var move = Thumb(Cursors.SizeAll);
        move.DragStarted += (_, _) => _layoutBefore = _controller.BeginLayoutEdit();
        move.DragDelta += (_, e) => { _group.X += e.HorizontalChange; _group.Y += e.VerticalChange; AppController.Clamp(_group); ApplyBounds(); };
        move.DragCompleted += (_, _) => FinishLayout(); layout.Children.Add(move);
        var header = new Grid { Margin = new Thickness(13, 0, 4, 0) };
        header.ColumnDefinitions.Add(new()); for (var i = 0; i < 3; i++) header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _name = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, IsHitTestVisible = false };
        header.Children.Add(_name);
        _count = new TextBlock { Foreground = new SolidColorBrush(Color.FromArgb(185, 255, 255, 255)), VerticalAlignment = VerticalAlignment.Center, FontSize = 11, Margin = new Thickness(8, 0, 8, 0), IsHitTestVisible = false }; Grid.SetColumn(_count, 1); header.Children.Add(_count);
        _collapse = GlyphButton("\uE70E", Text.Get("collapse"), () => controller.Safe(() => controller.Collapse(_group.Id))); Grid.SetColumn(_collapse, 2); header.Children.Add(_collapse);
        var more = GlyphButton("\uE712", Text.Get("settings"), ShowGroupMenu); Grid.SetColumn(more, 3); header.Children.Add(more); layout.Children.Add(header);
        _content = new Grid(); Grid.SetRow(_content, 1); layout.Children.Add(_content);
        _items = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0), SelectionMode = SelectionMode.Extended, Padding = new Thickness(7, 4, 7, 10), Foreground = Brushes.White };
        ScrollViewer.SetHorizontalScrollBarVisibility(_items, ScrollBarVisibility.Disabled); ScrollViewer.SetVerticalScrollBarVisibility(_items, ScrollBarVisibility.Auto);
        _items.ItemsPanel = (ItemsPanelTemplate)XamlReader.Parse("<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><WrapPanel /></ItemsPanelTemplate>");
        _items.ItemTemplate = (DataTemplate)XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <StackPanel Width="78" Height="88" Margin="2,1" ToolTip="{Binding Tooltip}">
                <Image Source="{Binding Image}" Width="40" Height="40" Margin="0,7,0,7" />
                <TextBlock Text="{Binding Name}" Foreground="White" FontSize="12" TextAlignment="Center" TextWrapping="Wrap" TextTrimming="CharacterEllipsis" MaxHeight="34" LineHeight="16">
                  <TextBlock.Effect><DropShadowEffect BlurRadius="3" ShadowDepth="1" Opacity="0.7" /></TextBlock.Effect>
                </TextBlock>
              </StackPanel>
              <DataTemplate.Triggers><DataTrigger Binding="{Binding Missing}" Value="True"><Setter Property="Opacity" Value="0.5" /></DataTrigger></DataTemplate.Triggers>
            </DataTemplate>
            """);
        _items.ItemContainerStyle = (Style)XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ListBoxItem">
              <Setter Property="Margin" Value="1" /><Setter Property="Padding" Value="1" /><Setter Property="BorderThickness" Value="1" /><Setter Property="BorderBrush" Value="Transparent" /><Setter Property="Background" Value="Transparent" />
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ListBoxItem"><Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="5"><ContentPresenter /></Border></ControlTemplate></Setter.Value></Setter>
              <Style.Triggers><Trigger Property="IsSelected" Value="True"><Setter Property="Background" Value="#423B83F6" /><Setter Property="BorderBrush" Value="#AA9BC3FF" /></Trigger><Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="#22FFFFFF" /></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter Property="BorderBrush" Value="White" /></Trigger></Style.Triggers>
            </Style>
            """);
        _content.Children.Add(_items);
        _empty = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false, Margin = new Thickness(20) };
        _empty.Children.Add(new TextBlock { Text = Text.Get("empty"), Foreground = Brushes.White, FontSize = 14, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
        _empty.Children.Add(new TextBlock { Text = Text.Get("emptyDetail"), Foreground = new SolidColorBrush(Color.FromArgb(185, 255, 255, 255)), FontSize = 11, Margin = new Thickness(0, 8, 0, 0), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
        _content.Children.Add(_empty);
        AddResizeEdges(outer);
        AllowDrop = true;
        PreviewDragOver += DragOverItems; PreviewDragLeave += (_, _) => ClearInsertion(); PreviewDrop += DropItems;
        _items.PreviewMouseLeftButtonDown += MouseDownItems;
        _items.PreviewMouseMove += MouseMoveItems;
        _items.PreviewMouseLeftButtonUp += (_, _) => { if (_preserveMulti && !_dragging && _pressedItem is not null) { _items.SelectedItems.Clear(); _items.SelectedItem = _pressedItem; } _preserveMulti = false; };
        _items.MouseDoubleClick += (_, e) => { if (Container(e.OriginalSource as DependencyObject)?.DataContext is IconItem item) controller.Safe(() => controller.Open(item.Path)); e.Handled = true; };
        _items.PreviewMouseRightButtonDown += (_, e) => { if (Container(e.OriginalSource as DependencyObject)?.DataContext is IconItem item && !_items.SelectedItems.Contains(item)) { _items.SelectedItems.Clear(); _items.SelectedItem = item; } };
        _items.MouseRightButtonUp += (_, e) => { var paths = SelectedPaths(); if (paths.Length > 0) { controller.Safe(() => ShowItemMenu(paths)); e.Handled = true; } };
        QueryContinueDrag += (_, e) =>
        {
            if (e.EscapePressed) { _cancelled = true; _controller.EndDesktopDrag(); return; }
            if ((e.KeyStates & DragDropKeyStates.LeftMouseButton) != 0 || !Native.GetCursorPos(out var point)) return;
            if (!_controller.IsDesktopDrop(point)) return;
            // Explorer must not receive a filesystem Drop on bare desktop: an external reference
            // is removed from this group only. Other Windows targets retain the standard FileDrop.
            _desktopRelease = point; e.Action = DragAction.Cancel; e.Handled = true;
        };
        PreviewKeyDown += (_, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.A) { _items.SelectAll(); e.Handled = true; }
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { controller.Safe(controller.Undo); e.Handled = true; }
        };
        Closing += (_, e) => { if (!AllowClose) { e.Cancel = true; controller.HideGroups(); } };
        Update(group, []);
    }

    public void Update(FenceGroup group, IReadOnlyList<ItemReference> items)
    {
        _group = group; Title = group.Name + (_preview ? " — Preview" : "");
        _name.Text = group.Name; _count.Text = items.Count.ToString(); _count.ToolTip = $"{items.Count} {Text.Get("count")}";
        var color = (Color)ColorConverter.ConvertFromString(group.Color); color.A = (byte)(Math.Clamp(group.Opacity, .25, .95) * 255); _surface.Background = new SolidColorBrush(color);
        _collapse.Content = group.Collapsed ? "\uE70D" : "\uE70E"; _collapse.ToolTip = Text.Get(group.Collapsed ? "expand" : "collapse");
        AutomationProperties.SetName(_collapse, (string)_collapse.ToolTip);
        _content.Visibility = group.Collapsed ? Visibility.Collapsed : Visibility.Visible;
        var selected = SelectedPaths().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = items.Select(i => new IconItem(i.Path)).ToList(); _items.ItemsSource = rows;
        foreach (var item in rows.Where(i => selected.Contains(i.Path))) _items.SelectedItems.Add(item);
        _empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ((TextBlock)_empty.Children[0]).Text = Text.Get("empty"); ((TextBlock)_empty.Children[1]).Text = Text.Get("emptyDetail");
        ApplyBounds();
    }
    public void ApplyBounds()
    {
        Width = _group.Width; Height = _group.Collapsed ? 36 : _group.Height;
        if (_preview || !IsLoaded) { Left = _group.X; Top = _group.Y; return; }
        var handle = new WindowInteropHelper(this).Handle; var scale = Native.DpiScale(handle);
        var point = Native.DipToScreen(_group.X, _group.Y, handle);
        Native.MapWindowPoints(0, Native.GetParent(handle), ref point, 1);
        Native.SetWindowPos(handle, 0, point.X, point.Y, (int)Math.Round(Width * scale), (int)Math.Round(Height * scale), 0x14);
    }
    internal void Highlight()
    {
        _surface.BorderBrush = Theme.Accent; _surface.BorderThickness = new Thickness(2);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        timer.Tick += (_, _) => { timer.Stop(); _surface.BorderThickness = new Thickness(1); ClearInsertion(); };
        timer.Start();
    }
    private static Thumb Thumb(Cursor cursor) => new() { Cursor = cursor, Background = Brushes.Transparent, Template = (ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Thumb'><Border Background='{TemplateBinding Background}' /></ControlTemplate>") };
    private Button GlyphButton(string glyph, string label, Action action)
    {
        var button = new Button { Content = glyph, FontFamily = new("Segoe Fluent Icons"), FontSize = 11, ToolTip = label, Style = (Style)Application.Current.FindResource("GroupButton") };
        AutomationProperties.SetName(button, label); button.Click += (_, _) => action(); return button;
    }
    private void AddResizeEdges(Grid outer)
    {
        foreach (var (horizontal, vertical) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, -1), (-1, 1), (1, 1) })
        {
            var thumb = Thumb(horizontal == 0 ? Cursors.SizeNS : vertical == 0 ? Cursors.SizeWE : horizontal == vertical ? Cursors.SizeNWSE : Cursors.SizeNESW);
            thumb.Width = horizontal == 0 ? double.NaN : 5; thumb.Height = vertical == 0 ? double.NaN : 5;
            thumb.HorizontalAlignment = horizontal < 0 ? HorizontalAlignment.Left : horizontal > 0 ? HorizontalAlignment.Right : HorizontalAlignment.Stretch;
            thumb.VerticalAlignment = vertical < 0 ? VerticalAlignment.Top : vertical > 0 ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
            thumb.DragStarted += (_, _) => _layoutBefore = _controller.BeginLayoutEdit();
            thumb.DragDelta += (_, e) =>
            {
                if (horizontal < 0) { var delta = Math.Min(e.HorizontalChange, _group.Width - 220); _group.X += delta; _group.Width -= delta; }
                if (horizontal > 0) _group.Width += e.HorizontalChange;
                if (!_group.Collapsed && vertical < 0) { var delta = Math.Min(e.VerticalChange, _group.Height - 130); _group.Y += delta; _group.Height -= delta; }
                if (!_group.Collapsed && vertical > 0) _group.Height += e.VerticalChange;
                AppController.Clamp(_group); ApplyBounds();
            };
            thumb.DragCompleted += (_, _) => FinishLayout(); outer.Children.Add(thumb);
        }
    }
    private void FinishLayout() { if (_layoutBefore is { } before) _controller.Safe(() => _controller.FinishLayoutEdit(before)); _layoutBefore = null; }
    private void ShowGroupMenu()
    {
        var menu = new ContextMenu();
        void Add(string key, Action action) { var item = new MenuItem { Header = Text.Get(key) }; item.Click += (_, _) => _controller.Safe(action); menu.Items.Add(item); }
        Add("rename", () => _controller.Rename(_group.Id)); Add(_group.Collapsed ? "expand" : "collapse", () => _controller.Collapse(_group.Id));
        Add("settings", _controller.ShowSettings); menu.Items.Add(new Separator()); Add("delete", () => _controller.Delete(_group.Id));
        menu.PlacementTarget = this; menu.IsOpen = true;
    }
    private void ShowItemMenu(string[] paths)
    {
        if (paths.All(p => !File.Exists(p) && !Directory.Exists(p)))
        {
            var menu = new ContextMenu(); var remove = new MenuItem { Header = Text.Get("remove") }; remove.Click += (_, _) => _controller.Safe(() => _controller.AssignMissing(paths));
            var resume = new MenuItem { Header = Text.Get("resume") }; resume.Click += (_, _) => _controller.Safe(() => _controller.Resume(paths));
            menu.Items.Add(remove); menu.Items.Add(resume); menu.IsOpen = true;
        }
        else _controller.ItemMenu(this, paths);
    }
    private string[] SelectedPaths() => _items.Items.Cast<IconItem>().Where(_items.SelectedItems.Contains).Select(i => i.Path).ToArray();
    private static ListBoxItem? Container(DependencyObject? node)
    {
        while (node is not null && node is not ListBoxItem) node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        return node as ListBoxItem;
    }
    private void MouseDownItems(object sender, MouseButtonEventArgs e)
    {
        _mouseStart = e.GetPosition(_items); _pressedItem = Container(e.OriginalSource as DependencyObject)?.DataContext as IconItem;
        _preserveMulti = _pressedItem is not null && _items.SelectedItems.Contains(_pressedItem) && _items.SelectedItems.Count > 1 && Keyboard.Modifiers == ModifierKeys.None && e.ClickCount == 1;
        if (_preserveMulti) e.Handled = true;
    }
    private void MouseMoveItems(object sender, MouseEventArgs e)
    {
        if (_dragging || e.LeftButton != MouseButtonState.Pressed || _pressedItem is null) return;
        var current = e.GetPosition(_items);
        if (Math.Abs(current.X - _mouseStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(current.Y - _mouseStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var paths = SelectedPaths().Where(p => File.Exists(p) || Directory.Exists(p)).ToArray(); if (paths.Length == 0) return;
        var drag = new ItemDrag(paths); _controller.ActiveDrag = drag; _dragging = true; _cancelled = false; _desktopRelease = null;
        try
        {
            _controller.BeginDesktopDrag(paths);
            var data = new DataObject(); data.SetData(DataFormats.FileDrop, paths); data.SetData(InternalFormat, drag.Token, false);
            var effect = DragDrop.DoDragDrop(this, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
            // Remove the click-through preview before validating the final real desktop target.
            _controller.EndDesktopDrag();
            if (!drag.Handled && !_cancelled)
            {
                if (_desktopRelease is { } point) _controller.Safe(() => _controller.DropReferencesOnDesktop(paths, point));
                if ((effect & DragDropEffects.Move) != 0) _controller.Safe(() => _controller.HandleExternalMove(paths));
            }
        }
        finally { _controller.EndDesktopDrag(); _controller.ActiveDrag = null; _dragging = false; _preserveMulti = false; _pressedItem = null; ClearInsertion(); }
    }
    private ItemDrag? InternalDrag(IDataObject data) => data.GetDataPresent(InternalFormat) && data.GetData(InternalFormat) is string token && _controller.ActiveDrag?.Token == token ? _controller.ActiveDrag : null;
    private DragDropEffects DropEffect(IDataObject data, DragDropEffects allowed) =>
        InternalDrag(data) is not null ? DragDropEffects.Move :
        !data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.None :
        (allowed & DragDropEffects.Link) != 0 ? DragDropEffects.Link :
        (allowed & DragDropEffects.Copy) != 0 ? DragDropEffects.Copy : DragDropEffects.None;
    private void DragOverItems(object sender, DragEventArgs e)
    {
        var internalDrag = InternalDrag(e.Data);
        e.Effects = DropEffect(e.Data, e.AllowedEffects);
        e.Handled = true; ClearInsertion(); if (e.Effects == DragDropEffects.None) return;
        _surface.BorderBrush = Theme.Accent;
        var container = Container(e.OriginalSource as DependencyObject);
        _insertionIndex = _items.Items.Count;
        if (container is not null)
        {
            _insertionIndex = _items.ItemContainerGenerator.IndexFromContainer(container);
            var after = e.GetPosition(container).X > container.ActualWidth / 2;
            if (after) _insertionIndex++;
            _insertionTarget = container; container.BorderBrush = Brushes.White; container.BorderThickness = after ? new Thickness(0, 0, 2, 0) : new Thickness(2, 0, 0, 0);
        }
    }
    private void DropItems(object sender, DragEventArgs e)
    {
        e.Effects = DropEffect(e.Data, e.AllowedEffects);
        var drag = InternalDrag(e.Data);
        var paths = drag?.Paths ?? e.Data.GetData(DataFormats.FileDrop) as string[];
        if (paths is null || e.Effects == DragDropEffects.None) { ClearInsertion(); return; }
        var index = _insertionIndex;
        if (drag is not null)
        {
            var moved = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            index -= _items.Items.Cast<IconItem>().Take(index).Count(i => moved.Contains(i.Path));
            drag.Handled = true;
        }
        _controller.Safe(() => _controller.Assign(paths, _group.Id, index)); e.Handled = true; ClearInsertion();
    }
    private void ClearInsertion()
    {
        _surface.BorderBrush = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255));
        if (_insertionTarget is not null) { _insertionTarget.ClearValue(Control.BorderBrushProperty); _insertionTarget.ClearValue(Control.BorderThicknessProperty); _insertionTarget = null; }
    }
}
