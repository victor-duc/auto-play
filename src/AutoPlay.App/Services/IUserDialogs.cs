namespace AutoPlay.App.Services;

/// <summary>Simple message boxes, abstracted so that view models do not depend on WPF windows.</summary>
public interface IUserDialogs
{
    bool Confirm(string message, string title);

    void ShowError(string message);
}
