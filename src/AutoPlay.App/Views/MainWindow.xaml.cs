using System.Windows;
using AutoPlay.Presentation.ViewModels;

namespace AutoPlay.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => _viewModel.Load();
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();
}
