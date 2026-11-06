using System.Net;
using System.Text;

namespace Quro.Grid.Data.Tests;

/// <summary>
/// GS-107: LoadAsync and Clear (A-75, Control Spec 2-5). LoadAsync GETs a top-level JSON array and replaces the rows only after the
/// whole response was read. A non-2xx response is Failed and bad JSON is Invalid, and neither changes the data set. Network errors and
/// cancellation throw, as in SaveAsync. The latest call wins: an older response returns Superseded and is dropped.
/// Clear is the same as Load([]).
/// </summary>
public class GS_107_LoadAsyncTests
{
    private const string Url = "https://api.test/lines";
    private const string TwoRows = """[{"lineNo":7,"itemCode":"X","qty":1.5,"dueDate":"2026-11-07"},{"LineNo":8,"ITEMCODE":"Y","qty":2}]""";

    /// <summary>Answers each request when the test releases it, so overlapping calls run in a set order.</summary>
    private sealed class HeldHandler : HttpMessageHandler
    {
        public List<TaskCompletionSource<HttpResponseMessage>> Pending { get; } = [];
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
            Pending.Add(pending);
            return pending.Task;
        }

        public void Answer(int call, string body, HttpStatusCode status = HttpStatusCode.OK) =>
            Pending[call].SetResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("no connection");
    }

    private static (QuroDataSet<PoLine> Ds, HeldHandler Handler, HttpClient Http) Setup()
    {
        var handler = new HeldHandler();
        return (TestData.TwoLines(), handler, new HttpClient(handler));
    }

    private static async Task<LoadResult> LoadWith(QuroDataSet<PoLine> ds, string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new HeldHandler();
        var call = ds.LoadAsync(new HttpClient(handler), Url);
        handler.Answer(0, body, status);
        return await call;
    }

    private static int[] LineNos(QuroDataSet<PoLine> ds) => ds.RowKeys.Select(k => ds.GetItem(k).LineNo).ToArray();

    [Fact]
    public async Task GS_107_LoadAsync_GetsTheUrl_AndLoadsTheRows()
    {
        var (ds, handler, http) = Setup();

        var call = ds.LoadAsync(http, Url);
        handler.Answer(0, TwoRows);
        var result = await call;

        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(Url, handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(new LoadResult(LoadStatus.Loaded, HttpStatusCode.OK, 2), result);
        Assert.Equal([7, 8], LineNos(ds));
        Assert.All(ds.RowKeys, k => Assert.Equal(RowState.Normal, ds.GetState(k)));
    }

    [Fact]
    public async Task GS_107_Json_NamesIgnoreCase_ValuesAreRead()
    {
        var ds = new QuroDataSet<PoLine>();

        await LoadWith(ds, TwoRows);

        var first = ds.GetItem(ds.RowKeys[0]);
        Assert.Equal("X", first.ItemCode);
        Assert.Equal(1.5m, first.Qty);
        Assert.Equal(new DateOnly(2026, 11, 7), first.DueDate);
        Assert.Equal("Y", ds.GetItem(ds.RowKeys[1]).ItemCode);   // "ITEMCODE"
    }

    [Fact]
    public async Task GS_107_Success_ActsLikeLoad_DiscardsChanges_ClearsUndo_RaisesLoadedOnce()
    {
        var ds = TestData.TwoLines();
        ds.SetValue(ds.RowKeys[0], nameof(PoLine.Qty), 99m);
        ds.AddRow();
        var loaded = 0;
        ds.Loaded += (_, _) => loaded++;

        await LoadWith(ds, TwoRows);

        Assert.False(ds.HasChanges);
        Assert.False(ds.CanUndo);
        Assert.Equal(1, loaded);
        Assert.Equal(ds.RowKeys[0], ds.CurrentRowKey);
    }

    [Fact]
    public async Task GS_107_EmptyArray_IsLoaded_WithNoRows()
    {
        var ds = TestData.TwoLines();

        var result = await LoadWith(ds, "[]");

        Assert.Equal(new LoadResult(LoadStatus.Loaded, HttpStatusCode.OK, 0), result);
        Assert.Empty(ds.RowKeys);
        Assert.Null(ds.CurrentRowKey);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GS_107_ErrorStatus_IsFailed_NothingChanges(HttpStatusCode status)
    {
        var ds = EditedLines(out var snapshot);
        var loaded = 0;
        ds.Loaded += (_, _) => loaded++;

        var result = await LoadWith(ds, TwoRows, status);

        Assert.Equal(new LoadResult(LoadStatus.Failed, status), result);
        AssertUnchanged(ds, snapshot);
        Assert.Equal(0, loaded);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("""{"rows":[]}""")]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("""[{"lineNo":"abc"}]""")]
    [InlineData("""[{"lineNo":1},""")]
    public async Task GS_107_BadJson_IsInvalid_NothingChanges(string body)
    {
        var ds = EditedLines(out var snapshot);
        var loaded = 0;
        ds.Loaded += (_, _) => loaded++;

        var result = await LoadWith(ds, body);

        Assert.Equal(new LoadResult(LoadStatus.Invalid, HttpStatusCode.OK), result);
        AssertUnchanged(ds, snapshot);
        Assert.Equal(0, loaded);
    }

    [Fact]
    public async Task GS_107_NetworkError_Throws_NothingChanges()
    {
        var ds = EditedLines(out var snapshot);

        await Assert.ThrowsAsync<HttpRequestException>(() => ds.LoadAsync(new HttpClient(new ThrowingHandler()), Url));

        AssertUnchanged(ds, snapshot);
    }

    [Fact]
    public async Task GS_107_Cancelled_Throws_NothingChanges()
    {
        var ds = EditedLines(out var snapshot);
        var handler = new HeldHandler();
        using var cancel = new CancellationTokenSource();

        var call = ds.LoadAsync(new HttpClient(handler), Url, cancel.Token);
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        AssertUnchanged(ds, snapshot);
    }

    [Fact]
    public async Task GS_107_Overlap_OlderAnswerArrivesLast_IsDropped()
    {
        var (ds, handler, http) = Setup();
        var older = ds.LoadAsync(http, Url);
        var newer = ds.LoadAsync(http, Url);

        handler.Answer(1, """[{"lineNo":2}]""");
        Assert.Equal(LoadStatus.Loaded, (await newer).Status);
        handler.Answer(0, """[{"lineNo":1}]""");

        Assert.Equal(LoadStatus.Superseded, (await older).Status);
        Assert.Equal([2], LineNos(ds));
    }

    [Fact]
    public async Task GS_107_Overlap_OlderAnswerArrivesFirst_IsDropped()
    {
        var (ds, handler, http) = Setup();
        var older = ds.LoadAsync(http, Url);
        var newer = ds.LoadAsync(http, Url);

        handler.Answer(0, """[{"lineNo":1}]""");
        Assert.Equal(LoadStatus.Superseded, (await older).Status);
        handler.Answer(1, """[{"lineNo":2}]""");

        Assert.Equal(LoadStatus.Loaded, (await newer).Status);
        Assert.Equal([2], LineNos(ds));
    }

    [Fact]
    public async Task GS_107_Overlap_NewerFails_OlderIsStillDropped()
    {
        var (ds, handler, http) = Setup();
        var before = LineNos(ds);
        var older = ds.LoadAsync(http, Url);
        var newer = ds.LoadAsync(http, Url);

        handler.Answer(1, "", HttpStatusCode.InternalServerError);
        Assert.Equal(LoadStatus.Failed, (await newer).Status);
        handler.Answer(0, """[{"lineNo":1}]""");

        Assert.Equal(LoadStatus.Superseded, (await older).Status);
        Assert.Equal(before, LineNos(ds));
    }

    [Fact]
    public async Task GS_107_ClearOrLoad_WhileWaiting_DropsTheAnswer()
    {
        var (ds, handler, http) = Setup();
        var first = ds.LoadAsync(http, Url);
        ds.Clear();
        handler.Answer(0, TwoRows);
        Assert.Equal(LoadStatus.Superseded, (await first).Status);
        Assert.Empty(ds.RowKeys);

        var second = ds.LoadAsync(http, Url);
        ds.Load([new PoLine { LineNo = 5 }]);
        handler.Answer(1, TwoRows);
        Assert.Equal(LoadStatus.Superseded, (await second).Status);
        Assert.Equal([5], LineNos(ds));
    }

    [Fact]
    public void GS_107_Clear_RemovesEverything_RaisesLoaded()
    {
        var ds = TestData.TwoLines();
        ds.SetValue(ds.RowKeys[0], nameof(PoLine.Qty), 99m);
        ds.AddRow();
        var loaded = 0;
        ds.Loaded += (_, _) => loaded++;

        ds.Clear();

        Assert.Empty(ds.RowKeys);
        Assert.Null(ds.CurrentRowKey);
        Assert.False(ds.HasChanges);
        Assert.False(ds.CanUndo);
        Assert.True(ds.GetChanges().IsEmpty);
        Assert.Equal(1, loaded);
    }

    [Fact]
    public void GS_107_Clear_DataSetStaysUsable()
    {
        var ds = TestData.TwoLines();
        ds.Clear();
        ds.Clear();

        var key = ds.AddRow(new PoLine { LineNo = 9 });

        Assert.Equal([key], ds.RowKeys);
        Assert.Equal(RowState.Inserted, ds.GetState(key));
    }

    private static QuroDataSet<PoLine> EditedLines(out (int[] Lines, int Current, decimal Qty) snapshot)
    {
        var ds = TestData.TwoLines();
        var key = ds.RowKeys[1];
        ds.SetValue(key, nameof(PoLine.Qty), 99m);
        ds.MoveTo(key);
        snapshot = (LineNos(ds), key, 99m);
        return ds;
    }

    private static void AssertUnchanged(QuroDataSet<PoLine> ds, (int[] Lines, int Current, decimal Qty) snapshot)
    {
        Assert.Equal(snapshot.Lines, LineNos(ds));
        Assert.Equal(snapshot.Current, ds.CurrentRowKey);
        Assert.Equal(snapshot.Qty, ds.GetItem(snapshot.Current).Qty);
        Assert.Equal(RowState.Updated, ds.GetState(snapshot.Current));
        Assert.True(ds.CanUndo);
    }
}
