using System.ClientModel;
using System.Net;
using System.Text;
using System.Text.Json;
using AiAgents.Application;
using AiAgents.Infrastructure;
using Azure.Core;

namespace AiAgents.UnitTests;

public sealed class FoundryAgentClientTests
{
    private const string Completed = """
        {"type":"response.completed","sequence_number":10,"response":{"id":"resp_1","object":"response","created_at":1,"status":"completed","model":"test-model","output":[],"usage":{"input_tokens":12,"output_tokens":7,"total_tokens":19}}}
        """;
    private static FoundryAgentOptions Options => new()
    {
        ProjectEndpoint = new Uri("https://foundry.example/api/projects/test"),
        AgentName = "test-agent",
        AgentVersion = "1"
    };

    [Fact]
    public async Task MapsTextRetrievalCitationsAndUsageUsingTheRealSdk()
    {
        using var handler = new StubHandler(Sse(
            """{"type":"response.created","sequence_number":0,"response":{"id":"resp_1","object":"response","created_at":1,"status":"in_progress","model":"test-model","output":[]}}""",
            """{"type":"response.output_item.added","sequence_number":1,"output_index":0,"item":{"id":"fs_1","type":"file_search_call","status":"in_progress","queries":[]}}""",
            """{"type":"response.output_item.done","sequence_number":2,"output_index":0,"item":{"id":"fs_1","type":"file_search_call","status":"completed","queries":["test"]}}""",
            """{"type":"response.output_text.delta","sequence_number":3,"item_id":"msg_1","output_index":1,"content_index":0,"delta":"Hello"}""",
            """{"type":"response.output_text.done","sequence_number":4,"item_id":"msg_1","output_index":1,"content_index":0,"text":"Hello"}""",
            """{"type":"response.output_item.done","sequence_number":5,"output_index":1,"item":{"id":"msg_1","type":"message","status":"completed","role":"assistant","content":[{"type":"output_text","text":"Hello","annotations":[{"type":"file_citation","file_id":"file_1","filename":"guide.pdf","index":0},{"type":"url_citation","title":"Guide","url":"https://example.com/guide","start_index":0,"end_index":5}]}]}}""",
            Completed));
        using var http = new HttpClient(handler);
        var client = new FoundryAgentClient(Options, new TestCredential(), http);

        var events = await CollectAsync(client);

        Assert.Collection(events,
            item => Assert.IsType<StatusUpdate>(item),
            item => Assert.Equal(new ToolCallStarted("fs_1", "file_search"), item),
            item => Assert.Equal(new ToolCallCompleted("fs_1"), item),
            item => Assert.Equal(new TextDelta("Hello"), item),
            item => Assert.Equal(new CompletedText("Hello"), item),
            item => Assert.Equal(new Citation("guide.pdf", null, "file_1"), item),
            item => Assert.Equal(new Citation("Guide", new Uri("https://example.com/guide"), IsUriCitation: true), item),
            item => Assert.Equal(new UsageInformation(12, 7), item),
            item => Assert.IsType<AgentCompleted>(item));
        Assert.Equal(1, handler.RequestCount);
        Assert.True(handler.StreamDisposed);
        using var request = JsonDocument.Parse(Assert.IsType<string>(handler.Body));
        Assert.Equal("test-agent", request.RootElement.GetProperty("agent_reference").GetProperty("name").GetString());
        Assert.Equal("1", request.RootElement.GetProperty("agent_reference").GetProperty("version").GetString());
        Assert.Equal("conv_test", request.RootElement.GetProperty("conversation").GetProperty("id").GetString());
        Assert.True(request.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal("question", request.RootElement.GetProperty("input")[0].GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task MapsAzureSearchToolItems()
    {
        using var handler = new StubHandler(Sse(
            """{"type":"response.output_item.added","sequence_number":0,"output_index":0,"item":{"id":"search_1","type":"azure_ai_search_call","status":"in_progress","arguments":"{}"}}""",
            """{"type":"response.output_item.done","sequence_number":1,"output_index":0,"item":{"id":"search_1","type":"azure_ai_search_call","status":"completed","arguments":"{}"}}""",
            Completed));
        using var http = new HttpClient(handler);

        var events = await CollectAsync(new FoundryAgentClient(Options, new TestCredential(), http));

        Assert.Equal(new ToolCallStarted("search_1", "azure_ai_search"), events[0]);
        Assert.Equal(new ToolCallCompleted("search_1"), events[1]);
        Assert.IsType<AgentCompleted>(events[^1]);
    }

    [Fact]
    public async Task CompletedTextPreservesEarlierContentPartsAndUnsafeLinksAreNotExposed()
    {
        using var handler = new StubHandler(Sse(
            """{"type":"response.output_text.done","sequence_number":0,"item_id":"msg_1","output_index":0,"content_index":0,"text":"First"}""",
            """{"type":"response.output_text.done","sequence_number":1,"item_id":"msg_1","output_index":0,"content_index":1,"text":" second"}""",
            """{"type":"response.output_item.done","sequence_number":2,"output_index":0,"item":{"id":"msg_1","type":"message","status":"completed","role":"assistant","content":[{"type":"output_text","text":"First second","annotations":[{"type":"url_citation","title":"Untrusted","url":"javascript:alert(1)","start_index":0,"end_index":5}]}]}}""",
            Completed));
        using var http = new HttpClient(handler);

        var events = await CollectAsync(new FoundryAgentClient(Options, new TestCredential(), http));

        Assert.Equal(new CompletedText("First second"), events[1]);
        Assert.Equal(new Citation("Untrusted", null, IsUriCitation: true), events[2]);
    }

    [Theory]
    [InlineData("""{"type":"error","sequence_number":0,"code":"server_error","message":"sensitive provider details"}""")]
    [InlineData("""{"type":"response.failed","sequence_number":0,"response":{"id":"resp_1","status":"failed","error":{"code":"server_error","message":"sensitive provider details"}}}""")]
    [InlineData("""{"type":"response.incomplete","sequence_number":0,"response":{"id":"resp_1","status":"incomplete","incomplete_details":{"reason":"max_output_tokens"}}}""")]
    [InlineData("""{"type":"response.refusal.done","sequence_number":0,"item_id":"msg_1","output_index":0,"content_index":0,"refusal":"sensitive provider details"}""")]
    public async Task TerminalFailuresNeverBecomeSuccessOrExposeProviderDetails(string error)
    {
        using var handler = new StubHandler(Sse(error, Completed));
        using var http = new HttpClient(handler);

        var events = await CollectAsync(new FoundryAgentClient(Options, new TestCredential(), http));

        var failure = Assert.IsType<AgentError>(Assert.Single(events));
        Assert.DoesNotContain("sensitive provider details", failure.Message);
        Assert.True(handler.StreamDisposed);
    }

    [Fact]
    public async Task NeverDispatchesAnUnsupportedFunctionCall()
    {
        using var handler = new StubHandler(Sse(
            """{"type":"response.output_item.added","sequence_number":0,"output_index":0,"item":{"id":"fc_1","type":"function_call","call_id":"call_1","name":"execute_command","arguments":"{}","status":"in_progress"}}""",
            Completed));
        using var http = new HttpClient(handler);

        var events = await CollectAsync(new FoundryAgentClient(Options, new TestCredential(), http));

        Assert.IsType<AgentError>(Assert.Single(events));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task TruncatedStreamThrowsInsteadOfClaimingCompletion()
    {
        using var handler = new StubHandler(Sse(
            """{"type":"response.output_text.delta","sequence_number":0,"item_id":"msg_1","output_index":0,"content_index":0,"delta":"Partial"}"""));
        using var http = new HttpClient(handler);
        var client = new FoundryAgentClient(Options, new TestCredential(), http);
        await using var stream = client.StreamAsync("conv_test", "question", default).GetAsyncEnumerator();

        Assert.True(await stream.MoveNextAsync());
        Assert.Equal(new TextDelta("Partial"), stream.Current);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await stream.MoveNextAsync());
        Assert.True(handler.StreamDisposed);
    }

    [Fact]
    public async Task CancellationDisposesTheStreamWithoutRetrying()
    {
        using var handler = new StubHandler(Sse(
            """{"type":"response.output_text.delta","sequence_number":0,"item_id":"msg_1","output_index":0,"content_index":0,"delta":"Partial"}""",
            Completed));
        using var http = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        var client = new FoundryAgentClient(Options, new TestCredential(), http);
        await using var stream = client.StreamAsync("conv_test", "question", cancellation.Token).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await stream.MoveNextAsync());
        Assert.True(handler.StreamDisposed);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task GenerativePostIsNeverAutomaticallyRetried(HttpStatusCode status)
    {
        using var handler = new StubHandler("""{"error":{"code":"test","message":"test failure"}}""", status);
        using var http = new HttpClient(handler);
        var client = new FoundryAgentClient(Options, new TestCredential(), http);

        await Assert.ThrowsAsync<ClientResultException>(() => CollectAsync(client));

        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("http://foundry.example/api/projects/test")]
    [InlineData("https://user:password@foundry.example/api/projects/test")]
    [InlineData("https://foundry.example/api/projects/test?key=not-allowed")]
    public void RejectsInsecureOrCredentialBearingProjectEndpoints(string endpoint)
    {
        using var handler = new StubHandler(string.Empty);
        using var http = new HttpClient(handler);

        Assert.Throws<ArgumentException>(() => new FoundryAgentClient(
            Options with { ProjectEndpoint = new Uri(endpoint) }, new TestCredential(), http));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task PreCanceledRequestDoesNotReachTheTransport()
    {
        using var handler = new StubHandler(Sse(Completed));
        using var http = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var client = new FoundryAgentClient(Options, new TestCredential(), http);
        await using var stream = client.StreamAsync("conv_test", "question", cancellation.Token).GetAsyncEnumerator();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await stream.MoveNextAsync());

        Assert.Equal(0, handler.RequestCount);
    }

    private static async Task<List<AgentStreamEvent>> CollectAsync(FoundryAgentClient client)
    {
        List<AgentStreamEvent> events = [];
        await foreach (var item in client.StreamAsync("conv_test", "question", default))
        {
            events.Add(item);
        }

        return events;
    }

    private static string Sse(params string[] events) => string.Concat(events.Select(json =>
    {
        using var document = JsonDocument.Parse(json);
        return $"event: {document.RootElement.GetProperty("type").GetString()}\ndata: {json}\n\n";
    }));

    internal sealed class TestCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("test-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        private TrackingStream? _stream;
        public int RequestCount { get; private set; }
        public string? Body { get; private set; }
        public bool StreamDisposed => _stream?.IsDisposed == true;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https", request.RequestUri?.Scheme);
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            _stream = new TrackingStream(Encoding.UTF8.GetBytes(body));
            var content = new StreamContent(_stream);
            content.Headers.ContentType = new(status == HttpStatusCode.OK ? "text/event-stream" : "application/json");
            return new HttpResponseMessage(status) { Content = content };
        }
    }

    private sealed class TrackingStream(byte[] content) : MemoryStream(content)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
