using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MyFences.Core;

namespace MyFences.App.Ui;

internal sealed record KindOption(ItemKind? Value, string Label);
internal sealed record GroupOption(Guid Id, string Name);

internal sealed class SettingsWindow : Window
{
    private readonly AppController _controller;
    private readonly AppState _edited;
    private readonly DataGrid _rules;
    private readonly TextBox _color, _hotkey;
    private readonly Slider _opacity;
    private readonly ComboBox _language;
    private readonly List<GroupOption> _targets;

    public SettingsWindow(AppController controller)
    {
        _controller = controller; _edited = controller.State.Clone();
        Title = "MyFences · " + Text.Get("settings");
        Width = Math.Min(860, SystemParameters.WorkArea.Width - 32);
        Height = Math.Min(660, SystemParameters.WorkArea.Height - 32);
        MinWidth = Math.Min(700, Width); MinHeight = Math.Min(520, Height);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new("Segoe UI"); FontSize = 13; Foreground = Theme.Ink;
        Background = Brushes.White;
        var root = new Grid { Margin = new Thickness(28, 24, 28, 20) };
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Content = root;

        var header = new Grid { Margin = new Thickness(0, 0, 0, 22) };
        header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = "MYFENCES", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Theme.Accent, Margin = new Thickness(0, 0, 0, 7) });
        var title = Theme.Label(Text.Get("welcomeTitle"), 24); title.FontWeight = FontWeights.SemiBold; heading.Children.Add(title);
        var subtitle = Theme.Label(controller.Preview ? Text.Get("previewHint") : Text.Get("welcomeBody"), 12, true); subtitle.Margin = new Thickness(0, 8, 18, 0); heading.Children.Add(subtitle);
        header.Children.Add(heading);
        var organize = Theme.Button(Text.Get("organize"), () => controller.Safe(controller.Organize), true);
        organize.VerticalAlignment = VerticalAlignment.Center; organize.Margin = new Thickness(18, 0, 0, 0); Grid.SetColumn(organize, 1); header.Children.Add(organize);
        root.Children.Add(header);

        var tabs = new TabControl { BorderBrush = new SolidColorBrush(Color.FromRgb(225, 230, 238)), Background = Brushes.White };
        Grid.SetRow(tabs, 1); root.Children.Add(tabs);
        var rulesTab = new TabItem { Header = Text.Get("rules") };
        var rulesPanel = new Grid { Margin = new Thickness(16) };
        for (var i = 0; i < 4; i++) rulesPanel.RowDefinitions.Add(new() { Height = i == 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        var hint = Theme.Label(Text.Get("rulesHint"), 12, true); hint.Margin = new Thickness(0, 0, 0, 14); rulesPanel.Children.Add(hint);
        _targets = BuildTargets();
        var kinds = new List<KindOption> { new(null, Text.Get("all")), new(ItemKind.Shortcut, Text.Get("shortcut")), new(ItemKind.Folder, Text.Get("folder")), new(ItemKind.File, Text.Get("file")) };
        _rules = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, CanUserReorderColumns = false, SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow, ItemsSource = _edited.Rules };
        _rules.Columns.Add(new DataGridCheckBoxColumn { Header = Text.Get("enabled"), Binding = new Binding("Enabled"), Width = 55 });
        _rules.Columns.Add(new DataGridTextColumn { Header = Text.Get("ruleName"), Binding = new Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _rules.Columns.Add(new DataGridComboBoxColumn { Header = Text.Get("kind"), ItemsSource = kinds, DisplayMemberPath = "Label", SelectedValuePath = "Value", SelectedValueBinding = new Binding("Kind"), Width = 126 });
        _rules.Columns.Add(new DataGridTextColumn { Header = Text.Get("extensions"), Binding = new Binding("Extensions"), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
        _rules.Columns.Add(new DataGridComboBoxColumn { Header = Text.Get("target"), ItemsSource = _targets, DisplayMemberPath = "Name", SelectedValuePath = "Id", SelectedValueBinding = new Binding("TargetGroupId"), Width = 132 });
        foreach (var column in _rules.Columns.OfType<DataGridTextColumn>())
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(6, 0, 8, 0)));
            if (column.Binding is Binding binding) style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(binding.Path.Path)));
            column.ElementStyle = style;
        }
        Grid.SetRow(_rules, 1); rulesPanel.Children.Add(_rules);
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        tools.Children.Add(Theme.Button(Text.Get("addRule"), AddRule));
        tools.Children.Add(Theme.Button(Text.Get("removeRule"), () => EditSelected((rule, index) => _edited.Rules.Remove(rule))));
        tools.Children.Add(Theme.Button(Text.Get("up"), () => MoveRule(-1)));
        tools.Children.Add(Theme.Button(Text.Get("down"), () => MoveRule(1)));
        Grid.SetRow(tools, 2); rulesPanel.Children.Add(tools);
        var extensionHint = Theme.Label(Text.Get("extensionHint"), 11, true); extensionHint.Margin = new Thickness(0, 12, 0, 0);
        Grid.SetRow(extensionHint, 3); rulesPanel.Children.Add(extensionHint);
        rulesTab.Content = rulesPanel; tabs.Items.Add(rulesTab);

        var appearanceTab = new TabItem { Header = Text.Get("appearance") };
        var appearance = new StackPanel { Margin = new Thickness(24, 20, 24, 24) };
        _language = new ComboBox { ItemsSource = new[] { "简体中文", "English" }, SelectedIndex = _edited.Settings.Language.StartsWith("zh") ? 0 : 1, Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
        AddField(appearance, "language", _language);
        var colors = new StackPanel { Orientation = Orientation.Horizontal };
        _color = new TextBox { Text = _edited.Settings.DefaultColor, Width = 104, MaxLength = 7 };
        AutomationProperties.SetName(_color, Text.Get("color"));
        foreach (var hex in new[] { "#253344", "#343842", "#334438", "#453647", "#4B3E30" })
        {
            var swatch = new Button { Width = 30, Height = 30, MinHeight = 30, Padding = new Thickness(0), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)), Margin = new Thickness(0, 0, 8, 0), ToolTip = hex };
            AutomationProperties.SetName(swatch, hex); swatch.Click += (_, _) => _color.Text = hex; colors.Children.Add(swatch);
        }
        colors.Children.Add(_color); AddField(appearance, "color", colors);
        var opacityRow = new StackPanel { Orientation = Orientation.Horizontal };
        _opacity = new Slider { Minimum = .25, Maximum = .95, Value = _edited.Settings.DefaultOpacity, TickFrequency = .05, Width = 260, VerticalAlignment = VerticalAlignment.Center };
        var opacityValue = Theme.Label($"{_opacity.Value:P0}", 12, true); opacityValue.Margin = new Thickness(16, 0, 0, 0);
        _opacity.ValueChanged += (_, _) => opacityValue.Text = $"{_opacity.Value:P0}";
        opacityRow.Children.Add(_opacity); opacityRow.Children.Add(opacityValue); AddField(appearance, "opacity", opacityRow);
        _hotkey = new TextBox { Text = _edited.Settings.Hotkey, Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
        AddField(appearance, "hotkey", _hotkey);
        var keyboardHint = Theme.Label(Text.Get("hotkeyHint"), 11, true); keyboardHint.Margin = new Thickness(0, -8, 0, 20); appearance.Children.Add(keyboardHint);
        appearance.Children.Add(Theme.Label(Text.Get("defaultsOnly"), 12, true));
        var resume = Theme.Button(Text.Get("resumeAll"), () => controller.Safe(() => controller.Resume(controller.State.Items.Where(i => i.Source == AssignmentSource.Manual).Select(i => i.Path).ToArray())));
        resume.HorizontalAlignment = HorizontalAlignment.Left; resume.Margin = new Thickness(0, 18, 0, 0); appearance.Children.Add(resume);
        appearanceTab.Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = appearance }; tabs.Items.Add(appearanceTab);

        var footer = new Grid { Margin = new Thickness(0, 18, 0, 0) };
        footer.ColumnDefinitions.Add(new()); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var meta = Theme.Label(Text.Get("footer"), 11, true); meta.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(meta);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(Theme.Button(Text.Get("close"), Close));
        var apply = Theme.Button(Text.Get("apply"), Apply, true); apply.Margin = new Thickness(0); actions.Children.Add(apply);
        Grid.SetColumn(actions, 1); footer.Children.Add(actions); Grid.SetRow(footer, 2); root.Children.Add(footer);
    }

    private List<GroupOption> BuildTargets() => _controller.State.Groups.Select(g => new GroupOption(g.Id, g.Name))
        .Concat(_edited.Rules.Where(r => r.TargetGroupId != Guid.Empty).Select(r => new GroupOption(r.TargetGroupId, r.TargetName)))
        .DistinctBy(g => g.Id).ToList();
    private static void AddField(StackPanel panel, string key, UIElement control)
    {
        var label = Theme.Label(Text.Get(key)); label.FontWeight = FontWeights.SemiBold; label.Margin = new Thickness(0, 0, 0, 8);
        AutomationProperties.SetName(control, Text.Get(key));
        panel.Children.Add(label); panel.Children.Add(control);
        if (control is FrameworkElement element) element.Margin = new Thickness(0, 0, 0, 18);
    }
    private void CommitCells() { _rules.CommitEdit(DataGridEditingUnit.Cell, true); _rules.CommitEdit(DataGridEditingUnit.Row, true); }
    private void RefreshRules(object? selected = null) { _rules.ItemsSource = null; _rules.ItemsSource = _edited.Rules; _rules.SelectedItem = selected; }
    private void AddRule()
    {
        CommitCells();
        if (_targets.Count == 0) return;
        var target = _targets[0];
        var rule = new ClassificationRule { Name = Text.Language.StartsWith("zh") ? "新规则" : "New rule", Kind = ItemKind.File, TargetGroupId = target.Id, TargetName = target.Name, Extensions = ".pdf" };
        _edited.Rules.Add(rule); RefreshRules(rule);
    }
    private void EditSelected(Action<ClassificationRule, int> action)
    {
        CommitCells(); if (_rules.SelectedItem is not ClassificationRule rule) return;
        action(rule, _edited.Rules.IndexOf(rule)); RefreshRules();
    }
    private void MoveRule(int direction)
    {
        CommitCells(); if (_rules.SelectedItem is not ClassificationRule rule) return;
        var index = _edited.Rules.IndexOf(rule); var next = index + direction;
        if (next < 0 || next >= _edited.Rules.Count) return;
        _edited.Rules.RemoveAt(index); _edited.Rules.Insert(next, rule); RefreshRules(rule);
    }
    private void Apply()
    {
        _controller.Safe(() =>
        {
            CommitCells();
            if (!Regex.IsMatch(_color.Text, "^#[0-9a-fA-F]{6}$")) throw new ArgumentException(Text.Get("color") + ": #RRGGBB");
            Shortcut.Parse(_hotkey.Text);
            _edited.Settings.Language = _language.SelectedIndex == 0 ? "zh-CN" : "en";
            _edited.Settings.DefaultColor = _color.Text.ToUpperInvariant();
            _edited.Settings.DefaultOpacity = _opacity.Value; _edited.Settings.Hotkey = _hotkey.Text.Trim();
            foreach (var rule in _edited.Rules)
            {
                var target = _targets.FirstOrDefault(g => g.Id == rule.TargetGroupId);
                if (rule.Enabled && target is null) throw new ArgumentException(Text.Get("target") + ": " + rule.Name);
                if (target is not null) rule.TargetName = target.Name;
            }
            _controller.ApplySettings(_edited); Close();
            _controller.ShowSettings();
        });
    }
}
