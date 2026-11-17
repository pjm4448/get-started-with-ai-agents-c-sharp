using System.Text.Json;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace AiAgents.EndToEndTests;

[Trait("Category", "Browser")]
public sealed class ChatBrowserTests : IAsyncLifetime
{
    private readonly BrowserTestHost _host = new();
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private Cookie? _cookie;

    private IPage Page => _page ?? throw new InvalidOperationException("The test browser has not started.");
    private IBrowserContext Context => _context ?? throw new InvalidOperationException("The test context has not started.");

    public async Task InitializeAsync()
    {
        _host.StartServer();
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        _context = await CreateContextAsync();
        await using var health = await Context.APIRequest.GetAsync("/health");
        Assert.Equal(200, health.Status);
        _cookie = await _host.SignInAsync(Guid.NewGuid());
        await Context.AddCookiesAsync([_cookie]);
        _page = await Context.NewPageAsync();
        await Page.GotoAsync("/");
        await Expect(Page.GetByLabel("Message", new() { Exact = true })).ToBeEnabledAsync();
    }

    [Fact]
    public async Task KeyboardSubmissionStreamsOnceAndRefreshRestoresOwnedHistory()
    {
        Assert.Equal(0, _host.Backend.Creates);
        var message = Page.GetByLabel("Message", new() { Exact = true });
        var send = Page.GetByRole(AriaRole.Button, new() { Name = "Send", Exact = true });
        await message.FocusAsync();
        await message.FillAsync("A keyboard-only question");
        await message.PressAsync("Tab");
        await Expect(send).ToBeFocusedAsync();
        await send.PressAsync("Enter");

        await Expect(Page.GetByText("Partial offline answer", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Tools running: 1", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(message).ToBeDisabledAsync();
        await Expect(send).ToBeDisabledAsync();
        Assert.Equal(1, _host.Backend.Sends);
        _host.Backend.CompleteResponse();
        await Expect(Page.GetByText("Response complete.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("offline-guide.pdf", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Tokens: 5 input, 8 output.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(message).ToHaveValueAsync("");

        await Page.ReloadAsync();

        await Expect(Page.GetByText("Completed offline answer", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("A keyboard-only question", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(message).ToBeEnabledAsync();
        Assert.Equal(1, _host.Backend.HistoryReads);
        Assert.Equal(1, _host.Backend.Creates);
        Assert.Equal(1, _host.Backend.Sends);
        await Page.GetByRole(AriaRole.Button, new() { Name = "New chat", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Messages will appear here when a conversation starts.", new() { Exact = true })).ToBeVisibleAsync();
        await Page.ReloadAsync();
        await Expect(message).ToBeEnabledAsync();
        await Expect(Page.GetByText("Completed offline answer", new() { Exact = true })).ToHaveCountAsync(0);
        Assert.Equal(1, _host.Backend.HistoryReads);
        Assert.Equal(1, _host.Backend.Creates);
    }

    [Fact]
    public async Task StopPreservesPartialOutputAndNewChatAllowsAnotherRequest()
    {
        await SendAndWaitForPartialAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Stop response", Exact = true }).ClickAsync();

        await Expect(Page.GetByText("Response canceled.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Partial offline answer", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Message", new() { Exact = true })).ToBeDisabledAsync();
        await _host.Backend.StreamEnded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(_host.Backend.StreamToken.IsCancellationRequested);
        Assert.Equal(1, _host.Backend.Sends);
        await Page.GetByRole(AriaRole.Button, new() { Name = "New chat", Exact = true }).ClickAsync();
        await Expect(Page.GetByLabel("Message", new() { Exact = true })).ToBeEnabledAsync();
        Assert.Equal(1, _host.Backend.Creates);
        await SendAndWaitForPartialAsync();
        _host.Backend.CompleteResponse();
        await Expect(Page.GetByText("Response complete.", new() { Exact = true })).ToBeVisibleAsync();
        Assert.Equal(2, _host.Backend.Creates);
        Assert.Equal(2, _host.Backend.Sends);
    }

    [Fact]
    public async Task BackendFailureShowsASafeErrorWithoutRetrying()
    {
        _host.Backend.FailResponse = true;
        await SendAndWaitForPartialAsync();

        _host.Backend.CompleteResponse();

        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("the request was not retried");
        await Expect(Page.GetByText("Partial offline answer", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Message", new() { Exact = true })).ToBeDisabledAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "New chat", Exact = true })).ToBeEnabledAsync();
        Assert.DoesNotContain("Private backend details", await Page.ContentAsync());
        Assert.Equal(1, _host.Backend.Sends);
    }

    [Fact]
    public async Task SignOutInAnotherTabCancelsWorkAndRevokesCopiedCookie()
    {
        var otherTab = await Context.NewPageAsync();
        await otherTab.GotoAsync("/");
        await Expect(otherTab.GetByLabel("Message", new() { Exact = true })).ToBeEnabledAsync();
        await SendAndWaitForPartialAsync();

        await otherTab.GetByRole(AriaRole.Button, new() { Name = "Sign out of this app", Exact = true }).ClickAsync();

        await Expect(otherTab.GetByRole(AriaRole.Heading, new() { Name = "Signed out of this app", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Your application session has ended");
        await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Sign in again", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Partial offline answer", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByLabel("Message", new() { Exact = true })).ToBeDisabledAsync();
        await _host.Backend.StreamEnded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(_host.Backend.StreamToken.IsCancellationRequested);
        await Context.AddCookiesAsync([_cookie ?? throw new InvalidOperationException("Missing original test cookie.")]);
        await using var replay = await Context.APIRequest.GetAsync("/", new() { MaxRedirects = 0 });
        Assert.Equal(302, replay.Status);
        Assert.StartsWith("https://login.microsoftonline.com/", replay.Headers["location"]);
        Assert.Equal(1, _host.Backend.Sends);
    }

    [Fact]
    public async Task AnotherOwnerDoesNotRestoreACopiedBrowserBookmark()
    {
        await SendAndWaitForPartialAsync();
        _host.Backend.CompleteResponse();
        await Expect(Page.GetByText("Response complete.", new() { Exact = true })).ToBeVisibleAsync();
        var bookmarks = await Page.EvaluateAsync<Dictionary<string, string>>(
            "() => Object.fromEntries(Object.entries(sessionStorage).filter(([key]) => key.startsWith('aiagents:conversation:v1:')))");
        Assert.Single(bookmarks);
        await using var otherContext = await CreateContextAsync();
        await otherContext.AddCookiesAsync([await _host.SignInAsync(Guid.NewGuid())]);
        await otherContext.AddInitScriptAsync(
            $"for (const [key, value] of Object.entries({JsonSerializer.Serialize(bookmarks)})) {{ sessionStorage.setItem(key, value); }}");
        var otherPage = await otherContext.NewPageAsync();

        await otherPage.GotoAsync("/");

        await Expect(otherPage.GetByLabel("Message", new() { Exact = true })).ToBeEnabledAsync();
        await Expect(otherPage.GetByText("Messages will appear here when a conversation starts.", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(otherPage.GetByText("Completed offline answer", new() { Exact = true })).ToHaveCountAsync(0);
        Assert.Equal(0, _host.Backend.HistoryReads);
        Assert.Equal(1, _host.Backend.Creates);
        Assert.Equal(1, _host.Backend.Sends);
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (_browser is not null)
            {
                await _browser.CloseAsync();
            }
        }
        finally
        {
            _playwright?.Dispose();
            await _host.DisposeAsync();
        }
    }

    private async Task<IBrowserContext> CreateContextAsync()
    {
        var browser = _browser ?? throw new InvalidOperationException("The test browser has not started.");
        var context = await browser.NewContextAsync(new()
        {
            BaseURL = _host.Address.AbsoluteUri,
            // Trust only this isolated context's ephemeral, loopback-only test server.
            IgnoreHTTPSErrors = true
        });
        await context.RouteAsync("**/*", route =>
            Uri.TryCreate(route.Request.Url, UriKind.Absolute, out var address) && address.GetLeftPart(UriPartial.Authority) == _host.Address.GetLeftPart(UriPartial.Authority)
                ? route.ContinueAsync()
                : route.AbortAsync("blockedbyclient"));
        return context;
    }

    private async Task SendAndWaitForPartialAsync()
    {
        await Page.GetByLabel("Message", new() { Exact = true }).FillAsync("An offline question");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Send", Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Partial offline answer", new() { Exact = true })).ToBeVisibleAsync();
    }
}
