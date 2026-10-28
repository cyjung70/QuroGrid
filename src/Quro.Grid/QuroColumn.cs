using Microsoft.AspNetCore.Components;

namespace Quro.Grid;

/// <summary>
/// 컬럼 정의 (Spec 4.5). 직접 그리지 않고 부모 <see cref="QuroGrid{TItem}"/>에 등록만 한다.
/// </summary>
public sealed class QuroColumn : ComponentBase, IDisposable
{
    [CascadingParameter] internal IColumnHost? Host { get; set; }

    [Parameter, EditorRequired] public string Field { get; set; } = "";

    /// <summary>헤더 리소스 키 (Spec 4.8). 문자열을 하드코딩하지 않는다.</summary>
    [Parameter, EditorRequired] public string HeaderKey { get; set; } = "";

    [Parameter] public CellType Type { get; set; } = CellType.Text;

    /// <summary>.NET 서식 문자열 (예: N3). 표시는 그리드의 Culture를 따른다.</summary>
    [Parameter] public string? Format { get; set; }

    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>열 너비 (px).</summary>
    [Parameter] public int Width { get; set; } = 120;

    [Parameter] public Aggregate Aggregate { get; set; }

    /// <summary>Currency 컬럼에서 행의 통화 코드를 읽을 필드 (Spec 4.6).</summary>
    [Parameter] public string? CurrencyField { get; set; }

    /// <summary>필수 입력 (DataSet 규칙으로 등록, Addendum A-10).</summary>
    [Parameter] public bool Required { get; set; }

    [Parameter] public decimal? Min { get; set; }
    [Parameter] public decimal? Max { get; set; }

    private object? _rendered;

    protected override void OnInitialized()
    {
        if (Host is null)
            throw new InvalidOperationException($"{nameof(QuroColumn)}은 QuroGrid 안에서만 쓸 수 있습니다.");
        Host.AddColumn(this);
    }

    /// <summary>정의가 실제로 바뀐 경우에만 그리드 전체를 다시 그리게 한다 (R4).</summary>
    protected override void OnParametersSet()
    {
        object now = (Field, HeaderKey, Type, Format, ReadOnly, Width, Aggregate, CurrencyField, Required, Min, Max);
        if (_rendered is not null && !Equals(now, _rendered))
            Host?.ColumnChanged(this);
        _rendered = now;
    }

    public void Dispose() => Host?.RemoveColumn(this);
}

internal interface IColumnHost
{
    void AddColumn(QuroColumn column);
    void ColumnChanged(QuroColumn column);
    void RemoveColumn(QuroColumn column);
}
