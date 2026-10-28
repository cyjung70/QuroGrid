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

    /// <summary>
    /// Popup 셀 코드 조회 (Addendum A-16). 입력 문자열 → 일치하는 코드 목록.
    /// 1건이면 자동 확정, 여러 건이면 OnPopupRequested, 0건이면 편집 유지 (A-05).
    /// </summary>
    [Parameter] public Func<string, Task<IReadOnlyList<string>>>? Lookup { get; set; }

    /// <summary>Combo 셀 목록 (Spec 4.6: 공통코드).</summary>
    [Parameter] public IReadOnlyList<ComboOption>? Options { get; set; }

    /// <summary>Check 셀에서 체크 상태로 저장할 값 (Y/N 매핑 등). 기본 true.</summary>
    [Parameter] public object? CheckedValue { get; set; } = true;

    /// <summary>Check 셀에서 체크 해제 상태로 저장할 값. 기본 false.</summary>
    [Parameter] public object? UncheckedValue { get; set; } = false;

    /// <summary>Button 셀 표시 문자열 리소스 키 (Spec 4.8). Button 컬럼의 Field는 식별자일 뿐 값을 읽지 않는다.</summary>
    [Parameter] public string? TextKey { get; set; }

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
        object now = (Field, HeaderKey, Type, Format, ReadOnly, Width, Aggregate, CurrencyField, Required, Min, Max,
            (Lookup, Options, CheckedValue, UncheckedValue, TextKey));
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
