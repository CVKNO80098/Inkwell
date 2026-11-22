using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Inkwell.App;

public static class Ui
{
    static bool _stylesLoaded;

    public static void ApplyStyles()
    {
        if (_stylesLoaded) return;
        _stylesLoaded = true;
        var xaml = @"
<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">

  <Style TargetType=""ToolTip"">
    <Setter Property=""Background"" Value=""{DynamicResource Ink.SurfaceAlt}""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource Ink.Fg}""/>
    <Setter Property=""BorderBrush"" Value=""{DynamicResource Ink.Border}""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""Padding"" Value=""9,5""/>
    <Setter Property=""FontSize"" Value=""12""/>
    <Setter Property=""FontFamily"" Value=""Segoe UI, Microsoft YaHei UI""/>
    <Setter Property=""HasDropShadow"" Value=""False""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ToolTip"">
          <Border Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}""
                  BorderThickness=""1"" CornerRadius=""6"" Padding=""{TemplateBinding Padding}"">
            <ContentPresenter/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""TextBox"">
    <Setter Property=""Background"" Value=""{DynamicResource Ink.SurfaceAlt}""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource Ink.Fg}""/>
    <Setter Property=""CaretBrush"" Value=""{DynamicResource Ink.Caret}""/>
    <Setter Property=""SelectionBrush"" Value=""{DynamicResource Ink.Selection}""/>
    <Setter Property=""BorderBrush"" Value=""{DynamicResource Ink.Border}""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""Padding"" Value=""8,4""/>
    <Setter Property=""FontSize"" Value=""13""/>
    <Setter Property=""FontFamily"" Value=""Segoe UI, Microsoft YaHei UI""/>
    <Setter Property=""VerticalContentAlignment"" Value=""Center""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""TextBox"">
          <Border x:Name=""bd"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}""
                  BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""6"" Padding=""{TemplateBinding Padding}"">
            <ScrollViewer x:Name=""PART_ContentHost"" Focusable=""False"" HorizontalScrollBarVisibility=""Hidden"" VerticalScrollBarVisibility=""Hidden""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsKeyboardFocusWithin"" Value=""True"">
              <Setter TargetName=""bd"" Property=""BorderBrush"" Value=""{DynamicResource Ink.Accent}""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""Button"" x:Key=""IconButton"">
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource Ink.Fg}""/>
    <Setter Property=""BorderThickness"" Value=""0""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Focusable"" Value=""False""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""bd"" Background=""{TemplateBinding Background}"" CornerRadius=""6"">
            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Background"" Value=""{DynamicResource Ink.Hover}""/>
            </Trigger>
            <Trigger Property=""IsPressed"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Background"" Value=""{DynamicResource Ink.Border}""/>
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""False"">
              <Setter Property=""Opacity"" Value=""0.35""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""Button"" x:Key=""CloseButton"" BasedOn=""{StaticResource IconButton}"">
    <Style.Triggers>
      <Trigger Property=""IsMouseOver"" Value=""True"">
        <Setter Property=""Foreground"" Value=""White""/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <Style TargetType=""Button"" x:Key=""FlatButton"">
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource Ink.Fg}""/>
    <Setter Property=""BorderThickness"" Value=""0""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Focusable"" Value=""False""/>
    <Setter Property=""Padding"" Value=""9,4""/>
    <Setter Property=""FontSize"" Value=""12.5""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""bd"" Background=""{TemplateBinding Background}"" CornerRadius=""6"" Padding=""{TemplateBinding Padding}"">
            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Background"" Value=""{DynamicResource Ink.Hover}""/>
              <Setter Property=""Foreground"" Value=""{DynamicResource Ink.Fg}""/>
            </Trigger>
            <Trigger Property=""IsPressed"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Background"" Value=""{DynamicResource Ink.Border}""/>
            </Trigger>
            <Trigger Property=""IsEnabled"" Value=""False"">
              <Setter Property=""Opacity"" Value=""0.35""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""Button"" x:Key=""AccentButton"">
    <Setter Property=""Background"" Value=""{DynamicResource Ink.Accent}""/>
    <Setter Property=""Foreground"" Value=""White""/>
    <Setter Property=""BorderThickness"" Value=""0""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""FontSize"" Value=""13""/>
    <Setter Property=""Padding"" Value=""20,9""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""bd"" Background=""{TemplateBinding Background}"" CornerRadius=""8"" Padding=""{TemplateBinding Padding}"">
            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Opacity"" Value=""0.88""/>
            </Trigger>
            <Trigger Property=""IsPressed"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Opacity"" Value=""0.75""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""Button"" x:Key=""OutlineButton"">
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource Ink.Fg}""/>
    <Setter Property=""BorderBrush"" Value=""{DynamicResource Ink.BorderStrong}""/>
    <Setter Property=""BorderThickness"" Value=""1""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""FontSize"" Value=""13""/>
    <Setter Property=""Padding"" Value=""18,8""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""bd"" Background=""{TemplateBinding Background}"" BorderBrush=""{TemplateBinding BorderBrush}""
                  BorderThickness=""{TemplateBinding BorderThickness}"" CornerRadius=""8"" Padding=""{TemplateBinding Padding}"">
            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Background"" Value=""{DynamicResource Ink.Hover}""/>
            </Trigger>
            <Trigger Property=""IsPressed"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Background"" Value=""{DynamicResource Ink.Border}""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""Button"" x:Key=""TabClose"">
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource Ink.Muted}""/>
    <Setter Property=""Width"" Value=""18""/>
    <Setter Property=""Height"" Value=""18""/>
    <Setter Property=""FontSize"" Value=""11""/>
    <Setter Property=""Cursor"" Value=""Hand""/>
    <Setter Property=""Focusable"" Value=""False""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""bd"" Background=""Transparent"" CornerRadius=""4"">
            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Background"" Value=""{DynamicResource Ink.Border}""/>
              <Setter Property=""Foreground"" Value=""{DynamicResource Ink.Fg}""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""ScrollBar"">
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""Width"" Value=""11""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""ScrollBar"">
          <Grid Background=""Transparent"">
            <Track x:Name=""PART_Track"" IsDirectionReversed=""True"" ViewportSize=""{TemplateBinding ViewportSize}"">
              <Track.DecreaseRepeatButton>
                <RepeatButton Command=""ScrollBar.PageUpCommand"" Opacity=""0"" Focusable=""False""/>
              </Track.DecreaseRepeatButton>
              <Track.Thumb>
                <Thumb>
                  <Thumb.Template>
                    <ControlTemplate TargetType=""Thumb"">
                      <Border x:Name=""t"" CornerRadius=""4"" Background=""{DynamicResource Ink.BorderStrong}"" Margin=""2,3,2,3""/>
                      <ControlTemplate.Triggers>
                        <Trigger Property=""IsMouseOver"" Value=""True"">
                          <Setter TargetName=""t"" Property=""Background"" Value=""{DynamicResource Ink.Muted}""/>
                        </Trigger>
                      </ControlTemplate.Triggers>
                    </ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command=""ScrollBar.PageDownCommand"" Opacity=""0"" Focusable=""False""/>
              </Track.IncreaseRepeatButton>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""RichTextBox"">
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""BorderThickness"" Value=""0""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource Ink.Fg}""/>
    <Setter Property=""CaretBrush"" Value=""{DynamicResource Ink.Caret}""/>
    <Setter Property=""SelectionBrush"" Value=""{DynamicResource Ink.Selection}""/>
    <Setter Property=""Padding"" Value=""0""/>
    <Setter Property=""FocusVisualStyle"" Value=""{x:Null}""/>
    <Setter Property=""IsUndoEnabled"" Value=""False""/>
  </Style>

  <Style TargetType=""Separator"">
    <Setter Property=""Background"" Value=""{DynamicResource Ink.Border}""/>
    <Setter Property=""Height"" Value=""1""/>
    <Setter Property=""Margin"" Value=""0""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Separator"">
          <Border Background=""{TemplateBinding Background}"" Height=""1""/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType=""Button"" x:Key=""CaptionButton"">
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""Foreground"" Value=""{DynamicResource Ink.Fg}""/>
    <Setter Property=""Width"" Value=""46""/>
    <Setter Property=""Height"" Value=""42""/>
    <Setter Property=""FontSize"" Value=""12""/>
    <Setter Property=""Focusable"" Value=""False""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""bd"" Background=""{TemplateBinding Background}"">
            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Background"" Value=""{DynamicResource Ink.Hover}""/>
            </Trigger>
            <Trigger Property=""IsPressed"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Background"" Value=""{DynamicResource Ink.Border}""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""Button"" x:Key=""CloseCaption"" BasedOn=""{StaticResource CaptionButton}"">
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""Button"">
          <Border x:Name=""bd"" Background=""Transparent"">
            <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property=""IsMouseOver"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Background"" Value=""#E81123""/>
              <Setter Property=""Foreground"" Value=""White""/>
            </Trigger>
            <Trigger Property=""IsPressed"" Value=""True"">
              <Setter TargetName=""bd"" Property=""Background"" Value=""#C50F1F""/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType=""GridSplitter"" x:Key=""PaneSplitter"">
    <Setter Property=""Background"" Value=""Transparent""/>
    <Setter Property=""Width"" Value=""7""/>
    <Setter Property=""HorizontalAlignment"" Value=""Stretch""/>
    <Setter Property=""Template"">
      <Setter.Value>
        <ControlTemplate TargetType=""GridSplitter"">
          <Grid Background=""Transparent"">
            <Border Width=""1"" Background=""{DynamicResource Ink.Border}"" HorizontalAlignment=""Center""/>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";
        var dict = (ResourceDictionary)XamlReader.Parse(xaml);
        foreach (var key in dict.Keys)
        {
            Application.Current.Resources[key] = dict[key];
        }
    }

    public static TextBlock Text(string text, double size, string brushKey = "Ink.Fg", FontWeight? weight = null)
    {
        var t = new TextBlock { Text = text, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
        t.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        if (weight.HasValue) t.FontWeight = weight.Value;
        return t;
    }

    public static Button IconButton(string glyph, string tip, Action onClick, double size = 30, double fontSize = 14)
    {
        var b = new Button
        {
            Content = new TextBlock { Text = glyph, FontSize = fontSize, FontFamily = Fonts.Icons },
            Width = size,
            Height = size,
            Style = (Style)Application.Current.Resources["IconButton"],
            ToolTip = tip
        };
        b.Click += (s, e) => onClick();
        return b;
    }

    public static Button FlatButton(string text, string tip, Action onClick, double size = 12.5)
    {
        var b = new Button
        {
            Content = new TextBlock { Text = text, FontSize = size },
            Style = (Style)Application.Current.Resources["FlatButton"],
            ToolTip = tip
        };
        b.Click += (s, e) => onClick();
        return b;
    }

    public static Border Round(UIElement child, double radius = 8, string bgKey = null, string borderKey = null, double border = 0, double padding = 0)
    {
        var b = new Border
        {
            CornerRadius = new CornerRadius(radius),
            Child = child,
            Padding = new Thickness(padding),
            BorderThickness = new Thickness(border),
            SnapsToDevicePixels = true
        };
        if (bgKey != null) b.SetResourceReference(Border.BackgroundProperty, bgKey);
        if (borderKey != null) b.SetResourceReference(Border.BorderBrushProperty, borderKey);
        return b;
    }

    public static DropShadowEffect Shadow(Theme t, double blur = 18, double depth = 4, double opacity = 0.35)
    {
        return new DropShadowEffect
        {
            Color = t.Shadow,
            BlurRadius = blur,
            ShadowDepth = depth,
            Direction = 270,
            Opacity = opacity,
            RenderingBias = RenderingBias.Performance
        };
    }

    public static Grid Stack(params UIElement[] children)
    {
        var g = new Grid();
        foreach (var c in children) g.Children.Add(c);
        return g;
    }

    public static StackPanel HStack(double spacing, params UIElement[] children)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        for (int i = 0; i < children.Length; i++)
        {
            if (i > 0 && spacing > 0 && children[i] is FrameworkElement fe1) fe1.Margin = new Thickness(spacing, 0, 0, 0);
            sp.Children.Add(children[i]);
        }
        return sp;
    }

    public static StackPanel VStack(double spacing, params UIElement[] children)
    {
        var sp = new StackPanel { Orientation = Orientation.Vertical };
        for (int i = 0; i < children.Length; i++)
        {
            if (i > 0 && spacing > 0 && children[i] is FrameworkElement fe2) fe2.Margin = new Thickness(0, spacing, 0, 0);
            sp.Children.Add(children[i]);
        }
        return sp;
    }

    public static Rectangle VLine(double height = 18)
    {
        var r = new Rectangle { Width = 1, Height = height, VerticalAlignment = VerticalAlignment.Center };
        r.SetResourceReference(Shape.FillProperty, "Ink.Border");
        return r;
    }

    public static ScrollViewer Scroll(UIElement content, bool horizontal = false)
    {
        return new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = horizontal ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Focusable = false
        };
    }
}
