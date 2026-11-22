using System.Windows;
using BWU.ViewModels;

namespace BWU;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    // Responsive layout: the sidebar collapses to icons on narrow windows.
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => _vm.ShowLabels = ActualWidth >= 1000;
}
