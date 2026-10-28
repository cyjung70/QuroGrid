namespace Quro.Grid.Data;

/// <summary>셀 하나의 검증 오류. 메시지는 리소스 키(<see cref="MessageKey"/>)로만 전달한다.</summary>
public sealed record ValidationError(int RowKey, string Field, string MessageKey);

/// <summary>검증 결과 (Spec 4.4).</summary>
public sealed class ValidationResult
{
    public IReadOnlyList<ValidationError> Errors { get; init; } = [];

    public bool IsValid => Errors.Count == 0;
}

/// <summary>검증 메시지 리소스 키.</summary>
public static class ValidationKeys
{
    public const string Required = "quro.validation.required";
    public const string Min = "quro.validation.min";
    public const string Max = "quro.validation.max";
}
