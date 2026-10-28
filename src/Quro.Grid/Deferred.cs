using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Quro.Grid;

/// <summary>
/// 앞쪽 형제(컬럼 정의)가 초기화된 뒤에 내용을 그리기 위한 자리. 같은 렌더 패스에서 컬럼 등록 → 본문 렌더링 순서를 보장한다.
/// 그리드 내부용이다 (Razor가 컴포넌트로 인식하려면 public이어야 한다).
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class Deferred : ComponentBase
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent);
}
