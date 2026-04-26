using Avalonia.Controls;
using OpcPlc.Gui.ViewModels;

namespace OpcPlc.Gui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.SaveNodes();
            }
        };
    }
}