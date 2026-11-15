using AiAgents.Web.Components.Chat;
using Microsoft.AspNetCore.Components;

namespace AiAgents.UnitTests;

public sealed class ChatComposerTests
{
    [Fact]
    public async Task ComposerIsDisabledWithoutIntegration()
    {
        string html = await ComponentRenderer.RenderAsync<ChatComposer>(new Dictionary<string, object?>
        {
            [nameof(ChatComposer.IsAvailable)] = true,
            [nameof(ChatComposer.Value)] = "Hello"
        });

        Assert.Matches("<textarea[^>]*disabled", html);
        Assert.Matches("<button[^>]*type=\"submit\"[^>]*disabled", html);
    }

    [Fact]
    public async Task AvailableComposerAllowsNonemptyInput()
    {
        var parameters = CreateParameters("Hello");

        string html = await ComponentRenderer.RenderAsync<ChatComposer>(parameters);

        Assert.DoesNotMatch("<textarea[^>]*disabled", html);
        Assert.DoesNotMatch("<button[^>]*type=\"submit\"[^>]*disabled", html);
        Assert.Contains("for=\"chat-message\"", html);
    }

    [Fact]
    public async Task ActiveResponseDisablesSendingAndOffersCancellation()
    {
        var parameters = CreateParameters("Hello");
        parameters[nameof(ChatComposer.IsResponding)] = true;
        parameters[nameof(ChatComposer.OnCancel)] = EventCallback.Factory.Create(this, () => { });

        string html = await ComponentRenderer.RenderAsync<ChatComposer>(parameters);

        Assert.Matches("<textarea[^>]*disabled", html);
        Assert.Matches("<button[^>]*type=\"submit\"[^>]*disabled", html);
        Assert.Contains("Stop response", html);
        Assert.DoesNotMatch("<button[^>]*type=\"button\"[^>]*disabled", html);
    }

    [Fact]
    public async Task WhitespaceCannotBeSubmitted()
    {
        string html = await ComponentRenderer.RenderAsync<ChatComposer>(CreateParameters("  "));

        Assert.Matches("<button[^>]*type=\"submit\"[^>]*disabled", html);
    }

    private Dictionary<string, object?> CreateParameters(string value) => new()
    {
        [nameof(ChatComposer.IsAvailable)] = true,
        [nameof(ChatComposer.Value)] = value,
        [nameof(ChatComposer.ValueChanged)] = EventCallback.Factory.Create<string>(this, _ => { }),
        [nameof(ChatComposer.OnSubmit)] = EventCallback.Factory.Create<string>(this, _ => { })
    };
}
