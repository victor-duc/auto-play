using AutoPlay.Domain.Model;
using AutoPlay.Domain.Sequencing;

namespace AutoPlay.Domain.Tests.Sequencing;

public class SequenceValidatorTests
{
    private readonly Location _button = new() { Name = "Button" };
    private readonly Screen _screen;

    public SequenceValidatorTests() => _screen = new Screen { Name = "Map", Locations = [_button] };

    private SequenceStep Step() => new() { ScreenId = _screen.Id, LocationId = _button.Id };

    [Fact]
    public void Valid_sequence_has_no_errors()
    {
        Assert.Empty(SequenceValidator.Validate("Raids", 0, [Step(), Step()], [_screen], ["Other"]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Name_is_required(string name)
    {
        Assert.Contains("The sequence name is required.", SequenceValidator.Validate(name, 1, [Step()], [_screen], []));
    }

    [Fact]
    public void Name_must_be_unique_ignoring_case_and_spaces()
    {
        Assert.Contains(
            "Another sequence is already named 'raids'.",
            SequenceValidator.Validate(" raids ", 1, [Step()], [_screen], ["Raids"]));
    }

    [Fact]
    public void Repeat_count_must_not_be_negative()
    {
        Assert.Contains(
            "The repeat count must be 0 (until stopped) or more.",
            SequenceValidator.Validate("S", -1, [Step()], [_screen], []));
    }

    [Fact]
    public void At_least_one_step_is_required()
    {
        Assert.Contains("Add at least one step.", SequenceValidator.Validate("S", 1, [], [_screen], []));
    }

    [Fact]
    public void Steps_must_refer_to_existing_locations()
    {
        var deletedLocation = new SequenceStep { ScreenId = _screen.Id, LocationId = Guid.NewGuid() };
        var deletedScreen = new SequenceStep { ScreenId = Guid.NewGuid(), LocationId = _button.Id };

        var errors = SequenceValidator.Validate("S", 1, [Step(), deletedLocation, deletedScreen], [_screen], []);

        Assert.Contains("Step 2 refers to a screen or location that no longer exists.", errors);
        Assert.Contains("Step 3 refers to a screen or location that no longer exists.", errors);
    }

    [Fact]
    public void Verification_timeout_must_not_be_negative()
    {
        var step = Step();
        step.VerificationTimeoutMs = -1;

        Assert.Contains(
            "The verification timeout of step 1 must not be negative.",
            SequenceValidator.Validate("S", 1, [step], [_screen], []));
    }

    [Fact]
    public void FindUsages_returns_the_sequences_using_a_screen()
    {
        var user = new Sequence { Name = "Uses it", Steps = [Step()] };
        var other = new Sequence { Name = "Other", Steps = [new SequenceStep { ScreenId = Guid.NewGuid() }] };

        Assert.Equal([user], SequenceValidator.FindUsages([user, other], _screen.Id));
    }
}
