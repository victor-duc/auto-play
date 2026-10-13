using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Geometry;
using AutoPlay.Domain.Model;
using AutoPlay.Domain.Recording;
using AutoPlay.Presentation.ViewModels;
using AutoPlay.TestDoubles;

namespace AutoPlay.Presentation.Tests;

public class ScreenEditorViewModelTests
{
    private readonly Guid _profileId = Guid.NewGuid();
    private readonly InMemoryRepository _repository = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly ScreenService _service;

    public ScreenEditorViewModelTests() =>
        _service = new ScreenService(_repository, _repository, _repository, new FakeScreenCapture());

    private ScreenEditorViewModel NewEditor(Screen? existing = null) =>
        new(_service, _dialogs, _profileId, TestImages.Gradient(200, 100), existing);

    [Fact]
    public void AddLocation_names_and_selects_the_new_location()
    {
        var editor = NewEditor();

        var first = editor.AddLocation(new PixelRect(10, 10, 20, 20));
        var second = editor.AddLocation(new PixelRect(50, 10, 20, 20));

        Assert.Equal("Location 1", first.Name);
        Assert.Equal("Location 2", second.Name);
        Assert.Same(second, editor.SelectedLocation);
        Assert.True(second.IsSelected);
        Assert.False(first.IsSelected);
        Assert.Equal(new PixelPoint(60, 20), second.ClickPoint);
    }

    [Fact]
    public void Save_reports_validation_errors()
    {
        var editor = NewEditor();
        editor.AddLocation(new PixelRect(10, 10, 20, 20));

        editor.SaveCommand.Execute(null);

        Assert.Equal("The screen name is required.", editor.ErrorMessage);
    }

    [Theory]
    [InlineData("abc", "500", "The delays must be whole numbers of milliseconds.")]
    [InlineData("600", "500", "The delays must be positive, and the maximum must not be lower than the minimum.")]
    public void Save_reports_invalid_delays(string min, string max, string expected)
    {
        var editor = NewEditor();
        editor.ScreenName = "Map";
        editor.DelayMinMs = min;
        editor.DelayMaxMs = max;

        editor.SaveCommand.Execute(null);

        Assert.Equal(expected, editor.ErrorMessage);
    }

    [Fact]
    public void Save_stores_the_screen_and_closes()
    {
        var editor = NewEditor();
        bool? closed = null;
        editor.CloseRequested += (_, saved) => closed = saved;
        editor.ScreenName = "Map";
        editor.AddLocation(new PixelRect(10, 10, 20, 20)).MatchThresholdText = "0,85";

        editor.SaveCommand.Execute(null);

        Assert.True(closed);
        var screen = Assert.Single(_repository.Screens[_profileId]);
        Assert.Equal(0.85, Assert.Single(screen.Locations).MatchThreshold);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void Invalid_threshold_is_reported()
    {
        var editor = NewEditor();
        editor.ScreenName = "Map";
        editor.AddLocation(new PixelRect(10, 10, 20, 20)).MatchThresholdText = "high";

        editor.SaveCommand.Execute(null);

        Assert.Equal("The match threshold of 'Location 1' is not a number.", editor.ErrorMessage);
    }

    [Fact]
    public void Existing_screen_is_loaded_and_not_dirty()
    {
        var capture = TestImages.Gradient(200, 100);
        var (screen, _) = ScreenBuilder.Build(Guid.NewGuid(), "Map", new DelayRange(1, 2), capture,
            [new LocationDraft { Name = "A", Bounds = new PixelRect(10, 10, 20, 20), ClickPoint = new PixelPoint(15, 15) }]);

        var editor = NewEditor(screen);

        Assert.Equal("Map", editor.ScreenName);
        Assert.Equal("A", Assert.Single(editor.Locations).Name);
        Assert.False(editor.IsDirty);
        Assert.True(editor.ConfirmDiscard());
        Assert.Empty(_dialogs.Confirmations);
    }

    [Fact]
    public void Cancel_with_changes_asks_for_confirmation()
    {
        var editor = NewEditor();
        bool? closed = null;
        editor.CloseRequested += (_, saved) => closed = saved;
        editor.AddLocation(new PixelRect(10, 10, 20, 20));
        _dialogs.ConfirmAnswer = false;

        editor.CancelCommand.Execute(null);

        Assert.Null(closed);
        Assert.Single(_dialogs.Confirmations);
    }

    [Fact]
    public void DeleteLocation_removes_the_selected_location()
    {
        var editor = NewEditor();
        editor.AddLocation(new PixelRect(10, 10, 20, 20));

        editor.DeleteLocationCommand.Execute(null);

        Assert.Empty(editor.Locations);
        Assert.Null(editor.SelectedLocation);
    }

    [Fact]
    public void Overlay_sizes_follow_the_display_scale()
    {
        var editor = NewEditor();

        editor.DisplayScale = 0.5;

        Assert.Equal(4, editor.StrokeThickness);
        Assert.Equal(2, editor.MarkerScale);
        Assert.Equal(12, editor.HandleTolerance);
    }
}
