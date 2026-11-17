using System.Runtime.CompilerServices;
using AiAgents.Application;

namespace AiAgents.UnitTests;

public sealed class AgentConversationServiceTests
{
    [Theory]
    [InlineData("", "hello")]
    [InlineData("conversation", " ")]
    public async Task RejectsInvalidInputBeforeCallingAgent(string conversationId, string message)
    {
        var client = new RecordingAgentClient();
        var service = CreateService(client);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await foreach (var _ in service.StreamAsync(conversationId, message))
            {
            }
        });
        Assert.False(client.Called);
    }

    [Fact]
    public async Task PreservesStreamEvents()
    {
        var client = new RecordingAgentClient();
        var service = CreateService(client);
        var events = new List<AgentStreamEvent>();

        await foreach (var item in service.StreamAsync("conversation", "hello"))
        {
            events.Add(item);
        }

        Assert.Equal([new TextDelta("partial"), new AgentCompleted()], events);
        Assert.True(client.Called);
    }

    [Fact]
    public async Task PropagatesCancellationWithoutRetrying()
    {
        var client = new RecordingAgentClient();
        var service = CreateService(client);
        using var cancellation = new CancellationTokenSource();
        await using var stream = service.StreamAsync("conversation", "hello", cancellation.Token)
            .GetAsyncEnumerator();

        Assert.True(await stream.MoveNextAsync());
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await stream.MoveNextAsync());
        Assert.Equal(1, client.Calls);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unowned")]
    [InlineData("different-user")]
    [InlineData("different-tenant")]
    [InlineData("different-id")]
    public async Task DeniedConversationsHaveTheSameErrorAndNeverReachAgentOrHistory(string scenario)
    {
        var context = new ConversationContext();
        context.Conversation = scenario switch
        {
            "missing" => null,
            "unowned" => new("conversation", null),
            "different-user" => new("conversation", new ConversationOwner(TenantId, Guid.NewGuid()).Marker),
            "different-tenant" => new("conversation", new ConversationOwner(Guid.NewGuid(), ObjectId).Marker),
            _ => new("another-conversation", context.Owner.Marker)
        };
        var client = new RecordingAgentClient();
        var service = context.CreateService(client);

        var streamError = await Assert.ThrowsAsync<ConversationAccessException>(
            async () => await service.StreamAsync("conversation", "hello").ToListAsync());
        var historyError = await Assert.ThrowsAsync<ConversationAccessException>(
            async () => await service.GetHistoryAsync("conversation").ToListAsync());

        Assert.Equal("The conversation is not available.", streamError.Message);
        Assert.Equal(streamError.Message, historyError.Message);
        Assert.Equal(0, client.Calls);
        Assert.Equal(0, context.HistoryCalls);
        Assert.Equal(0, context.CreateCalls);
    }

    [Fact]
    public async Task OwnerCanCreateSendAndReadHistory()
    {
        var context = new ConversationContext();
        var client = new RecordingAgentClient();
        var service = context.CreateService(client);

        Assert.Equal("conversation", await service.CreateAsync());
        Assert.Equal(context.Owner.Marker, context.CreatedOwner);
        await service.StreamAsync("conversation", "hello").ToListAsync();
        var history = await service.GetHistoryAsync("conversation").ToListAsync();

        Assert.Equal("Stored answer", Assert.Single(history).Text);
        Assert.Equal(1, context.CreateCalls);
        Assert.Equal(1, context.HistoryCalls);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task OwnershipIsResolvedAgainOnSubsequentOperationsAndDuringLookup()
    {
        var context = new ConversationContext();
        var client = new RecordingAgentClient();
        var service = context.CreateService(client);
        await service.StreamAsync("conversation", "hello").ToListAsync();

        context.OnGet = () => context.Owner = new ConversationOwner(TenantId, Guid.NewGuid());
        await Assert.ThrowsAsync<ConversationAccessException>(
            async () => await service.StreamAsync("conversation", "hello").ToListAsync());
        await Assert.ThrowsAsync<ConversationAccessException>(
            async () => await service.GetHistoryAsync("conversation").ToListAsync());

        Assert.Equal(1, client.Calls);
        Assert.Equal(0, context.HistoryCalls);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("send")]
    [InlineData("history")]
    public async Task MissingAuthenticationAndPreCancellationNeverAccessStorage(string operation)
    {
        var context = new ConversationContext { OwnerError = new ConversationAccessException() };
        var client = new RecordingAgentClient();
        var service = context.CreateService(client);
        Task Execute(CancellationToken token) => operation switch
        {
            "create" => service.CreateAsync(token),
            "send" => service.StreamAsync("conversation", "hello", token).ToListAsync().AsTask(),
            _ => service.GetHistoryAsync("conversation", token).ToListAsync().AsTask()
        };

        await Assert.ThrowsAsync<ConversationAccessException>(() => Execute(default));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Execute(cancellation.Token));

        Assert.Equal(0, context.GetCalls);
        Assert.Equal(0, context.CreateCalls);
        Assert.Equal(0, context.HistoryCalls);
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task ProviderFailuresAreNotRetriedOrAdopted()
    {
        var failure = new IOException("provider failure");
        var context = new ConversationContext { StoreError = failure };
        var client = new RecordingAgentClient();
        var service = context.CreateService(client);

        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(
            async () => await service.StreamAsync("conversation", "hello").ToListAsync()));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(
            async () => await service.GetHistoryAsync("conversation").ToListAsync()));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => service.CreateAsync()));
        Assert.Equal(2, context.GetCalls);
        Assert.Equal(1, context.CreateCalls);
        Assert.Equal(0, context.HistoryCalls);
        Assert.Equal(0, client.Calls);
    }

    [Theory]
    [InlineData(null, "unowned")]
    [InlineData("another-owner", "owned")]
    [InlineData("matching-owner", "")]
    public async Task RejectsCreationWhenTheProviderDoesNotPreserveOwnership(string? marker, string id)
    {
        var context = new ConversationContext
        {
            CreatedConversation = new StoredConversation(id,
                marker == "matching-owner" ? new ConversationOwner(TenantId, ObjectId).Marker : marker)
        };
        var service = context.CreateService(new RecordingAgentClient());

        await Assert.ThrowsAsync<InvalidDataException>(() => service.CreateAsync());
        Assert.Equal(1, context.CreateCalls);
    }

    [Fact]
    public async Task SignOutDuringCreationDoesNotReturnTheCreatedConversation()
    {
        var context = new ConversationContext();
        context.OnCreate = () => context.OwnerError = new ConversationAccessException();
        var service = context.CreateService(new RecordingAgentClient());

        await Assert.ThrowsAsync<ConversationAccessException>(() => service.CreateAsync());
        Assert.Equal(1, context.CreateCalls);
    }

    [Fact]
    public async Task CancellationDuringOwnershipLookupNeverInvokesTheAgent()
    {
        using var cancellation = new CancellationTokenSource();
        var context = new ConversationContext { OnGet = cancellation.Cancel };
        var client = new RecordingAgentClient();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await context.CreateService(client).StreamAsync("conversation", "hello", cancellation.Token).ToListAsync());
        Assert.Equal(0, client.Calls);
        Assert.Equal(1, context.GetCalls);
    }

    internal static AgentConversationService CreateService(IAgentClient client) =>
        new ConversationContext().CreateService(client);

    private static readonly Guid TenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ObjectId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    private sealed class ConversationContext : IConversationStore, ICurrentConversationOwner
    {
        public ConversationOwner Owner { get; set; } = new(TenantId, ObjectId);
        public StoredConversation? Conversation { get; set; } = new("conversation", new ConversationOwner(TenantId, ObjectId).Marker);
        public Exception? OwnerError { get; set; }
        public Exception? StoreError { get; init; }
        public Action? OnGet { get; set; }
        public Action? OnCreate { get; set; }
        public StoredConversation? CreatedConversation { get; init; }
        public string? CreatedOwner { get; private set; }
        public int CreateCalls { get; private set; }
        public int GetCalls { get; private set; }
        public int HistoryCalls { get; private set; }

        public AgentConversationService CreateService(IAgentClient client) => new(client, this, this);

        public Task<ConversationOwner> GetAsync(CancellationToken cancellationToken) =>
            OwnerError is { } error ? Task.FromException<ConversationOwner>(error) : Task.FromResult(Owner);

        public Task<StoredConversation> CreateAsync(string ownerMarker, CancellationToken cancellationToken)
        {
            CreateCalls++;
            CreatedOwner = ownerMarker;
            OnCreate?.Invoke();
            return StoreError is { } error
                ? Task.FromException<StoredConversation>(error)
                : Task.FromResult(CreatedConversation ?? new StoredConversation("conversation", ownerMarker));
        }

        public Task<StoredConversation?> GetAsync(string conversationId, CancellationToken cancellationToken)
        {
            GetCalls++;
            OnGet?.Invoke();
            return StoreError is { } error
                ? Task.FromException<StoredConversation?>(error)
                : Task.FromResult(Conversation);
        }

        public async IAsyncEnumerable<ConversationHistoryMessage> GetHistoryAsync(
            string conversationId, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            HistoryCalls++;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new("message", ConversationMessageRole.Assistant, "Stored answer", []);
        }
    }

    private sealed class RecordingAgentClient : IAgentClient
    {
        public int Calls { get; private set; }

        public bool Called => Calls > 0;

        public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
            string conversationId,
            string message,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Calls++;
            yield return new TextDelta("partial");
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new AgentCompleted();
        }
    }
}
