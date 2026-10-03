using System.Windows;
using AutoPlay.App.ViewModels;
using AutoPlay.App.Views;
using AutoPlay.Core.Model;
using AutoPlay.Core.Storage;

namespace AutoPlay.App.Services;

/// <summary>Opens the sequence editor.</summary>
public sealed class SequenceEditingFlow(IProfileStore store, IUserDialogs dialogs)
{
    /// <summary>Creates (when <paramref name="sequence"/> is null) or edits a sequence. Returns true if it was saved.</summary>
    public bool Edit(Profile profile, IReadOnlyList<Screen> screens, Sequence? sequence, IReadOnlyList<string> otherSequenceNames)
    {
        var viewModel = new SequenceEditorViewModel(store, dialogs, profile, screens, sequence, otherSequenceNames);
        var editor = new SequenceEditorWindow(viewModel) { Owner = Application.Current.MainWindow };
        return editor.ShowDialog() == true;
    }
}
