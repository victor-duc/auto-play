namespace AutoPlay.Presentation.Services;

/// <summary>Simple message boxes, abstracted so that view models do not depend on WPF windows.</summary>
public interface IUserDialogs
{
    bool Confirm(string message, string title);

    void ShowError(string message);

    void ShowInformation(string message);

    /// <summary>Asks where to save a file; returns the chosen path, or null if cancelled.</summary>
    /// <param name="filter">File types, e.g. "Zip archive (*.zip)|*.zip".</param>
    string? AskSaveFilePath(string title, string defaultFileName, string filter);
}
