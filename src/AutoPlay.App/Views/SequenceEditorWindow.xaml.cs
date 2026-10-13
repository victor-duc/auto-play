using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AutoPlay.Presentation.ViewModels;

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
        else if (key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
        {
            _viewModel.RunFromStepCommand.Execute(null);
        }
        else if (key == Key.F6 && Keyboard.Modifiers == ModifierKeys.None)
        {
            _viewModel.TestStepCommand.Execute(null);
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

    private Point? _dragStartPoint;
    private StepViewModel? _draggedStep;

    private void OnStepsPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(StepsList);
        _draggedStep = FindStep(e.OriginalSource);
    }

    private void OnStepsPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragStartPoint is not { } start || _draggedStep is not { } step)
        {
            return;
        }

        var offset = e.GetPosition(StepsList) - start;
        if (Math.Abs(offset.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(offset.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragStartPoint = null;
        DragDrop.DoDragDrop(StepsList, new DataObject(typeof(StepViewModel), step), DragDropEffects.Move);
        _draggedStep = null;
    }

    private void OnStepsDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(StepViewModel)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnStepsDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(StepViewModel)) is not StepViewModel step)
        {
            return;
        }

        // Dropped on a step: takes its place. Dropped below the last step: goes to the end.
        var target = FindStep(e.OriginalSource);
        var targetIndex = target is null ? _viewModel.Steps.Count - 1 : _viewModel.Steps.IndexOf(target);
        _viewModel.MoveStep(step, targetIndex);
        e.Handled = true;
    }

    /// <summary>The step displayed by the list item containing <paramref name="source"/>, if any.</summary>
    private StepViewModel? FindStep(object source) =>
        source is DependencyObject element && ItemsControl.ContainerFromElement(StepsList, element) is ListBoxItem item
            ? item.DataContext as StepViewModel
            : null;
}
