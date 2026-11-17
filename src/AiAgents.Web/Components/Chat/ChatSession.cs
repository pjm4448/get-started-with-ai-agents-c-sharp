using AiAgents.Application;

namespace AiAgents.Web.Components.Chat;

public sealed class ChatSession
{
    private readonly AgentConversationService _conversationService;
    private readonly ILogger<ChatSession> _logger;
    private readonly string _conversationId;
    private readonly List<ChatMessage> _messages = [];
    private readonly IReadOnlyList<ChatMessage> _readOnlyMessages;
    private readonly HashSet<string> _activeTools = [];
    private readonly object _gate = new();
    private CancellationTokenSource? _activeRequest;
    private Task? _cancellationTask;
    private bool _ending;

    public ChatSession(
        AgentConversationService conversationService,
        ILogger<ChatSession> logger,
        string conversationId,
        IReadOnlyList<ChatMessage>? initialMessages = null)
    {
        ArgumentNullException.ThrowIfNull(conversationService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        _conversationService = conversationService;
        _logger = logger;
        _conversationId = conversationId;
        if (initialMessages is not null)
        {
            _messages.AddRange(initialMessages);
        }

        _readOnlyMessages = _messages.AsReadOnly();
    }

    public event Func<Task>? Changed;

    public IReadOnlyList<ChatMessage> Messages => _readOnlyMessages;

    public ConversationStatus Status { get; private set; }

    public int ActiveToolCount => _activeTools.Count;

    public bool HasWarning { get; private set; }

    public bool HasStatusUpdate { get; private set; }

    public bool AccessDenied { get; private set; }

    public UsageInformation? Usage { get; private set; }

    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        CancellationTokenSource request;
        lock (_gate)
        {
            if (_activeRequest is not null || _ending)
            {
                throw new InvalidOperationException("A response is already in progress.");
            }

            request = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _activeRequest = request;
        }

        try
        {
            Status = ConversationStatus.Responding;
            HasWarning = false;
            AccessDenied = false;
            HasStatusUpdate = false;
            Usage = null;
            _activeTools.Clear();
            _messages.Add(new ChatMessage(ChatMessageRole.User, message, []));
            _messages.Add(new ChatMessage(ChatMessageRole.Assistant, string.Empty, []));
            await NotifyAsync();

            await using var stream = _conversationService
                .StreamAsync(_conversationId, message, request.Token)
                .GetAsyncEnumerator(request.Token);

            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await stream.MoveNextAsync();
                }
                catch (OperationCanceledException exception) when (
                    request.IsCancellationRequested || exception.CancellationToken.IsCancellationRequested)
                {
                    Status = ConversationStatus.Canceled;
                    break;
                }
                catch (ConversationAccessException)
                {
                    AccessDenied = true;
                    Status = ConversationStatus.Failed;
                    _logger.LogWarning("Conversation access was denied.");
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        "Agent response failed with exception type {ExceptionType}.",
                        exception.GetType().Name);
                    Status = ConversationStatus.Failed;
                    break;
                }

                if (request.IsCancellationRequested)
                {
                    Status = ConversationStatus.Canceled;
                    break;
                }

                if (!hasNext)
                {
                    _logger.LogWarning("Agent response ended without a completion event.");
                    Status = ConversationStatus.Failed;
                    break;
                }

                Apply(stream.Current);
                await NotifyAsync();
                if (Status != ConversationStatus.Responding)
                {
                    break;
                }
            }
        }
        finally
        {
            if (Status == ConversationStatus.Responding)
            {
                Status = ConversationStatus.Failed;
                _logger.LogWarning("Agent response stopped unexpectedly.");
            }

            _activeTools.Clear();
            Task? cancellationTask;
            lock (_gate)
            {
                _ending = true;
                _activeRequest = null;
                cancellationTask = _cancellationTask;
            }

            try
            {
                if (cancellationTask is not null)
                {
                    await cancellationTask;
                }
            }
            finally
            {
                request.Dispose();
                lock (_gate)
                {
                    _cancellationTask = null;
                    _ending = false;
                }

                await NotifyAsync();
            }
        }
    }

    public Task CancelAsync()
    {
        lock (_gate)
        {
            if (_activeRequest is null)
            {
                return _cancellationTask ?? Task.CompletedTask;
            }

            return _cancellationTask ??= _activeRequest.CancelAsync();
        }
    }

    private void Apply(AgentStreamEvent item)
    {
        var assistant = _messages[^1];
        switch (item)
        {
            case TextDelta delta:
                _messages[^1] = assistant with { Text = assistant.Text + delta.Text };
                break;
            case CompletedText completed:
                _messages[^1] = assistant with { Text = completed.Text };
                break;
            case Citation citation:
                _messages[^1] = assistant with { Citations = [.. assistant.Citations, citation.Label] };
                break;
            case ToolCallStarted started:
                _activeTools.Add(started.Id);
                break;
            case ToolCallCompleted completed:
                if (!_activeTools.Remove(completed.Id))
                {
                    _logger.LogWarning("Agent reported completion of an unknown tool call.");
                }

                break;
            case StatusUpdate:
                HasStatusUpdate = true;
                break;
            case UsageInformation usage:
                Usage = usage;
                break;
            case AgentWarning:
                HasWarning = true;
                break;
            case AgentError:
                _logger.LogWarning("Agent reported an error.");
                Status = ConversationStatus.Failed;
                break;
            case AgentCompleted:
                Status = ConversationStatus.Completed;
                break;
            default:
                _logger.LogWarning("Agent returned an unsupported stream event.");
                Status = ConversationStatus.Failed;
                break;
        }
    }

    private async Task NotifyAsync()
    {
        var handlers = Changed;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<Task>>())
        {
            await handler();
        }
    }
}
