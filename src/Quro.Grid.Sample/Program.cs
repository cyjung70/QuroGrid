using Quro.Grid.Sample.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
