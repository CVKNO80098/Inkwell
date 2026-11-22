using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Inkwell.App;

public sealed class MenuEntry
{
    public string Text = "";
    public string Shortcut = "";
    public string Glyph = "";
    public bool IsSeparator;
    public bool Checked;
    public bool Enabled = true;
    public Action Action;
    public List<MenuEntry> Children;

    public static MenuEntry Item(string text, string shortcut, Action action, string glyph = "")
    {
        return new MenuEntry { Text = text, Shortcut = shortcut, Action = action, Glyph = glyph };
    }

    public static MenuEntry Sub(string text, params MenuEntry[] children)
    {
        var list = new List<MenuEntry>();
        foreach (var c in children) if (c != null) list.Add(c);
        return new MenuEntry { Text = text, Children = list };
    }

    public static MenuEntry Sep()
    {
        return new MenuEntry { IsSeparator = true };
    }

    public static MenuEntry Check(string text, bool isChecked, Action action, string shortcut = "")
    {
        return new MenuEntry { Text = text, Checked = isChecked, Action = action, Shortcut = shortcut };
    }
}

/// <summary>Lightweight theme aware popup menus (replaces the hard to theme WPF ContextMenu).</summary>
public static class MenuPanel
{
    static readonly List<Popup> OpenPopups = new List<Popup>();
    static readonly List<Popup> SubPopups = new List<Popup>();
    static bool _suppressClose;
    static bool _inputHooked;
    static FrameworkElement _anchor;
    static Window _owner;

    public static bool AnyOpen => OpenPopups.Count > 0;

    public static void CloseAll()
    {
        if (OpenPopups.Count == 0 && SubPopups.Count == 0) return;
        _suppressClose = true;
        foreach (var p in OpenPopups.ToArray())
        {
            try { p.IsOpen = false; } catch { }
        }
        OpenPopups.Clear();
        SubPopups.Clear();
        _suppressClose = false;
        _anchor = null;
        if (_owner != null)
        {
            try { _owner.Deactivated -= OnOwnerDeactivated; } catch { }
            _owner = null;
        }
    }

    static void OnOwnerDeactivated(object sender, EventArgs e) => CloseAll();

    static void CloseSubmenus(Popup keep = null)
    {
        if (SubPopups.Count == 0) return;
        _suppressClose = true;
        foreach (var p in SubPopups.ToArray())
        {
            if (ReferenceEquals(p, keep)) continue;
            try { p.IsOpen = false; } catch { }
        }
        SubPopups.RemoveAll(p => !ReferenceEquals(p, keep));
        _suppressClose = false;
    }

    static bool IsWithin(DependencyObject d, DependencyObject ancestor)
    {
        while (d != null)
        {
            if (ReferenceEquals(d, ancestor)) return true;
            if (d is Visual || d is System.Windows.Media.Media3D.Visual3D)
                d = VisualTreeHelper.GetParent(d);
            else
                d = LogicalTreeHelper.GetParent(d);
        }
        return false;
    }

    static bool IsWithinPopup(DependencyObject d)
    {
        foreach (var p in OpenPopups)
        {
            if (p.Child == null) continue;
            if (ReferenceEquals(p.Child, d)) return true;
            if (IsWithin(d, p.Child)) return true;
        }
        return false;
    }

    static void HookInput()
    {
        if (_inputHooked) return;
        _inputHooked = true;
        InputManager.Current.PreProcessInput += (s, e) =>
        {
            if (OpenPopups.Count == 0) return;
            var input = e.StagingItem.Input;
            if (input is KeyEventArgs k && k.IsDown && k.Key == Key.Escape)
            {
                CloseAll();
                k.Handled = true;
                return;
            }
            if (input is MouseButtonEventArgs mb && mb.ChangedButton == MouseButton.Left && mb.ButtonState == MouseButtonState.Pressed)
            {
                var over = Mouse.DirectlyOver as DependencyObject;
                if (over == null) return;
                if (IsWithinPopup(over)) return;
                if (_anchor != null && IsWithin(over, _anchor)) return;
                CloseAll();
            }
        };
    }

    static Popup CreateRoot(Theme theme, IEnumerable<MenuEntry> entries)
    {
        var popup = new Popup
        {
            StaysOpen = true,
            AllowsTransparency = true,
            PopupAnimation = PopupAnimation.Fade
        };
        popup.Child = BuildPanel(theme, entries, popup);
        var self = popup;
        popup.Closed += (s, e) =>
        {
            OpenPopups.Remove(self);
            SubPopups.Remove(self);
            if (!_suppressClose) CloseAll();
        };
        OpenPopups.Add(popup);
        return popup;
    }

    public static void Show(FrameworkElement anchor, Theme theme, params MenuEntry[] entries)
    {
        HookInput();
        if (OpenPopups.Count > 0 && ReferenceEquals(_anchor, anchor))
        {
            CloseAll();
            return;
        }
        CloseAll();
        _anchor = anchor;
        var popup = CreateRoot(theme, entries);
        popup.PlacementTarget = anchor;
        popup.Placement = PlacementMode.Bottom;
        popup.VerticalOffset = 6;
        popup.IsOpen = true;
        _owner = Window.GetWindow(anchor);
        if (_owner != null) _owner.Deactivated += OnOwnerDeactivated;
    }

    public static void ShowAtMouse(Theme theme, params MenuEntry[] entries)
    {
        HookInput();
        CloseAll();
        _anchor = null;
        var popup = CreateRoot(theme, entries);
        popup.Placement = PlacementMode.MousePoint;
        popup.IsOpen = true;
        _owner = Application.Current != null ? Application.Current.MainWindow : null;
        if (_owner != null) _owner.Deactivated += OnOwnerDeactivated;
    }

    static Border BuildPanel(Theme theme, IEnumerable<MenuEntry> entries, Popup ownerPopup)
    {
        var stack = new StackPanel { Width = 238 };
        foreach (var entry in entries)
        {
            if (entry == null) continue;
            if (entry.IsSeparator)
            {
                stack.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(8, 5, 8, 5),
                    Background = theme.BorderBrush
                });
                continue;
            }
            stack.Children.Add(BuildRow(theme, entry, ownerPopup));
        }

        var border = new Border
        {
            Background = theme.SurfaceBrush,
            BorderBrush = theme.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(6),
            Effect = Ui.Shadow(theme, 22, 6, 0.35),
            Child = stack
        };
        return border;
    }

    static FrameworkElement BuildRow(Theme theme, MenuEntry entry, Popup rootPopup)
    {
        var grid = new Grid { Background = Brushes.Transparent };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var glyph = new TextBlock
        {
            Text = entry.Checked ? "✓" : entry.Glyph,
            FontSize = 13,
            FontFamily = entry.Checked ? Fonts.Ui : Fonts.Icons,
            Foreground = entry.Checked ? theme.AccentBrush : theme.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetColumn(glyph, 0);
        grid.Children.Add(glyph);

        var text = new TextBlock
        {
            Text = entry.Text,
            FontSize = 13,
            Foreground = entry.Enabled ? theme.FgBrush : theme.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        var shortcut = new TextBlock
        {
            Text = entry.Shortcut ?? "",
            FontSize = 11,
            Foreground = theme.MutedBrush,
            Margin = new Thickness(18, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(shortcut, 2);
        grid.Children.Add(shortcut);

        var arrow = new TextBlock
        {
            Text = entry.Children != null && entry.Children.Count > 0 ? "›" : "",
            FontSize = 14,
            Foreground = theme.MutedBrush,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(arrow, 3);
        grid.Children.Add(arrow);

        var row = new Border
        {
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(6, 7, 10, 7),
            Margin = new Thickness(0, 1, 0, 1),
            Child = grid,
            Background = Brushes.Transparent,
            Cursor = entry.Enabled ? Cursors.Hand : Cursors.Arrow
        };

        Popup subPopup = null;
        DispatcherTimer closeTimer = null;

        void CancelClose()
        {
            if (closeTimer == null) return;
            closeTimer.Stop();
            closeTimer = null;
        }

        void ScheduleClose()
        {
            CancelClose();
            closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
            closeTimer.Tick += (a, b) =>
            {
                CancelClose();
                if (subPopup == null || !subPopup.IsOpen) return;
                bool overRow = row.IsMouseOver;
                bool overSub = subPopup.Child != null && subPopup.Child.IsMouseOver;
                if (overRow || overSub) return;
                _suppressClose = true;
                try { subPopup.IsOpen = false; } catch { }
                SubPopups.Remove(subPopup);
                OpenPopups.Remove(subPopup);
                subPopup = null;
                _suppressClose = false;
            };
            closeTimer.Start();
        }

        void OpenSub()
        {
            CancelClose();
            CloseSubmenus();
            var sp = new Popup
            {
                PlacementTarget = row,
                Placement = PlacementMode.Right,
                VerticalOffset = -8,
                HorizontalOffset = 4,
                StaysOpen = true,
                AllowsTransparency = true
            };
            sp.Child = BuildPanel(theme, new List<MenuEntry>(entry.Children), sp);
            sp.Child.MouseEnter += (s, e) => CancelClose();
            sp.Child.MouseLeave += (s, e) => { if (!row.IsMouseOver) ScheduleClose(); };
            sp.Closed += (s, e) =>
            {
                OpenPopups.Remove(sp);
                SubPopups.Remove(sp);
                if (ReferenceEquals(subPopup, sp)) subPopup = null;
            };
            subPopup = sp;
            SubPopups.Add(sp);
            OpenPopups.Add(sp);
            sp.IsOpen = true;
        }

        if (entry.Children != null && entry.Children.Count > 0)
        {
            row.MouseEnter += (s, e) =>
            {
                CancelClose();
                if (subPopup == null || !subPopup.IsOpen) OpenSub();
            };
            row.MouseLeave += (s, e) =>
            {
                if (subPopup != null && subPopup.IsOpen) ScheduleClose();
            };
        }

        row.MouseEnter += (s, e) =>
        {
            if (entry.Enabled) row.Background = theme.HoverBrush;
        };
        row.MouseLeave += (s, e) => { row.Background = Brushes.Transparent; };

        row.MouseLeftButtonUp += (s, e) =>
        {
            if (!entry.Enabled) return;
            if (entry.Children != null && entry.Children.Count > 0)
            {
                if (subPopup == null) OpenSub();
                else subPopup.IsOpen = true;
                return;
            }
            var act = entry.Action;
            CloseAll();
            if (act != null) Application.Current.Dispatcher.BeginInvoke(new Action(act));
        };

        return row;
    }
}
