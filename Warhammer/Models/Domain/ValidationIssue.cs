namespace Warhammer.Models.Domain;

public enum ValidationSeverity
{
    Error,
    Warning,
}

public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message,
    UnitConfiguration? RelatedUnit = null);
