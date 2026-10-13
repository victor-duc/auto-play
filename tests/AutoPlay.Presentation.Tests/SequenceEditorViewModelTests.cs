using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Model;
using AutoPlay.Presentation.ViewModels;
using AutoPlay.TestDoubles;

namespace AutoPlay.Presentation.Tests;

public class SequenceEditorViewModelTests
{
    private readonly InMemoryRepository _repository = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly Profile _profile = new() { Name = "Game" };
    private readonly Screen _screen;

    public SequenceEditorViewModelTests()
    {
        _screen = new Screen
        {
            Name = "Map",
            Locations = [new Location { Name = "A" }, new Location { Name = "B" }, new Location { Name = "C" }],
        };
        _repository.Screens[_profile.Id] = [_screen];
        foreach (var location in _screen.Locations)
        {
            _repository.Templates[(_screen.Id, location.Id)] = TestImages.Solid(8, 8);
        }
    }

    private SequenceEditorViewModel NewEditor(Sequence? existing = null) => new(
        new SequenceService(_repository, _repository),
        new ScreenService(_repository, _repository, _repository, new FakeScreenCapture()),
        _dialogs,
        _profile,
        [_screen],
        existing);

    private static void Add(SequenceEditorViewModel editor, string locationName)
    {
        editor.SelectedLocationChoice = editor.SelectedScreenChoice!.Locations.Single(l => l.Name == locationName);
        editor.AddStepCommand.Execute(null);
    }

    private static string[] Titles(SequenceEditorViewModel editor) => editor.Steps.Select(s => s.Title).ToArray();

    [Fact]
    public void Offers_the_screens_and_their_thumbnails()
    {
        var editor = NewEditor();

        Assert.Same(_screen, editor.SelectedScreenChoice?.Screen);
        Assert.All(editor.SelectedScreenChoice!.Locations, l => Assert.NotNull(l.Thumbnail));
    }

    [Fact]
    public void AddStep_inserts_after_the_selected_step()
    {
        var editor = NewEditor();
        Add(editor, "A");
        Add(editor, "C");
        editor.SelectedStep = editor.Steps[0];

        Add(editor, "B");

        Assert.Equal(["Map / A", "Map / B", "Map / C"], Titles(editor));
        Assert.Equal([1, 2, 3], editor.Steps.Select(s => s.Number));
        Assert.Same(editor.Steps[1], editor.SelectedStep);
    }

    [Fact]
    public void Steps_can_be_moved_duplicated_and_deleted()
    {
        var editor = NewEditor();
        Add(editor, "A");
        Add(editor, "B");

        editor.MoveUpCommand.Execute(null);
        Assert.Equal(["Map / B", "Map / A"], Titles(editor));
        Assert.False(editor.MoveUpCommand.CanExecute(null));

        editor.DuplicateStepCommand.Execute(null);
        Assert.Equal(["Map / B", "Map / B", "Map / A"], Titles(editor));

        editor.DeleteStepCommand.Execute(null);
        Assert.Equal(["Map / B", "Map / A"], Titles(editor));
        Assert.Same(editor.Steps[1], editor.SelectedStep);
    }

    [Fact]
    public void Save_reports_validation_errors()
    {
        var editor = NewEditor();

        editor.SaveCommand.Execute(null);

        Assert.Contains("The sequence name is required.", editor.ErrorMessage);
        Assert.Contains("Add at least one step.", editor.ErrorMessage);
    }

    [Fact]
    public void Save_stores_steps_with_their_overrides()
    {
        var editor = NewEditor();
        bool? closed = null;
        editor.CloseRequested += (_, saved) => closed = saved;
        editor.Name = "Raids";
        editor.RepeatCountText = "0";
        Add(editor, "A");
        var step = editor.Steps[0];
        step.OverrideDelay = true;
        step.DelayMinMs = "100";
        step.DelayMaxMs = "200";
        step.Verify = false;

        editor.SaveCommand.Execute(null);

        Assert.True(closed);
        var saved = Assert.Single(_repository.Sequences[_profile.Id]);
        Assert.Equal(0, saved.RepeatCount);
        var savedStep = Assert.Single(saved.Steps);
        Assert.Equal(new DelayRange(100, 200), savedStep.Delay);
        Assert.False(savedStep.Verify);
    }

    [Fact]
    public void Invalid_step_override_is_reported()
    {
        var editor = NewEditor();
        editor.Name = "Raids";
        Add(editor, "A");
        editor.Steps[0].OverrideDelay = true;
        editor.Steps[0].DelayMinMs = "500";
        editor.Steps[0].DelayMaxMs = "100";

        editor.SaveCommand.Execute(null);

        Assert.StartsWith("The delay of step 1", editor.ErrorMessage);
    }

    [Fact]
    public void Steps_on_deleted_locations_are_flagged()
    {
        var existing = new Sequence
        {
            Name = "Old",
            Steps = [new SequenceStep { ScreenId = _screen.Id, LocationId = Guid.NewGuid() }],
        };

        var editor = NewEditor(existing);

        var step = Assert.Single(editor.Steps);
        Assert.True(step.IsMissing);
        Assert.Equal("Missing screen or location", step.Title);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void Step_summary_describes_the_effective_settings()
    {
        var editor = NewEditor();
        Add(editor, "A");
        var step = editor.Steps[0];

        Assert.Equal("Delay 800–1500 ms (screen default) · verified, timeout 10000 ms (default)", step.Summary);

        step.Verify = false;
        step.OverrideDelay = true;
        step.DelayMinMs = "1";
        step.DelayMaxMs = "2";

        Assert.Equal("Delay 1–2 ms · no verification", step.Summary);
    }
}
