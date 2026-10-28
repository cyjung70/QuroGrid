namespace Quro.Grid.Data;

/// <summary>
/// 필드 검증 규칙 (Spec 4.5·4.6). 그리드는 QuroColumn의 Required·Min·Max를
/// <see cref="QuroDataSet{T}.SetRule"/>로 넘긴다.
/// </summary>
public sealed class FieldRule
{
    /// <summary>null 또는 공백 문자열이면 오류.</summary>
    public bool Required { get; init; }

    /// <summary>Smallest allowed number (inclusive).</summary>
    public decimal? Min { get; init; }
    /// <summary>Largest allowed number (inclusive).</summary>
    public decimal? Max { get; init; }
}
