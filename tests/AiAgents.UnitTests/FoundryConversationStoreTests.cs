using System.ClientModel;
using System.Net;
using System.Text;
using System.Text.Json;
using AiAgents.Application;
using AiAgents.Infrastructure;

namespace AiAgents.UnitTests;

public sealed class FoundryConversationStoreTests
{
    private static readonly string OwnerMarker = new ConversationOwner(
        Guid.Parse("11111111-1111-4111-8111-111111111111"),
        Guid.Parse("22222222-2222-4222-8222-222222222222")).Marker;

    [Fact]
    public async Task CreatesOwnershipMetadataInTheInitialRequest()
    {
        using var handler = new JsonHandler(_ => Json(ConversationJson(OwnerMarker)));
        using var http = new HttpClient(handler);

        var result = await Client(http).CreateAsync(OwnerMarker, default);

        Assert.Equal(new StoredConversation("conv_test", OwnerMarker), result);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.EndsWith("/conversations", Assert.Single(handler.Requests).AbsolutePath);
        using var body = JsonDocument.Parse(Assert.IsType<string>(handler.Body));
        Assert.Equal(OwnerMarker, body.RootElement.GetProperty("metadata").GetProperty("aiagents_owner").GetString());
        Assert.Single(body.RootElement.GetProperty("metadata").EnumerateObject());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadsStoredOwnershipWithoutAdoptingUnownedConversations(bool owned)
    {
        using var handler = new JsonHandler(_ => Json(ConversationJson(owned ? OwnerMarker : null)));
        using var http = new HttpClient(handler);

        var result = await Client(http).GetAsync("conv_test", default);

        Assert.Equal(new StoredConversation("conv_test", owned ? OwnerMarker : null), result);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.EndsWith("/conversations/conv_test", Assert.Single(handler.Requests).AbsolutePath);
        Assert.Null(handler.Body);
    }

    [Theory]
    [InlineData("""{"id":"conv_test","object":"conversation","created_at":1}""")]
    [InlineData("""{"id":"conv_test","object":"conversation","created_at":1,"metadata":null}""")]
    public async Task MissingMetadataDoesNotEstablishOwnership(string json)
    {
        using var handler = new JsonHandler(_ => Json(json));
        using var http = new HttpClient(handler);

        Assert.Equal(new StoredConversation("conv_test", null), await Client(http).GetAsync("conv_test", default));
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task OnlyNotFoundBecomesMissingAndOtherFailuresAreNotRetried(int status)
    {
        using var handler = new JsonHandler(_ => Json(
            """{"error":{"code":"provider_error","message":"private details"}}""", (HttpStatusCode)status));
        using var http = new HttpClient(handler);
        var client = Client(http);

        if (status == 404)
        {
            Assert.Null(await client.GetAsync("conv_test", default));
        }
        else
        {
            var error = await Assert.ThrowsAsync<ClientResultException>(() => client.GetAsync("conv_test", default));
            Assert.Equal(status, error.Status);
        }

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task CreationFailuresAreNotRetried()
    {
        using var handler = new JsonHandler(_ => Json(
            """{"error":{"code":"server_error","message":"private details"}}""", HttpStatusCode.InternalServerError));
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<ClientResultException>(() => Client(http).CreateAsync(OwnerMarker, default));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task HistoryPaginatesChronologicallyAndExcludesSystemMessagesAndToolPayloads()
    {
        using var handler = new JsonHandler(page => Json(page == 1 ? """
            {"object":"list","data":[
              {"id":"msg_user","type":"message","role":"user","content":[{"type":"input_text","text":"Question"}]},
              {"id":"msg_system","type":"message","role":"system","content":[{"type":"input_text","text":"private instructions"}]},
              {"id":"fs_1","type":"file_search_call","status":"completed","queries":["private tool arguments"]}
            ],"first_id":"msg_user","last_id":"fs_1","has_more":true}
            """ : """
            {"object":"list","data":[
              {"id":"msg_answer","type":"message","status":"completed","role":"assistant","content":[
                {"type":"output_text","text":"Stored ","annotations":[]},
                {"type":"output_text","text":"answer","annotations":[
                  {"type":"file_citation","file_id":"file_1","filename":"guide.pdf","index":0},
                  {"type":"url_citation","title":"Unsafe","url":"javascript:alert(1)","start_index":0,"end_index":6}
                ]}
              ]}
            ],"first_id":"msg_answer","last_id":"msg_answer","has_more":false}
            """));
        using var http = new HttpClient(handler);

        var history = await Client(http).GetHistoryAsync("conv_test", default).ToListAsync();

        Assert.Collection(history,
            item =>
            {
                Assert.Equal(ConversationMessageRole.User, item.Role);
                Assert.Equal("Question", item.Text);
            },
            item =>
            {
                Assert.Equal("msg_answer", item.Id);
                Assert.Equal(ConversationMessageRole.Assistant, item.Role);
                Assert.Equal("Stored answer", item.Text);
                Assert.Equal([new Citation("guide.pdf", null), new Citation("Unsafe", null)], item.Citations);
            });
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, uri =>
        {
            Assert.EndsWith("/conversations/conv_test/items", uri.AbsolutePath);
            Assert.Contains("order=asc", uri.Query);
        });
        Assert.Contains("after=fs_1", handler.Requests[1].Query);
    }

    [Fact]
    public async Task HistoryRefusalIsSafeAndUnsupportedContentFailsExplicitly()
    {
        using var handler = new JsonHandler(call => Json(call == 1 ? """
            {"object":"list","data":[{"id":"msg_1","type":"message","role":"assistant","content":[{"type":"refusal","refusal":"private details"}]}],"has_more":false}
            """ : """
            {"object":"list","data":[{"id":"msg_1","type":"message","role":"user","content":[{"type":"input_image","image_url":"https://example.test/private.png"}]}],"has_more":false}
            """));
        using var http = new HttpClient(handler);
        var client = Client(http);

        var history = await client.GetHistoryAsync("conv_test", default).ToListAsync();
        Assert.Equal("The agent could not complete its response.", Assert.Single(history).Text);
        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await client.GetHistoryAsync("conv_test", default).ToListAsync());
    }

    [Fact]
    public async Task HistoryCancellationStopsBeforeTheNextPage()
    {
        using var handler = new JsonHandler(_ => Json("""
            {"object":"list","data":[{"id":"msg_1","type":"message","role":"user","content":[{"type":"input_text","text":"Question"}]}],"first_id":"msg_1","last_id":"msg_1","has_more":true}
            """));
        using var http = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        await using var history = Client(http).GetHistoryAsync("conv_test", cancellation.Token).GetAsyncEnumerator();

        Assert.True(await history.MoveNextAsync());
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await history.MoveNextAsync());
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task PreCancellationPreventsAllConversationRequests()
    {
        using var handler = new JsonHandler(_ => throw new InvalidOperationException("Transport must not be used."));
        using var http = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var client = Client(http);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CreateAsync(OwnerMarker, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("conv_test", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await client.GetHistoryAsync("conv_test", cancellation.Token).ToListAsync());
        Assert.Empty(handler.Requests);
    }

    private static FoundryAgentClient Client(HttpClient http) => new(new FoundryAgentOptions
    {
        ProjectEndpoint = new Uri("https://foundry.example/api/projects/test"),
        AgentName = "test-agent",
        AgentVersion = "1"
    }, new FoundryAgentClientTests.TestCredential(), http);

    private static string ConversationJson(string? ownerMarker) => JsonSerializer.Serialize(new
    {
        id = "conv_test",
        @object = "conversation",
        created_at = 1,
        metadata = ownerMarker is null ? new Dictionary<string, string>() : new() { ["aiagents_owner"] = ownerMarker }
    });

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class JsonHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];
        public HttpMethod? Method { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(Assert.IsType<Uri>(request.RequestUri));
            Method = request.Method;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(Requests.Count);
        }
    }
}
