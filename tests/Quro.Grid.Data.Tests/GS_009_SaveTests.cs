using System.Net;

namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-009: DataSet → 저장 API 호출 (Spec 4.4, 2026-10-28 결정).
/// 저장 API는 QuroGrid와 분리된 서버이므로 여기서는 가짜 HTTP 핸들러로 DataSet 쪽 동작만 검증한다.
/// 성공 시 AcceptChanges, 충돌·실패 시 변경분 유지. 성공 후 재조회는 앱이 한다.
/// </summary>
public class GS_009_SaveTests
{
    private const string Url = "https://api.test/po/PO-2026-00123/lines/changes";

    private static (QuroDataSet<PoLine> Ds, int Row) Edited()
    {
        var ds = TestData.TwoLines();
        var row = ds.RowKeys[0];
        ds.SetValue(row, nameof(PoLine.Qty), 15m);
        return (ds, row);
    }

    [Fact]
    public async Task GS_009_SaveStaleVersion_Conflict()
    {
        var (ds, row) = Edited();
        var http = new HttpClient(new FakeHttpHandler(HttpStatusCode.Conflict));

        var result = await ds.SaveAsync(http, Url);

        Assert.Equal(SaveStatus.Conflict, result.Status);
        Assert.Equal(RowState.Updated, ds.GetState(row));
        Assert.False(ds.GetChanges().IsEmpty);
    }

    [Fact]
    public async Task GS_009_SaveSuccess_PostsChangesAndAccepts()
    {
        var (ds, row) = Edited();
        var expectedBody = ds.GetChanges().ToJson();
        var handler = new FakeHttpHandler(HttpStatusCode.OK);

        var result = await ds.SaveAsync(new HttpClient(handler), Url);

        Assert.Equal(SaveStatus.Saved, result.Status);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(Url, request.Uri!.ToString());
        Assert.Equal(expectedBody, request.Body);
        Assert.Equal(RowState.Normal, ds.GetState(row));
        Assert.True(ds.GetChanges().IsEmpty);
    }

    [Fact]
    public async Task GS_009_SaveServerError_KeepsChanges()
    {
        var (ds, row) = Edited();
        var http = new HttpClient(new FakeHttpHandler(HttpStatusCode.InternalServerError));

        var result = await ds.SaveAsync(http, Url);

        Assert.Equal(SaveStatus.Failed, result.Status);
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Equal(RowState.Updated, ds.GetState(row));
    }

    [Fact]
    public async Task GS_009_SaveWithoutChanges_DoesNotCallApi()
    {
        var ds = TestData.TwoLines();
        var handler = new FakeHttpHandler(HttpStatusCode.OK);

        var result = await ds.SaveAsync(new HttpClient(handler), Url);

        Assert.Equal(SaveStatus.NoChanges, result.Status);
        Assert.Empty(handler.Requests);
    }
}
