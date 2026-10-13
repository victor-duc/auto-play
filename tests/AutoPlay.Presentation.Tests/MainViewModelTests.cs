using AutoPlay.Application.Ports;
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
    private readonly FakeTemplateMatcher _matcher = new(new MatchResult(default, 0));
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _viewModel = new MainViewModel(
            new ProfileService(_repository),
            new ScreenService(_repository, _repository, _repository, new FakeScreenCapture(), _matcher),
            new SequenceService(_repository, _repository),
            _flows,
            _flows,
            _flows,
            _dialogs,
            _shell,
            new AppPaths("root"),
            captureInbox: new CaptureInboxService(_repository, new FakeScreenCapture(), new FakeClock()),
            executionService: new SequenceExecutionService(_repository, _repository, _repository, runHistory: _repository));
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
    public async Task RecaptureScreen_opens_the_recapture_flow()
    {
        AddProfileWithScreenAndSequence(out var screen, out _);
        _viewModel.SelectedScreen = _viewModel.Screens.Single(s => s.Id == screen.Id);

        await _viewModel.RecaptureScreenCommand.ExecuteAsync(null);

        Assert.Equal(screen.Id, Assert.Single(_flows.Recaptured).Id);
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
    public void ExportProfile_proposes_a_file_name_and_can_be_cancelled()
    {
        _viewModel.NewProfileName = "Hero: Wars";
        _viewModel.CreateProfileCommand.Execute(null);
        var status = _viewModel.StatusMessage;

        _viewModel.ExportProfileCommand.Execute(null);

        Assert.Equal(["AutoPlay - Hero_ Wars.zip"], _dialogs.SaveFileNames);
        Assert.Equal(status, _viewModel.StatusMessage);
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

    [Fact]
    public void QuickCapture_adds_a_pending_capture_to_the_selected_profile()
    {
        _repository.Profiles.Add(new Profile { Name = "Game", LastTargetRegion = new(0, 0, 40, 30) });
        _viewModel.Load();

        Assert.True(_viewModel.QuickCapture());

        Assert.Single(_viewModel.PendingCaptures);
        Assert.True(_viewModel.HasPendingCaptures);
        Assert.StartsWith("Screen captured", _viewModel.StatusMessage);
    }

    [Fact]
    public void QuickCapture_explains_why_it_cannot_capture()
    {
        Assert.False(_viewModel.QuickCapture());
        Assert.Equal("Quick capture: select a profile first.", _viewModel.StatusMessage);

        _repository.Profiles.Add(new Profile { Name = "Game" });
        _viewModel.Load();

        Assert.False(_viewModel.QuickCapture());
        Assert.Contains("target area is not known", _viewModel.StatusMessage);
    }

    [Fact]
    public void Annotating_a_pending_capture_removes_it_once_the_screen_is_saved()
    {
        _repository.Profiles.Add(new Profile { Name = "Game", LastTargetRegion = new(0, 0, 40, 30) });
        _viewModel.Load();
        _viewModel.QuickCapture();
        var capture = _viewModel.PendingCaptures[0];

        _flows.AnnotateResult = false;
        _viewModel.AnnotateCaptureCommand.Execute(capture);
        Assert.Single(_viewModel.PendingCaptures);

        _flows.AnnotateResult = true;
        _viewModel.AnnotateCaptureCommand.Execute(capture);

        Assert.Equal(2, _flows.Annotated.Count);
        Assert.Equal(40, _flows.Annotated[0].Width);
        Assert.Empty(_viewModel.PendingCaptures);
    }

    [Fact]
    public void DiscardCapture_removes_it()
    {
        _repository.Profiles.Add(new Profile { Name = "Game", LastTargetRegion = new(0, 0, 40, 30) });
        _viewModel.Load();
        _viewModel.QuickCapture();

        _viewModel.DiscardCaptureCommand.Execute(_viewModel.PendingCaptures[0]);

        Assert.Empty(_viewModel.PendingCaptures);
        Assert.False(_viewModel.HasPendingCaptures);
    }

    [Fact]
    public void Lists_show_screen_thumbnails_and_the_last_run_of_each_sequence()
    {
        var profile = new Profile { Name = "Game" };
        var screen = new Screen { Name = "Map" };
        var sequence = new Sequence { Name = "Raids" };
        _repository.Profiles.Add(profile);
        _repository.Screens[profile.Id] = [screen];
        _repository.Captures[screen.Id] = TestImages.Solid(960, 540);
        _repository.Sequences[profile.Id] = [sequence];
        _repository.SaveLastRun(profile.Id, new AutoPlay.Application.Execution.RunSummary
        {
            SequenceId = sequence.Id,
            EndedAt = DateTimeOffset.Now,
            Status = AutoPlay.Application.Execution.RunStatus.Completed,
        });

        _viewModel.Load();

        Assert.Equal(96, _viewModel.Screens.Single().Thumbnail!.Width);
        Assert.StartsWith("✔ Completed today", _viewModel.Sequences.Single().LastRunText);
    }
}
