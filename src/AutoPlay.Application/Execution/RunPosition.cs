namespace AutoPlay.Application.Execution;

/// <summary>A position in a sequence run: the iteration (0-based) and the step index within it.</summary>
public readonly record struct RunPosition(int Iteration, int StepIndex);
