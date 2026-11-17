using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using AiAgents.Application;
using AiAgents.Web.Chat;
using AiAgents.Web.Components.Chat;
using Bunit;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AiAgents.ComponentTests;

public sealed class AgentChatTests
{
    [Fact]
    public async Task CreatesOwnedChatStreamsProgressAndPreventsDuplicateSubmission()
    {
        await using var test = new ChatTest();
        test.Backend.BlockStream = true;
        var cut = test.Context.Render<AgentChat>();
        cut.WaitForAssertion(() => Assert.False(cut.Find("textarea").HasAttribute("disabled")));
        cut.Find("textarea").Input("Hello");
        Task submit = cut.Find("form").SubmitAsync(EventArgs.Empty);
        await test.Backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Partial", cut.Markup);
            Assert.Contains("Tools running: 1", cut.Markup);
            Assert.True(cut.Find("textarea").HasAttribute("disabled"));
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => test.Workspace.SendAsync("duplicate"));
        test.Backend.Release.TrySetResult();
        await submit;

        cut.WaitForAssertion(() => Assert.Contains("Response complete.", cut.Markup));
        Assert.Contains("guide.pdf", cut.Markup);
        Assert.Contains("Tokens: 2 input, 3 output.", cut.Markup);
        Assert.Empty(cut.FindComponent<ChatComposer>().Instance.Value);
        Assert.Equal(1, test.Backend.CreateCalls);
        Assert.Equal(1, test.Backend.SendCalls);
        Assert.Equal(test.Owner.Current.Marker, test.Backend.Conversations["conv_1"].OwnerMarker);
        Assert.Equal("conv_1", test.Bookmark.SavedId);
        Assert.Equal(test.Owner.Current.Marker, test.Bookmark.SavedOwner);
    }

    [Fact]
    public async Task CancellationPreservesPartialOutputAndNewChatClearsOnlyTheBookmark()
    {
        await using var test = new ChatTest();
        test.Backend.BlockStream = true;
        var cut = test.Context.Render<AgentChat>();
        cut.WaitForAssertion(() => Assert.True(test.Workspace.CanSend));
        cut.Find("textarea").Input("Hello");
        Task submit = cut.Find("form").SubmitAsync(EventArgs.Empty);
        await test.Backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await cut.FindAll("button").Single(button => button.TextContent == "Stop response").ClickAsync(new MouseEventArgs());
        await submit;

        Assert.Contains("Partial", cut.Markup);
        Assert.Contains("Response canceled.", cut.Markup);
        Assert.Contains("not retried", cut.Markup);
        Assert.True(cut.Find("textarea").HasAttribute("disabled"));
        await cut.FindAll("button").Single(button => button.TextContent == "New chat").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.False(cut.Find("textarea").HasAttribute("disabled")));
        Assert.Null(test.Bookmark.SavedId);
        Assert.Empty(test.Workspace.Session?.Messages ?? []);
        Assert.Single(test.Backend.Conversations);
        Assert.Equal(1, test.Backend.CreateCalls);
        Assert.Equal(1, test.Backend.SendCalls);
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public async Task RestoresOwnedHistoryUpToTheApprovedLimitWithoutSending(int count, bool blocked)
    {
        await using var test = new ChatTest();
        test.SaveExisting(test.Owner.Current.Marker);
        test.Backend.History = Enumerable.Range(1, count).Select(index =>
            new ConversationHistoryMessage($"message_{index}", ConversationMessageRole.Assistant, $"Answer {index}", [])).ToArray();

        var cut = test.Context.Render<AgentChat>();

        cut.WaitForAssertion(() => Assert.True(test.Workspace.IsInitialized));
        Assert.Equal(!blocked, test.Workspace.CanSend);
        if (blocked)
        {
            Assert.Contains("100-message restoration limit", cut.Find("[role=alert]").TextContent);
            Assert.Empty(cut.FindAll("li.message"));
        }
        else
        {
            Assert.Equal(100, cut.FindAll("li.message").Count);
            Assert.Contains("Answer 100", cut.Markup);
        }

        Assert.Equal(0, test.Backend.CreateCalls);
        Assert.Equal(0, test.Backend.SendCalls);
        Assert.Equal(count, test.Backend.HistoryItemsRead);
    }

    [Theory]
    [InlineData("different-owner", true)]
    [InlineData(null, true)]
    [InlineData(null, false)]
    public async Task UnauthorizedSavedConversationIsNotReadOrAdopted(string? marker, bool exists)
    {
        await using var test = new ChatTest();
        test.SaveExisting(marker);
        if (!exists)
        {
            test.Backend.Conversations.Remove("saved");
        }

        var cut = test.Context.Render<AgentChat>();

        cut.WaitForAssertion(() => Assert.Contains("conversation is unavailable", cut.Find("[role=alert]").TextContent));
        Assert.False(test.Workspace.CanSend);
        Assert.Equal(0, test.Backend.HistoryItemsRead);
        Assert.Equal(0, test.Backend.SendCalls);
        Assert.Equal(marker, test.Backend.Conversations.GetValueOrDefault("saved")?.OwnerMarker);
        Assert.Equal("saved", test.Bookmark.SavedId);
        await cut.FindAll("button").Single(button => button.TextContent == "New chat").ClickAsync(new MouseEventArgs());
        Assert.Null(test.Bookmark.SavedId);
        Assert.Equal(exists ? 1 : 0, test.Backend.Conversations.Count);
        Assert.Equal(marker, test.Backend.Conversations.GetValueOrDefault("saved")?.OwnerMarker);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SessionEndingDisablesChatAndOffersFullPageSignIn(bool streaming)
    {
        await using var test = new ChatTest();
        test.Backend.BlockStream = true;
        var cut = test.Context.Render<AgentChat>();
        cut.WaitForAssertion(() => Assert.True(test.Workspace.CanSend));
        Task submit = Task.CompletedTask;
        if (streaming)
        {
            cut.Find("textarea").Input("Hello");
            submit = cut.Find("form").SubmitAsync(EventArgs.Empty);
            await test.Backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        await test.Ended.CancelAsync();
        await submit;

        cut.WaitForAssertion(() => Assert.Contains("session has ended", cut.Find("[role=alert]").TextContent));
        if (streaming)
        {
            Assert.Contains("Partial", cut.Markup);
        }

        Assert.Equal("/", cut.Find("a").GetAttribute("href"));
        Assert.Equal("false", cut.Find("a").GetAttribute("data-enhance-nav"));
        Assert.True(cut.Find("textarea").HasAttribute("disabled"));
        Assert.Equal(streaming ? 1 : 0, test.Backend.SendCalls);
    }

    [Fact]
    public async Task SessionEndedMessageWinsWhenTheSessionWatcherHasNotResumed()
    {
        await using var test = new ChatTest();
        test.Backend.BlockStream = true;
        var deferred = new DeferredSynchronizationContext();
        var previous = SynchronizationContext.Current;
        Task initialize;
        try
        {
            SynchronizationContext.SetSynchronizationContext(deferred);
            initialize = test.Workspace.InitializeAsync();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await initialize;
        try
        {
            Task send = test.Workspace.SendAsync("Hello");
            await test.Backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

            await test.Ended.CancelAsync();
            await send.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(test.Workspace.SessionEnded);
            Assert.Equal("Your application session has ended. Sign in again to continue.", test.Workspace.Error);
            Assert.False(test.Workspace.CanSend);
        }
        finally
        {
            await test.Ended.CancelAsync();
            await deferred.Posted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            deferred.Drain();
        }
    }

    [Theory]
    [InlineData(4000, true)]
    [InlineData(4001, false)]
    public async Task EnforcesTheMessageLimitBeforeAnyProviderWork(int length, bool accepted)
    {
        await using var test = new ChatTest();
        var cut = test.Context.Render<AgentChat>();
        cut.WaitForAssertion(() => Assert.True(test.Workspace.CanSend));
        bool result = false;

        await cut.InvokeAsync(async () => result = await test.Workspace.SendAsync(new string('x', length)));

        Assert.Equal(accepted, result);
        Assert.Equal(accepted ? 1 : 0, test.Backend.SendCalls);
        Assert.Equal(accepted ? 1 : 0, test.Backend.CreateCalls);
        Assert.Equal("4000", cut.Find("textarea").GetAttribute("maxlength"));
        if (!accepted)
        {
            Assert.Contains("4,000", cut.Find("[role=alert]").TextContent);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreationOrStorageFailurePreservesDraftAndNeverSendsOrLeaksBackendDetails(bool storageFailure)
    {
        await using var test = new ChatTest();
        test.Bookmark.FailSave = storageFailure;
        if (!storageFailure)
        {
            test.Backend.OnCreate = () => throw new IOException("private storage details");
        }
        var cut = test.Context.Render<AgentChat>();
        cut.WaitForAssertion(() => Assert.True(test.Workspace.CanSend));
        cut.Find("textarea").Input("Keep my draft");

        await cut.Find("form").SubmitAsync(EventArgs.Empty);

        Assert.Equal("Keep my draft", cut.FindComponent<ChatComposer>().Instance.Value);
        Assert.Contains("Keep my draft", cut.Markup);
        Assert.NotEmpty(cut.Find("[role=alert]").TextContent);
        Assert.DoesNotContain("private storage details", cut.Markup);
        Assert.Equal(1, test.Backend.CreateCalls);
        Assert.Equal(0, test.Backend.SendCalls);
        Assert.Null(test.Bookmark.SavedId);
    }

    [Fact]
    public async Task TimeoutCancelsWithoutRetryingAndKeepsPartialOutput()
    {
        await using var test = new ChatTest(TimeSpan.FromMilliseconds(250));
        test.Backend.BlockStream = true;
        var cut = test.Context.Render<AgentChat>();
        cut.WaitForAssertion(() => Assert.True(test.Workspace.CanSend));
        cut.Find("textarea").Input("Hello");

        await cut.Find("form").SubmitAsync(EventArgs.Empty);

        cut.WaitForAssertion(() => Assert.Contains("timed out", cut.Find("[role=alert]").TextContent));
        Assert.Contains("Partial", cut.Markup);
        Assert.Equal(1, test.Backend.SendCalls);
        Assert.False(test.Workspace.CanSend);
    }

    [Fact]
    public async Task ChangedIdentityCannotPersistOrSendUnderThePreviousOwner()
    {
        await using var test = new ChatTest();
        test.Backend.OnCreate = () => test.Owner.Current = new ConversationOwner(Guid.NewGuid(), Guid.NewGuid(), test.Ended.Token);
        var cut = test.Context.Render<AgentChat>();
        cut.WaitForAssertion(() => Assert.True(test.Workspace.CanSend));
        cut.Find("textarea").Input("Hello");

        await cut.Find("form").SubmitAsync(EventArgs.Empty);

        Assert.NotEmpty(cut.Find("[role=alert]").TextContent);
        Assert.Null(test.Bookmark.SavedId);
        Assert.Equal(0, test.Backend.SendCalls);
    }

    [Fact]
    public async Task ChangedSessionDuringHistoryRestorationDoesNotDisplayTheOldTranscript()
    {
        await using var test = new ChatTest();
        test.SaveExisting(test.Owner.Current.Marker);
        test.Backend.History = [new ConversationHistoryMessage("item", ConversationMessageRole.Assistant, "Old private answer", [])];
        test.Backend.OnHistoryCompleted = () =>
            test.Owner.Current = new ConversationOwner(Guid.NewGuid(), Guid.NewGuid(), test.Ended.Token);

        var cut = test.Context.Render<AgentChat>();

        cut.WaitForAssertion(() => Assert.True(test.Workspace.IsInitialized));
        Assert.True(test.Workspace.SessionEnded);
        Assert.DoesNotContain("Old private answer", cut.Markup);
        Assert.False(test.Workspace.CanSend);
    }

    [Fact]
    public async Task RestoredProviderContentIsRenderedAsTextNotMarkup()
    {
        await using var test = new ChatTest();
        test.SaveExisting(test.Owner.Current.Marker);
        test.Backend.History =
        [
            new ConversationHistoryMessage("item", ConversationMessageRole.Assistant,
                "<img src=x onerror=alert(1)>", [new Citation("<script>untrusted</script>", null)])
        ];

        var cut = test.Context.Render<AgentChat>();

        cut.WaitForAssertion(() => Assert.True(test.Workspace.IsInitialized));
        Assert.Empty(cut.FindAll("img, script"));
        Assert.Contains("<img src=x onerror=alert(1)>", cut.Find(".message-text").TextContent);
        Assert.Contains("<script>untrusted</script>", cut.Find(".citations").TextContent);
    }

    [Fact]
    public async Task DisposingTheComponentCancelsItsActiveRequest()
    {
        var test = new ChatTest();
        test.Backend.BlockStream = true;
        var cut = test.Context.Render<AgentChat>();
        cut.WaitForAssertion(() => Assert.True(test.Workspace.CanSend));
        cut.Find("textarea").Input("Hello");
        Task submit = cut.Find("form").SubmitAsync(EventArgs.Empty);
        await test.Backend.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await test.DisposeAsync();
        await submit.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(test.Backend.StreamToken.IsCancellationRequested);
        Assert.Equal(1, test.Backend.SendCalls);
    }

    [Fact]
    public async Task BookmarkIsProtectedAndBoundToItsOwnerStorageKey()
    {
        await using var context = new BunitContext();
        context.JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();
        var storage = new ConversationBookmark(new ProtectedSessionStorage(
            context.JSInterop.JSRuntime, new EphemeralDataProtectionProvider()));

        await storage.SetAsync("owner", "conv_private", default);

        var invocation = Assert.Single(context.JSInterop.Invocations);
        var ciphertext = Assert.IsType<string>(invocation.Arguments[1]);
        Assert.DoesNotContain("conv_private", ciphertext);
        context.JSInterop.Setup<string>("sessionStorage.getItem", "aiagents:conversation:v1:owner").SetResult(ciphertext);
        Assert.Equal("conv_private", await storage.GetAsync("owner", default));
        context.JSInterop.Setup<string>("sessionStorage.getItem", "aiagents:conversation:v1:other").SetResult(ciphertext);
        await Assert.ThrowsAsync<CryptographicException>(() => storage.GetAsync("other", default));
    }

    private sealed class DeferredSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> _callbacks = new();
        public TaskCompletionSource Posted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Post(SendOrPostCallback callback, object? state)
        {
            _callbacks.Enqueue(() => callback(state));
            Posted.TrySetResult();
        }

        public void Drain()
        {
            while (_callbacks.TryDequeue(out var callback))
            {
                callback();
            }
        }
    }

    private sealed class ChatTest : IAsyncDisposable
    {
        public BunitContext Context { get; } = new();
        public CancellationTokenSource Ended { get; } = new();
        public TestOwner Owner { get; }
        public TestBackend Backend { get; }
        public TestBookmark Bookmark { get; } = new();
        public ChatWorkspace Workspace => Context.Services.GetRequiredService<ChatWorkspace>();

        public ChatTest(TimeSpan? timeout = null)
        {
            Owner = new TestOwner(new ConversationOwner(Guid.NewGuid(), Guid.NewGuid(), Ended.Token));
            Backend = new TestBackend(Owner);
            Context.Services.AddLogging();
            Context.Services.AddSingleton<ICurrentConversationOwner>(Owner);
            Context.Services.AddSingleton<IAgentClient>(Backend);
            Context.Services.AddSingleton<IConversationStore>(Backend);
            Context.Services.AddSingleton<IConversationBookmark>(Bookmark);
            Context.Services.AddSingleton(TimeProvider.System);
            Context.Services.AddSingleton(Options.Create(new AgentChatOptions
            {
                OperationTimeout = timeout ?? TimeSpan.FromMinutes(2)
            }));
            Context.Services.AddTransient<AgentConversationService>();
            Context.Services.AddSingleton<ChatWorkspace>();
        }

        public void SaveExisting(string? marker)
        {
            Bookmark.SavedId = "saved";
            Backend.Conversations.Add("saved", new StoredConversation("saved", marker));
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            Ended.Dispose();
        }
    }

    private sealed class TestOwner(ConversationOwner owner) : ICurrentConversationOwner
    {
        public ConversationOwner Current { get; set; } = owner;
        public Task<ConversationOwner> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Current);
        }
    }

    private sealed class TestBookmark : IConversationBookmark
    {
        public string? SavedId { get; set; }
        public string? SavedOwner { get; private set; }
        public bool FailSave { get; set; }
        public Task<string?> GetAsync(string ownerMarker, CancellationToken cancellationToken) => Task.FromResult(SavedId);
        public Task SetAsync(string ownerMarker, string conversationId, CancellationToken cancellationToken)
        {
            if (FailSave)
            {
                throw new IOException("private storage details");
            }

            SavedOwner = ownerMarker;
            SavedId = conversationId;
            return Task.CompletedTask;
        }

        public Task ClearAsync(string ownerMarker, CancellationToken cancellationToken)
        {
            SavedId = null;
            return Task.CompletedTask;
        }
    }

    private sealed class TestBackend(TestOwner owner) : IAgentClient, IConversationStore
    {
        public Dictionary<string, StoredConversation> Conversations { get; } = [];
        public IReadOnlyList<ConversationHistoryMessage> History { get; set; } = [];
        public int CreateCalls { get; private set; }
        public int SendCalls { get; private set; }
        public int HistoryItemsRead { get; private set; }
        public bool BlockStream { get; set; }
        public Action? OnCreate { get; set; }
        public Action? OnHistoryCompleted { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken StreamToken { get; private set; }

        public Task<StoredConversation> CreateAsync(string ownerMarker, CancellationToken cancellationToken)
        {
            var conversation = new StoredConversation($"conv_{++CreateCalls}", ownerMarker);
            Conversations.Add(conversation.Id, conversation);
            OnCreate?.Invoke();
            return Task.FromResult(conversation);
        }

        public Task<StoredConversation?> GetAsync(string conversationId, CancellationToken cancellationToken) =>
            Task.FromResult(Conversations.GetValueOrDefault(conversationId));

        public async IAsyncEnumerable<ConversationHistoryMessage> GetHistoryAsync(
            string conversationId, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var message in History)
            {
                cancellationToken.ThrowIfCancellationRequested();
                HistoryItemsRead++;
                yield return message;
                await Task.Yield();
            }

            OnHistoryCompleted?.Invoke();
        }

        public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
            string conversationId, string message, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Assert.Equal(owner.Current.Marker, Conversations[conversationId].OwnerMarker);
            SendCalls++;
            StreamToken = cancellationToken;
            yield return new TextDelta("Partial");
            yield return new ToolCallStarted("tool", "search");
            Started.TrySetResult();
            if (BlockStream)
            {
                await Release.Task.WaitAsync(cancellationToken);
            }

            yield return new ToolCallCompleted("tool");
            yield return new CompletedText("Complete answer");
            yield return new Citation("guide.pdf", null);
            yield return new UsageInformation(2, 3);
            yield return new AgentCompleted();
        }
    }
}
