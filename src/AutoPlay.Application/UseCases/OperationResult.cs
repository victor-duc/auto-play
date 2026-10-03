namespace AutoPlay.Application.UseCases;

/// <summary>The outcome of a use case that can be refused: success, or the reasons of the refusal.</summary>
public sealed record OperationResult
{
    private static readonly OperationResult s_success = new([]);

    private OperationResult(IReadOnlyList<string> errors) => Errors = errors;

    /// <summary>Messages to show to the user; empty on success.</summary>
    public IReadOnlyList<string> Errors { get; }

    public bool Succeeded => Errors.Count == 0;

    public static OperationResult Success() => s_success;

    public static OperationResult Failure(params IReadOnlyList<string> errors) => new(EnsureErrors(errors));

    public static OperationResult<T> Success<T>(T value) => new(value, []);

    public static OperationResult<T> Failure<T>(params IReadOnlyList<string> errors) => new(default, EnsureErrors(errors));

    private static IReadOnlyList<string> EnsureErrors(IReadOnlyList<string> errors) =>
        errors.Count == 0 ? throw new ArgumentException("A failure needs at least one error.", nameof(errors)) : errors;
}

/// <summary>The outcome of a use case that returns a value when it succeeds; created with <see cref="OperationResult"/>.</summary>
public sealed record OperationResult<T>
{
    internal OperationResult(T? value, IReadOnlyList<string> errors)
    {
        Value = value;
        Errors = errors;
    }

    /// <summary>The result; only meaningful when <see cref="Succeeded"/> is true.</summary>
    public T? Value { get; }

    public IReadOnlyList<string> Errors { get; }

    public bool Succeeded => Errors.Count == 0;
}
