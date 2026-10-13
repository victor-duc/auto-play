namespace AutoPlay.Domain.Model;

/// <summary>What a sequence step does.</summary>
public enum StepKind
{
    /// <summary>Clicks a location of a screen (after an optional verification).</summary>
    Click,

    /// <summary>Waits a random duration within <see cref="SequenceStep.Delay"/>.</summary>
    Wait,

    /// <summary>Runs another sequence (<see cref="SequenceStep.SequenceId"/>), its repeat count included.</summary>
    RunSequence,
}
