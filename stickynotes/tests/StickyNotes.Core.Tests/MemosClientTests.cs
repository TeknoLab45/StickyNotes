using System.Net;
using System.Net.Http;
using StickyNotes.Core.Sync;

namespace StickyNotes.Core.Tests;

public class MemosClientTests
{
    private const string Token = "memos_pat_SECRET_TEST_TOKEN";

    private static (MemosClient Client, StubHandler Handler) Make(Func<HttpRequestMessage, HttpResponseMessage> respond, string? root = null)
    {
        var handler = new StubHandler { Respond = respond };
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://memos.test") };
        return (new MemosClient(http, "http://memos.test/", Token, root), handler);
    }

    [Fact]
    public async Task DetectRoot_UsesApi_WhenProfileAnswersThere()
    {
        var (client, _) = Make(r => r.RequestUri!.AbsolutePath == "/api/instance/profile"
            ? StubHandler.Json(HttpStatusCode.OK, "{\"version\":\"26.11.1\"}")
            : new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Equal("/api", await client.DetectRootAsync());
        Assert.Equal("/api", client.ApiRoot);
    }

    [Fact]
    public async Task DetectRoot_FallsBackToApiV1_WhenApiProfileIs404()
    {
        var (client, _) = Make(r => r.RequestUri!.AbsolutePath == "/api/v1/instance/profile"
            ? StubHandler.Json(HttpStatusCode.OK, "{\"version\":\"0.31.0\"}")
            : new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Equal("/api/v1", await client.DetectRootAsync());
    }

    [Fact]
    public async Task DetectRoot_NeitherAnswers_ThrowsVersionError()
    {
        var (client, _) = Make(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        await Assert.ThrowsAsync<MemosVersionException>(() => client.DetectRootAsync());
    }

    [Fact]
    public async Task CachedRoot_IsUsedWithoutProbing()
    {
        var (client, handler) = Make(_ => StubHandler.Json(HttpStatusCode.OK, "{\"user\":{\"name\":\"users/host\",\"username\":\"host\"}}"), "/api/v1");
        var me = await client.GetMeAsync();
        Assert.Equal("users/host", me.Name);
        Assert.Single(handler.Seen);
        Assert.Equal("/api/v1/auth/me", handler.Seen[0].RequestUri!.AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task DeleteMemo_SendsDelete_AndTreats404AsSuccess(HttpStatusCode status)
    {
        var (client, handler) = Make(_ => StubHandler.Json(status, "{}"), "/api");

        await client.DeleteMemoAsync("memos/abc");

        Assert.Equal(HttpMethod.Delete, handler.Seen.Single().Method);
        Assert.Equal("/api/memos/abc", handler.Seen[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task DeleteMemo_ServerError_Throws()
    {
        var (client, _) = Make(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError), "/api");
        await Assert.ThrowsAsync<MemosException>(() => client.DeleteMemoAsync("memos/abc"));
    }

    [Fact]
    public async Task Requests_CarryBearerToken()
    {
        var (client, handler) = Make(_ => StubHandler.Json(HttpStatusCode.OK, "{\"user\":{\"name\":\"users/host\",\"username\":\"host\"}}"), "/api");
        await client.GetMeAsync();
        Assert.Equal("Bearer " + Token, handler.Seen[0].Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task StatusCodes_MapToTypedErrors_WithoutLeakingToken()
    {
        var (c401, _) = Make(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized), "/api");
        var e401 = await Assert.ThrowsAsync<MemosUnauthorizedException>(() => c401.GetMeAsync());
        Assert.DoesNotContain(Token, e401.Message);

        var (c403, _) = Make(_ => new HttpResponseMessage(HttpStatusCode.Forbidden), "/api");
        await Assert.ThrowsAsync<MemosForbiddenException>(() => c403.UpdateMemoAsync("memos/x", "a", "NORMAL"));

        var (c409, _) = Make(_ => new HttpResponseMessage(HttpStatusCode.Conflict), "/api");
        await Assert.ThrowsAsync<MemosAlreadyExistsException>(() => c409.CreateMemoAsync("abc", "spaces/s", "SPACE", "x"));
    }

    [Fact]
    public async Task NotFound_ReturnsNull_ForLookups()
    {
        var (client, _) = Make(_ => new HttpResponseMessage(HttpStatusCode.NotFound), "/api");
        Assert.Null(await client.GetSpaceAsync("spaces/gone"));
        Assert.Null(await client.GetMemoAsync("memos/gone"));
        Assert.Null(await client.GetUserAsync("users/gone"));
    }

    [Fact]
    public async Task TransportFailure_BecomesOffline()
    {
        var (client, _) = Make(_ => throw new HttpRequestException("connection refused"), "/api");
        await Assert.ThrowsAsync<MemosOfflineException>(() => client.GetMeAsync());
    }

    [Fact]
    public async Task ListMemos_SendsStateAndEncodedSpaceFilter()
    {
        var (client, handler) = Make(_ => StubHandler.Json(HttpStatusCode.OK, "{\"memos\":[],\"nextPageToken\":\"\"}"), "/api/v1");
        await client.ListMemosAsync("spaces/s1", "ARCHIVED");
        var url = handler.Seen[0].RequestUri!.OriginalString;
        Assert.Contains("state=ARCHIVED", url);
        Assert.Contains(Uri.EscapeDataString("space == \"spaces/s1\""), url);
    }

    [Fact]
    public async Task ListMemos_FollowsPageTokens()
    {
        var calls = 0;
        var (client, _) = Make(_ =>
        {
            calls++;
            return calls == 1
                ? StubHandler.Json(HttpStatusCode.OK, "{\"memos\":[{\"name\":\"memos/a\",\"creator\":\"users/host\",\"content\":\"1\",\"state\":\"NORMAL\",\"space\":\"spaces/s1\",\"updateTime\":\"2026-11-19T09:00:00Z\"}],\"nextPageToken\":\"p2\"}")
                : StubHandler.Json(HttpStatusCode.OK, "{\"memos\":[{\"name\":\"memos/b\",\"creator\":\"users/host\",\"content\":\"2\",\"state\":\"NORMAL\",\"space\":\"spaces/s1\",\"updateTime\":\"2026-11-19T09:00:00Z\"}],\"nextPageToken\":\"\"}");
        }, "/api");
        var list = await client.ListMemosAsync("spaces/s1", "NORMAL");
        Assert.Equal(new[] { "memos/a", "memos/b" }, list.Select(m => m.Name));
    }
}
