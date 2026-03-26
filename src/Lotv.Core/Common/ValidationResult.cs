namespace Lotv.Core.Common;

public class ValidationResult
{
    private readonly List<ValidationError> _errors = [];

    public bool IsValid => _errors.Count == 0;
    public IReadOnlyList<ValidationError> Errors => _errors;

    public void AddError(string field, string message) =>
        _errors.Add(new ValidationError(field, message));

    public static ValidationResult Success() => new();
}

public record ValidationError(string Field, string Message);
