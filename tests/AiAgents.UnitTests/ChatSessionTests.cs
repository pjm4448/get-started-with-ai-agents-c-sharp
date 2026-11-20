using System.Runtime.CompilerServices;
using AiAgents.Application;
using AiAgents.Web.Components.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiAgents.UnitTests;

public sealed class ChatSessionTests
{
    [Fact]
    public async Task PreservesFileCitationIdentityAndDeduplicatesRepeatedAnnotations()
    {
        var citation = new Citation("product_info_1.md", null, "file_1");
        var session = CreateSession(new SequenceClient(
            [new TextDelta("Answer"), citation, citation, new AgentCompleted()]));

        await session.SendAsync("Question");

        Assert.Equal(citation, Assert.Single(session.Messages[^1].Citations));
        Assert.Equal(ConversationStatus.Completed, session.Status);
    }

    [Fact]
    public async Task AppliesStreamingEventsAndPreservesConversationTurns()
    {
        var client = new SequenceClient(
        [
            new TextDelta("Part"),
            new TextDelta("ial"),
            new ToolCallStarted("tool", "search"),
            new Citation("Document", new Uri("https://example.test/secret?token=private")),
            new ToolCallCompleted("tool"),
            new CompletedText("Complete answer"),
            new UsageInformation(5, 7),
            new AgentWarning("private warning"),
            new AgentCompleted()
        ]);
        var session = CreateSession(client);
        var snapshots = new List<(string Text, int ActiveTools)>();
        session.Changed += () =>
        {
            snapshots.Add((session.Messages[^1].Text, session.ActiveToolCount));
            return Task.CompletedTask;
        };

        await session.SendAsync("Question");

        Assert.Equal(ConversationStatus.Completed, session.Status);
        Assert.Collection(session.Messages,
            message =>
            {
                Assert.Equal(ChatMessageRole.User, message.Role);
                Assert.Equal("Question", message.Text);
            },
            message =>
            {
                Assert.Equal(ChatMessageRole.Assistant, message.Role);
                Assert.Equal("Complete answer", message.Text);
                Assert.Equal([new Citation("Document", new Uri("https://example.test/secret?token=private"))], message.Citations);
            });
        Assert.Contains(("Part", 0), snapshots);
        Assert.Contains(("Partial", 1), snapshots);
        Assert.True(session.HasWarning);
        Assert.Equal(new UsageInformation(5, 7), session.Usage);
        Assert.Equal(0, session.ActiveToolCount);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task IncompleteStreamIsAnErrorAndKeepsPartialOutput()
    {
        var client = new SequenceClient([new TextDelta("Partial")]);
        var session = CreateSession(client);

        await session.SendAsync("Question");

        Assert.Equal(ConversationStatus.Failed, session.Status);
        Assert.Equal("Partial", session.Messages[^1].Text);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task AgentErrorsDoNotExposeOrLogBackendDetails()
    {
        var logger = new RecordingLogger();
        var client = new SequenceClient([new TextDelta("Partial"), new AgentError("private backend details")]);
        var session = new ChatSession(AgentConversationServiceTests.CreateService(client), logger, "conversation");

        await session.SendAsync("sensitive question");

        Assert.Equal(ConversationStatus.Failed, session.Status);
        Assert.Equal("Partial", session.Messages[^1].Text);
        Assert.Contains(logger.Messages, entry => entry.Contains("Agent reported an error.", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, entry => entry.Contains("private", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, entry => entry.Contains("sensitive", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CancellationPreservesPartialOutputAndRejectsDuplicateSubmission()
    {
        var client = new BlockingClient();
        var session = CreateSession(client);
        Task first = session.SendAsync("First");
        await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SendAsync("Second"));
        await session.CancelAsync();
        await first.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ConversationStatus.Canceled, session.Status);
        Assert.Equal("Partial", session.Messages[^1].Text);
        Assert.Equal(2, session.Messages.Count);
        Assert.Equal(1, client.Calls);
    }

    private static ChatSession CreateSession(IAgentClient client) =>
        new(AgentConversationServiceTests.CreateService(client), NullLogger<ChatSession>.Instance, "conversation");

    private sealed class SequenceClient(IReadOnlyList<AgentStreamEvent> events) : IAgentClient
    {
        public int Calls { get; private set; }

        public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
            string conversationId,
            string message,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Calls++;
            foreach (var item in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }
    }

    private sealed class BlockingClient : IAgentClient
    {
        public int Calls { get; private set; }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<AgentStreamEvent> StreamAsync(
            string conversationId,
            string message,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Calls++;
            yield return new TextDelta("Partial");
            Started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class RecordingLogger : ILogger<ChatSession>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
