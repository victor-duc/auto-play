using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AutoPlay.App.ViewModels;

namespace AutoPlay.App.Views;

public partial class SequenceEditorWindow : Window
{
    private readonly SequenceEditorViewModel _viewModel;
    private bool _closeConfirmed;

    public SequenceEditorWindow(SequenceEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Loaded += (_, _) =>
        {
            if (string.IsNullOrEmpty(viewModel.Name))
            {
                NameBox.Focus();
            }
            else
            {
                StepsList.Focus();
            }
        };
    }

    private void OnCloseRequested(object? sender, bool saved)
    {
        _closeConfirmed = true;
        DialogResult = saved;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_closeConfirmed && !_viewModel.ConfirmDiscard())
        {
            e.Cancel = true;
        }
    }

    private void OnLocationDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement((ListBox)sender, source) is ListBoxItem)
        {
            _viewModel.AddStepCommand.Execute(null);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var inTextBox = Keyboard.FocusedElement is TextBox;

        if (key == Key.Escape && ScreenComboBox.IsDropDownOpen)
        {
            return; // Let Esc close the drop-down.
        }

        if (key == Key.Escape)
        {
            _viewModel.CancelCommand.Execute(null);
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.Up)
        {
            _viewModel.MoveUpCommand.Execute(null);
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.Down)
        {
            _viewModel.MoveDownCommand.Execute(null);
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && key == Key.D)
        {
            _viewModel.DuplicateStepCommand.Execute(null);
        }
        else if (key == Key.Delete && !inTextBox)
        {
            _viewModel.DeleteStepCommand.Execute(null);
        }
        else
        {
            return;
        }

        e.Handled = true;
    }
}
