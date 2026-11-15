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
        var service = new AgentConversationService(client);

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
        var service = new AgentConversationService(client);
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
        var service = new AgentConversationService(client);
        using var cancellation = new CancellationTokenSource();
        await using var stream = service.StreamAsync("conversation", "hello", cancellation.Token)
            .GetAsyncEnumerator();

        Assert.True(await stream.MoveNextAsync());
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await stream.MoveNextAsync());
        Assert.Equal(1, client.Calls);
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
