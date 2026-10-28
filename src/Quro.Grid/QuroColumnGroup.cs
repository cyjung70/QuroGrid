using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Quro.Grid;

/// <summary>
/// 다단 헤더 그룹 (GS-070, P1). 안에 둔 컬럼(과 그룹) 위에 그룹 헤더를 그린다. 직접 그리지 않고,
/// 안의 컬럼이 자기 그룹을 알도록 자신을 내려 준다. 그룹 안에 그룹을 두면 한 행 아래에 그린다.
/// </summary>
public sealed class QuroColumnGroup : ComponentBase, IDisposable
{
    [CascadingParameter] internal IColumnHost? Host { get; set; }

    /// <summary>바깥 그룹 (그룹 안의 그룹이면)</summary>
    [CascadingParameter] internal QuroColumnGroup? Parent { get; set; }

    /// <summary>그룹 헤더 리소스 키 (Spec 4.8)</summary>
    [Parameter, EditorRequired] public string HeaderKey { get; set; } = "";

    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>안에 정의된 컬럼과 그룹 (정의 순서). 그리드가 채운다</summary>
    internal List<object> Members { get; } = [];

    private string? _rendered;

    protected override void OnInitialized()
    {
        if (Host is null)
            throw new InvalidOperationException($"{nameof(QuroColumnGroup)}은 QuroGrid 안에서만 쓸 수 있습니다.");
        Host.AddGroup(this);
    }

    protected override void OnParametersSet()
    {
        if (_rendered is not null && _rendered != HeaderKey)
            Host?.GroupChanged();
        _rendered = HeaderKey;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<QuroColumnGroup>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<QuroColumnGroup>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<QuroColumnGroup>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<QuroColumnGroup>.ChildContent), ChildContent);
        builder.CloseComponent();
    }

    public void Dispose() => Host?.RemoveGroup(this);

    /// <summary>바깥에서 안쪽 순서의 그룹 경로 (이 그룹 포함)</summary>
    internal List<QuroColumnGroup> Path()
    {
        var path = Parent?.Path() ?? [];
        path.Add(this);
        return path;
    }
}
