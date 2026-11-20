using AiAgents.Application;
using AiAgents.Web.Components.Chat;
using Microsoft.Extensions.Options;

namespace AiAgents.Web.Chat;

public sealed class ChatWorkspace(
    AgentConversationService conversations,
    ICurrentConversationOwner currentOwner,
    IConversationBookmark bookmark,
    IOptions<AgentChatOptions> options,
    TimeProvider clock,
    ILoggerFactory loggerFactory) : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ILogger _logger = loggerFactory.CreateLogger<ChatWorkspace>();
    private CancellationToken _sessionEnded;
    private string? _ownerMarker;
    private Task? _sessionWatcher;
    private bool _initializing;
    private bool _blocked;
    private bool _authenticationEnded;
    private bool _disposed;

    public event Func<Task>? Changed;

    public ChatSession? Session { get; private set; }
    public bool IsInitialized { get; private set; }
    public bool IsBusy { get; private set; }
    public bool SessionEnded => _authenticationEnded || _sessionEnded.IsCancellationRequested;
    public string? Error { get; private set; }
    public bool CanSend => IsInitialized && _ownerMarker is not null && !IsBusy && !SessionEnded && !_blocked && !_disposed;
    public bool CanStartNewChat => IsInitialized && _ownerMarker is not null && !IsBusy && !SessionEnded && !_disposed;

    public async Task InitializeAsync()
    {
        if (IsInitialized || _initializing)
        {
            return;
        }

        _initializing = true;
        await RunAsync(async token =>
        {
            var owner = await ResolveOwnerAsync(token);
            _ownerMarker = owner.Marker;
            _sessionEnded = owner.SessionEnded;
            _sessionWatcher = ObserveSessionAsync();
            string? id = await bookmark.GetAsync(owner.Marker, token);
            if (id is not null)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new InvalidDataException("The saved conversation identifier is invalid.");
                }

                List<ChatMessage> messages = [];
                await foreach (var item in conversations.GetHistoryAsync(id, token))
                {
                    if (messages.Count == AgentChatOptions.HistoryMessageLimit)
                    {
                        throw new HistoryLimitException();
                    }

                    messages.Add(new ChatMessage(
                        item.Role == ConversationMessageRole.User ? ChatMessageRole.User : ChatMessageRole.Assistant,
                        item.Text, [.. item.Citations.Distinct()]));
                }

                await EnsureCurrentSessionAsync(token);
                SetSession(id, messages);
            }
        });
        IsInitialized = true;
        _initializing = false;
        await NotifyAsync();
    }

    public async Task<bool> SendAsync(string message)
    {
        if (!CanSend)
        {
            throw new InvalidOperationException("The conversation is not ready to send.");
        }

        if (string.IsNullOrWhiteSpace(message) || message.Length > AgentChatOptions.MessageCharacterLimit)
        {
            Error = $"Enter a message between 1 and {AgentChatOptions.MessageCharacterLimit:N0} characters.";
            await NotifyAsync();
            return false;
        }

        bool accepted = false;
        await RunAsync(async token =>
        {
            if (Session is null)
            {
                string id = await conversations.CreateAsync(token);
                await EnsureCurrentSessionAsync(token);
                await bookmark.SetAsync(RequiredOwner(), id, token);
                SetSession(id, []);
            }

            await EnsureCurrentSessionAsync(token);
            var session = Session ?? throw new InvalidOperationException("The conversation was not initialized.");
            accepted = true;
            await session.SendAsync(message, token);
            token.ThrowIfCancellationRequested();
            if (Session.AccessDenied)
            {
                throw new ConversationAccessException();
            }

            if (Session.Status is ConversationStatus.Failed or ConversationStatus.Canceled)
            {
                _blocked = true;
                Error = "The response stopped. Partial output is preserved. Start a new chat before sending again; the request was not retried.";
            }
        });
        return accepted;
    }

    public Task StartNewChatAsync()
    {
        if (!CanStartNewChat)
        {
            throw new InvalidOperationException("A new conversation cannot be started now.");
        }

        return RunAsync(async token =>
        {
            await bookmark.ClearAsync(RequiredOwner(), token);
            if (Session is { } previous)
            {
                previous.Changed -= NotifyAsync;
            }

            Session = null;
            _blocked = false;
        });
    }

    public Task CancelAsync() => Session?.CancelAsync() ?? Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _lifetime.CancelAsync();
        if (Session is { } session)
        {
            session.Changed -= NotifyAsync;
            await session.CancelAsync();
        }

        if (_sessionWatcher is { } watcher)
        {
            await watcher;
        }

        _lifetime.Dispose();
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsBusy)
        {
            throw new InvalidOperationException("A conversation operation is already in progress.");
        }

        IsBusy = true;
        Error = null;
        using var timeout = new CancellationTokenSource(options.Value.OperationTimeout, clock);
        using var request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, timeout.Token);
        try
        {
            await NotifyAsync();
            if (_ownerMarker is not null)
            {
                await EnsureCurrentSessionAsync(request.Token);
            }

            await action(request.Token);
            request.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (_disposed)
        {
            // Component disposal cancels local work without rendering another update.
        }
        catch (OperationCanceledException) when (SessionEnded)
        {
            SetSessionEnded();
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            _blocked = true;
            Error = "The operation timed out. Start a new chat before sending again. It was not retried; remote work may still be running.";
            _logger.LogWarning("A chat operation timed out.");
        }
        catch (ConversationAccessException)
        {
            _blocked = true;
            Error = "The conversation is unavailable. Start a new chat or sign in again.";
            _logger.LogWarning("Conversation access was denied.");
        }
        catch (HistoryLimitException)
        {
            _blocked = true;
            Error = "This conversation exceeds the 100-message restoration limit. Start a new chat.";
            _logger.LogWarning("Conversation history exceeded the restoration limit.");
        }
        catch (Exception exception)
        {
            _blocked = true;
            Error = "The conversation could not be loaded or updated. Check browser storage, Azure developer sign-in and Foundry access, then start a new chat. No request was retried.";
            _logger.LogWarning("A chat operation failed with exception type {ExceptionType}.", exception.GetType().Name);
        }
        finally
        {
            IsBusy = false;
            if (!_disposed && SessionEnded)
            {
                SetSessionEnded();
            }

            await NotifyAsync();
        }
    }

    private async Task ObserveSessionAsync()
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, _sessionEnded).WaitAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_disposed)
        {
            return;
        }
        catch (OperationCanceledException) when (SessionEnded)
        {
            SetSessionEnded();
            await _lifetime.CancelAsync();
            await NotifyAsync();
        }
    }

    private void SetSessionEnded()
    {
        _authenticationEnded = true;
        _blocked = true;
        Error = "Your application session has ended. Sign in again to continue.";
    }

    private string RequiredOwner() =>
        _ownerMarker ?? throw new InvalidOperationException("The conversation owner has not been resolved.");

    private async Task<ConversationOwner> ResolveOwnerAsync(CancellationToken token)
    {
        try
        {
            return await currentOwner.GetAsync(token);
        }
        catch (ConversationAccessException)
        {
            SetSessionEnded();
            await _lifetime.CancelAsync();
            throw new OperationCanceledException(_lifetime.Token);
        }
    }

    private async Task EnsureCurrentSessionAsync(CancellationToken token)
    {
        var owner = await ResolveOwnerAsync(token);
        if (owner.Marker != _ownerMarker || owner.SessionEnded != _sessionEnded)
        {
            SetSessionEnded();
            await _lifetime.CancelAsync();
            throw new OperationCanceledException(_lifetime.Token);
        }
    }

    private void SetSession(string id, IReadOnlyList<ChatMessage> messages)
    {
        Session = new ChatSession(conversations, loggerFactory.CreateLogger<ChatSession>(), id, messages);
        Session.Changed += NotifyAsync;
    }

    private async Task NotifyAsync()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var handler in Changed?.GetInvocationList().Cast<Func<Task>>() ?? [])
        {
            await handler();
        }
    }

    private sealed class HistoryLimitException : Exception;
}
