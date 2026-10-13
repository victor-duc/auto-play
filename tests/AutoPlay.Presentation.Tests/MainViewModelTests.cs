using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Model;
using AutoPlay.Presentation.Services;
using AutoPlay.Presentation.ViewModels;
using AutoPlay.TestDoubles;

namespace AutoPlay.Presentation.Tests;

public class MainViewModelTests
{
    private readonly InMemoryRepository _repository = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly FakeShell _shell = new();
    private readonly FakeFlows _flows = new();
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _viewModel = new MainViewModel(
            new ProfileService(_repository),
            new ScreenService(_repository, _repository, _repository, new FakeScreenCapture()),
            new SequenceService(_repository, _repository),
            _flows,
            _flows,
            _flows,
            _dialogs,
            _shell,
            new AppPaths("root"));
    }

    [Fact]
    public void Load_selects_the_first_profile()
    {
        _repository.Profiles.Add(new Profile { Name = "B" });
        _repository.Profiles.Add(new Profile { Name = "A" });

        _viewModel.Load();

        Assert.Equal(2, _viewModel.Profiles.Count);
        Assert.Same(_viewModel.Profiles[0], _viewModel.SelectedProfile);
    }

    [Fact]
    public void CreateProfile_adds_and_selects_it()
    {
        _viewModel.NewProfileName = " Hero Wars ";

        _viewModel.CreateProfileCommand.Execute(null);

        var profile = Assert.Single(_viewModel.Profiles);
        Assert.Equal("Hero Wars", profile.Name);
        Assert.Same(profile, _viewModel.SelectedProfile);
        Assert.Equal(string.Empty, _viewModel.NewProfileName);
    }

    [Fact]
    public void CreateProfile_is_disabled_for_an_empty_or_existing_name()
    {
        _viewModel.NewProfileName = "Game";
        _viewModel.CreateProfileCommand.Execute(null);

        _viewModel.NewProfileName = "  ";
        Assert.False(_viewModel.CreateProfileCommand.CanExecute(null));
        _viewModel.NewProfileName = "game";
        Assert.False(_viewModel.CreateProfileCommand.CanExecute(null));
    }

    [Fact]
    public void DeleteProfile_asks_for_confirmation()
    {
        _viewModel.NewProfileName = "Game";
        _viewModel.CreateProfileCommand.Execute(null);
        _dialogs.ConfirmAnswer = false;

        _viewModel.DeleteProfileCommand.Execute(null);

        Assert.Single(_viewModel.Profiles);
        Assert.Single(_repository.Profiles);

        _dialogs.ConfirmAnswer = true;
        _viewModel.DeleteProfileCommand.Execute(null);

        Assert.Empty(_viewModel.Profiles);
        Assert.Empty(_repository.Profiles);
    }

    [Fact]
    public void DeleteScreen_lists_the_sequences_using_it()
    {
        var profile = AddProfileWithScreenAndSequence(out var screen, out _);
        _viewModel.SelectedScreen = _viewModel.Screens.Single(s => s.Id == screen.Id);

        _viewModel.DeleteScreenCommand.Execute(null);

        Assert.Contains("It is used by: Raids.", Assert.Single(_dialogs.Confirmations));
        Assert.Empty(_repository.Screens[profile.Id]);
    }

    [Fact]
    public void DeleteSequence_lists_the_sequences_running_it()
    {
        var profile = AddProfileWithScreenAndSequence(out _, out var sequence);
        _repository.SaveSequence(profile.Id, new Sequence
        {
            Name = "Daily",
            Steps = [new SequenceStep { Kind = StepKind.RunSequence, SequenceId = sequence.Id }],
        });
        _viewModel.Load();
        _viewModel.SelectedSequence = _viewModel.Sequences.Single(s => s.Id == sequence.Id);
        _dialogs.ConfirmAnswer = false;

        _viewModel.DeleteSequenceCommand.Execute(null);

        Assert.Contains("It is run by: Daily.", Assert.Single(_dialogs.Confirmations));
    }

    [Fact]
    public void DuplicateSequence_selects_the_copy()
    {
        AddProfileWithScreenAndSequence(out _, out var sequence);
        _viewModel.SelectedSequence = _viewModel.Sequences.Single(s => s.Id == sequence.Id);

        _viewModel.DuplicateSequenceCommand.Execute(null);

        Assert.Equal(2, _viewModel.Sequences.Count);
        Assert.Equal("Raids (copy)", _viewModel.SelectedSequence?.Name);
    }

    [Fact]
    public async Task RunSequence_starts_the_execution_flow()
    {
        AddProfileWithScreenAndSequence(out _, out var sequence);
        _viewModel.SelectedSequence = _viewModel.Sequences.Single(s => s.Id == sequence.Id);

        await _viewModel.RunSequenceCommand.ExecuteAsync(null);

        Assert.Equal(sequence.Id, Assert.Single(_flows.Runs).Id);
    }

    [Fact]
    public void Menu_opens_the_logs_and_data_folders()
    {
        _viewModel.OpenLogsFolderCommand.Execute(null);
        _viewModel.OpenDataFolderCommand.Execute(null);

        Assert.Equal([Path.Combine("root", "Logs"), Path.Combine("root", "Profiles")], _shell.Opened);
    }

    private Profile AddProfileWithScreenAndSequence(out Screen screen, out Sequence sequence)
    {
        var profile = new Profile { Name = "Game" };
        var location = new Location { Name = "Button" };
        screen = new Screen { Name = "Map", Locations = [location] };
        sequence = new Sequence { Name = "Raids", Steps = [new SequenceStep { ScreenId = screen.Id, LocationId = location.Id }] };
        _repository.Profiles.Add(profile);
        _repository.Screens[profile.Id] = [screen];
        _repository.Sequences[profile.Id] = [sequence];
        _viewModel.Load();
        return profile;
    }
}
