using System.Windows;
using System.Windows.Controls;

namespace BWU.Utilities;

public static class TextBoxBehavior
{
    public static readonly DependencyProperty AutoScrollProperty =
        DependencyProperty.RegisterAttached("AutoScroll", typeof(bool), typeof(TextBoxBehavior),
            new PropertyMetadata(false, OnAutoScrollChanged));

    public static bool GetAutoScroll(DependencyObject d) => (bool)d.GetValue(AutoScrollProperty);
    public static void SetAutoScroll(DependencyObject d, bool v) => d.SetValue(AutoScrollProperty, v);

    private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;
        tb.TextChanged -= OnTextChanged;
        if ((bool)e.NewValue) tb.TextChanged += OnTextChanged;
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e) => ((TextBox)sender).ScrollToEnd();
}
