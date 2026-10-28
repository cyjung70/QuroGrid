using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Quro.Grid.E2E;

/// <summary>
/// 샘플 앱(Quro.Grid.Sample)을 별도 프로세스로 띄우는 테스트 고정 장치. 테스트 컬렉션 전체가 공유한다.
/// </summary>
public sealed class SampleServer : IAsyncLifetime
{
    private Process? _process;

    public string BaseUrl { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}";
        var project = Path.Combine(RepoRoot(), "src", "Quro.Grid.Sample");

        _process = Process.Start(new ProcessStartInfo("dotnet",
            ["run", "--project", project, "--no-build", "--no-launch-profile", "--urls", BaseUrl])
        {
            WorkingDirectory = project,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment =
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Development",
                ["Circuit__RetentionSeconds"] = "5",   // GS-048: 끊긴 회로를 빨리 버려 회로 소멸을 재현
                ["Diagnostics__Memory"] = "true",      // GS-058: 회로당 메모리 측정용 /_diag/memory
            },
        }) ?? throw new InvalidOperationException("샘플 앱을 시작하지 못했습니다.");
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        using var http = new HttpClient();
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
                throw new InvalidOperationException($"샘플 앱이 종료되었습니다 (코드 {_process.ExitCode}).");
            try
            {
                if ((await http.GetAsync(BaseUrl)).IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
                // 아직 기동 중
            }
            await Task.Delay(250);
        }
        throw new TimeoutException("샘플 앱이 60초 안에 응답하지 않았습니다.");
    }

    public Task DisposeAsync()
    {
        if (_process is { HasExited: false })
            _process.Kill(entireProcessTree: true);
        _process?.Dispose();
        return Task.CompletedTask;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "QuroGrid.slnx")))
                return dir.FullName;
        throw new DirectoryNotFoundException("QuroGrid.slnx를 찾지 못했습니다.");
    }
}

[CollectionDefinition(Name)]
public sealed class SampleCollection : ICollectionFixture<SampleServer>
{
    public const string Name = "sample";
}
