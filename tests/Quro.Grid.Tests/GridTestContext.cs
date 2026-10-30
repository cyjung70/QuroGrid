using Bunit;

namespace Quro.Grid.Tests;

/// <summary>
/// 컴포넌트 테스트 공통 기반. 그리드는 첫 렌더 후 quro-grid.js를 등록하므로 JS 호출을 느슨한 모드로 받는다.
/// JS 등록 자체는 GS_037 테스트에서 검증한다.
/// </summary>
public abstract class GridTestContext : BunitContext, IAsyncLifetime
{
    public const string ModulePath = "./_content/Quro.Grid/quro-grid.js";

    protected GridTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;

    // 비동기 정리만 지원하는 서비스를 위해 xUnit이 동기 Dispose보다 먼저 호출한다.
    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();
}
