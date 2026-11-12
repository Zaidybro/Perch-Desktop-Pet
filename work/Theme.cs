using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Perch
{
    internal abstract class Theme
    {
        public const string Ink = "#1C3831";
        public const string Muted = "#4F665E";
        public const string Paper = "#F6F7EF";
        public const string White = "#FFFFFF";
        public const string BorderColor = "#D6E3D8";
        public const string Green = "#255C47";
        public const string Soft = "#E6F1E7";
        public const string Pink = "#FAEBE7";
        public const string Red = "#8D382C";

        private static readonly Dictionary<string, SolidColorBrush> Brushes = new Dictionary<string, SolidColorBrush>();
        private static ControlTemplate buttonTemplate;

        public static SolidColorBrush Brush(string color)
        {
            SolidColorBrush brush;
            if (!Brushes.TryGetValue(color, out brush))
            {
                brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                brush.Freeze();
                Brushes.Add(color, brush);
            }
            return brush;
        }

        public static TextBlock Text(string value, double size = 14, string color = Ink, bool bold = false)
        {
            return new TextBlock
            {
                Text = value, FontSize = size, Foreground = Brush(color),
                FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                TextWrapping = TextWrapping.Wrap
            };
        }

        public static Border Card(UIElement content, string color = White, int padding = 14)
        {
            return new Border
            {
                Background = Brush(color), BorderBrush = Brush(BorderColor), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14), Padding = new Thickness(padding), Child = content
            };
        }

        public static Button Button(string label, System.Action action, string color = White, string foreground = Ink)
        {
            return Button((object)label, action, color, foreground);
        }

        public static Button Button(object content, System.Action action, string color, string foreground)
        {
            if (buttonTemplate == null)
            {
                buttonTemplate = (ControlTemplate)XamlReader.Parse(@"<ControlTemplate
                    xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>
                    <Border x:Name='frame' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'
                      BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}' CornerRadius='9'>
                      <Grid>
                        <Border x:Name='hover' Background='White' Opacity='0' CornerRadius='4' Margin='-4,-2'/>
                        <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'
                          VerticalAlignment='{TemplateBinding VerticalContentAlignment}' RecognizesAccessKey='True'/>
                      </Grid>
                    </Border>
                    <ControlTemplate.Triggers>
                      <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='hover' Property='Opacity' Value='0.10'/></Trigger>
                      <Trigger Property='IsPressed' Value='True'><Setter TargetName='frame' Property='Opacity' Value='0.65'/></Trigger>
                      <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='frame' Property='BorderBrush' Value='#4C9474'/></Trigger>
                      <Trigger Property='IsEnabled' Value='False'><Setter TargetName='frame' Property='Opacity' Value='0.50'/></Trigger>
                    </ControlTemplate.Triggers>
                    </ControlTemplate>");
            }
            var button = new Button
            {
                Content = content, Background = Brush(color), Foreground = Brush(foreground),
                BorderBrush = Brush(color == White ? BorderColor : color),
                BorderThickness = new Thickness(1), Padding = new Thickness(11, 7, 11, 7),
                MinHeight = 34, FontSize = 13, FontWeight = FontWeights.SemiBold,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center, Template = buttonTemplate,
                Margin = new Thickness(0, 0, 6, 6)
            };
            button.Click += delegate { action(); };
            return button;
        }

        public static TextBox Input(string value, string name, double width = double.NaN)
        {
            var input = new TextBox
            {
                Text = value, Width = width, Padding = new Thickness(7, 5, 7, 5), MinHeight = 31,
                FontSize = 14, Foreground = Brush(Ink), BorderBrush = Brush(BorderColor),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            System.Windows.Automation.AutomationProperties.SetName(input, name);
            return input;
        }

        public static void Add(Panel panel, FrameworkElement child, double below = 10)
        {
            child.Margin = new Thickness(child.Margin.Left, child.Margin.Top, child.Margin.Right, below);
            panel.Children.Add(child);
        }

        public static StackPanel Row(params UIElement[] children)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (UIElement child in children)
                row.Children.Add(child);
            return row;
        }

        public static ScrollViewer Scroll(UIElement content)
        {
            return new ScrollViewer
            {
                Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
        }
    }
}
