namespace AutoPlay.Core.Execution;

/// <summary>Progress reported before each click.</summary>
/// <param name="Position">The step about to be executed.</param>
/// <param name="StepCount">Number of steps in the sequence.</param>
/// <param name="MatchScore">Score of the verification, or null if the step is not verified.</param>
public sealed record RunProgress(RunPosition Position, int StepCount, double? MatchScore);
