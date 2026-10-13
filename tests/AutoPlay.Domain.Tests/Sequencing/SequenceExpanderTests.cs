using AutoPlay.Domain.Model;
using AutoPlay.Domain.Sequencing;

namespace AutoPlay.Domain.Tests.Sequencing;

public class SequenceExpanderTests
{
    private static SequenceStep Click(int marker) =>
        new() { ScreenId = Guid.NewGuid(), LocationId = Guid.NewGuid(), VerificationTimeoutMs = marker };

    private static SequenceStep Run(Sequence target) => new() { Kind = StepKind.RunSequence, SequenceId = target.Id };

    private static int?[] Markers(Sequence sequence) => sequence.Steps.Select(s => s.VerificationTimeoutMs).ToArray();

    [Fact]
    public void Sequences_without_chaining_are_copied()
    {
        var sequence = new Sequence { Name = "A", RepeatCount = 3, Steps = [Click(1), SequenceStep.Wait(new DelayRange(1, 2))] };

        var (expanded, errors) = SequenceExpander.Expand(sequence, [sequence]);

        Assert.Empty(errors);
        Assert.Equal(sequence.Id, expanded!.Id);
        Assert.Equal(3, expanded.RepeatCount);
        Assert.Equal(2, expanded.Steps.Count);
        Assert.NotSame(sequence.Steps[0], expanded.Steps[0]);
    }

    [Fact]
    public void Run_steps_are_replaced_by_the_target_steps_repeated()
    {
        var inner = new Sequence { Name = "Inner", RepeatCount = 2, Steps = [Click(2), Click(3)] };
        var middle = new Sequence { Name = "Middle", RepeatCount = 1, Steps = [Run(inner), Click(4)] };
        var root = new Sequence { Name = "Root", Steps = [Click(1), Run(middle), Click(5)] };

        var (expanded, errors) = SequenceExpander.Expand(root, [root, middle, inner]);

        Assert.Empty(errors);
        Assert.Equal([1, 2, 3, 2, 3, 4, 5], Markers(expanded!));
        Assert.DoesNotContain(expanded!.Steps, s => s.Kind == StepKind.RunSequence);
    }

    [Fact]
    public void A_sequence_cannot_run_itself()
    {
        var root = new Sequence { Name = "Root" };
        root.Steps.Add(Run(root));

        var (expanded, errors) = SequenceExpander.Expand(root, [root]);

        Assert.Null(expanded);
        Assert.Equal(["Step 1 of 'Root' creates a loop: a sequence cannot run itself, directly or not."], errors);
    }

    [Fact]
    public void Indirect_loops_are_detected()
    {
        var a = new Sequence { Name = "A" };
        var b = new Sequence { Name = "B" };
        var c = new Sequence { Name = "C" };
        a.Steps.Add(Run(b));
        b.Steps.Add(Run(c));
        c.Steps.Add(Run(b));

        var errors = SequenceExpander.ValidateChaining(a, [a, b, c]);

        Assert.Equal(["Step 1 of 'C' creates a loop: a sequence cannot run itself, directly or not."], errors);
    }

    [Fact]
    public void Missing_and_endless_targets_are_refused()
    {
        var endless = new Sequence { Name = "Farm", RepeatCount = 0, Steps = [Click(1)] };
        var root = new Sequence
        {
            Name = "Root",
            Steps = [new SequenceStep { Kind = StepKind.RunSequence, SequenceId = Guid.NewGuid() }, Run(endless)],
        };

        var errors = SequenceExpander.ValidateChaining(root, [root, endless]);

        Assert.Equal(
            [
                "Step 1 of 'Root' runs a sequence that no longer exists.",
                "Step 2 of 'Root' runs 'Farm', which repeats until stopped: it would never end.",
            ],
            errors);
    }

    [Fact]
    public void The_edited_version_of_the_root_is_used()
    {
        var stored = new Sequence { Name = "Root", Steps = [Click(1)] };
        var edited = new Sequence { Id = stored.Id, Name = "Root", Steps = [Click(2)] };

        var (expanded, _) = SequenceExpander.Expand(edited, [stored]);

        Assert.Equal([2], Markers(expanded!));
    }

    [Fact]
    public void Huge_expansions_are_refused()
    {
        var inner = new Sequence { Name = "Inner", RepeatCount = 1000, Steps = Enumerable.Range(0, 200).Select(Click).ToList() };
        var root = new Sequence { Name = "Root", Steps = [Run(inner)] };

        var (expanded, errors) = SequenceExpander.Expand(root, [root, inner]);

        Assert.Null(expanded);
        Assert.Contains($"more than {SequenceExpander.MaximumSteps} steps", Assert.Single(errors));
    }

    [Fact]
    public void FindCallers_returns_the_sequences_running_a_sequence()
    {
        var target = new Sequence { Name = "Target" };
        var caller = new Sequence { Name = "Caller", Steps = [Run(target)] };

        Assert.Equal([caller], SequenceExpander.FindCallers([target, caller], target.Id));
    }
}
