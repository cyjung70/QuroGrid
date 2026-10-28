using System.Net;

namespace Quro.Grid.Data.Tests;

/// <summary>저장 API 대역. 정해진 상태 코드로 응답하고 받은 요청을 기록한다.</summary>
internal sealed class FakeHttpHandler(HttpStatusCode status) : HttpMessageHandler
{
    public List<(HttpMethod Method, Uri? Uri, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri, body));
        return new HttpResponseMessage(status);
    }
}
