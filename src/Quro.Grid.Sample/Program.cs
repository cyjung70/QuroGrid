using Quro.Grid.Sample.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // 끊긴 회로 보관 시간 (R7). E2E는 회로 소멸(GS-048)을 재현하려고 짧게 준다.
        if (builder.Configuration.GetValue<int?>("Circuit:RetentionSeconds") is { } seconds)
            options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromSeconds(seconds);
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// 화면 언어: 요청(쿠키·Accept-Language)에 따라 ko/en. 회로도 연결 요청의 언어를 따른다.
string[] languages = ["ko-KR", "en-US"];
app.UseRequestLocalization(o => o
    .SetDefaultCulture(languages[0])
    .AddSupportedCultures(languages)
    .AddSupportedUICultures(languages));

app.UseAntiforgery();

// 성능 측정용 관리 힙 크기 (GS-058, E2E). 설정으로 켠 경우에만 연다.
if (app.Configuration.GetValue<bool>("Diagnostics:Memory"))
    app.MapGet("/_diag/memory", () => new { HeapBytes = GC.GetTotalMemory(forceFullCollection: true) });

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
