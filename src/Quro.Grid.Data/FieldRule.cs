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

    /// <summary>When true, <see cref="Min"/> itself is not allowed (greater than). Screen definition <c>gt</c>, G-02.</summary>
    public bool MinExclusive { get; init; }

    /// <summary>When true, <see cref="Max"/> itself is not allowed (less than). Screen definition <c>lt</c>, G-02.</summary>
    public bool MaxExclusive { get; init; }

    /// <summary>Number that is not allowed. Screen definition <c>ne</c>, G-02.</summary>
    public decimal? NotEqual { get; init; }

    /// <summary>
    /// Resource key reported instead of the default range and not-equal keys (screen definition <c>validate.msg</c>).
    /// Required errors keep <see cref="ValidationKeys.Required"/>.
    /// </summary>
    public string? MessageKey { get; init; }
}
