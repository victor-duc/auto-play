using AutoPlay.Presentation.ViewModels;
using AutoPlay.App.Views;
using AutoPlay.Presentation.Services;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Model;

namespace AutoPlay.App.Services;

/// <summary>Opens the sequence editor.</summary>
public sealed class SequenceEditingFlow(
    SequenceService sequenceService,
    ScreenService screenService,
    IUserDialogs dialogs,
    ActivityTracker activity,
    ISequenceExecutionFlow execution,
    ILocationTestFlow tester) : ISequenceEditingFlow
{
    /// <summary>Creates (when <paramref name="sequence"/> is null) or edits a sequence. Returns true if it was saved.</summary>
    public bool Edit(Profile profile, Sequence? sequence)
    {
        using var busy = activity.Begin();
        var viewModel = new SequenceEditorViewModel(
            sequenceService, screenService, dialogs, profile, screenService.GetScreens(profile.Id), sequence, execution, tester);
        var editor = new SequenceEditorWindow(viewModel) { Owner = System.Windows.Application.Current.MainWindow };
        return editor.ShowDialog() == true;
    }
}
