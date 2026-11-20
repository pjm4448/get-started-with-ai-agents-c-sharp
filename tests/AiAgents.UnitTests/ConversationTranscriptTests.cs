using AiAgents.Web.Components.Chat;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiAgents.UnitTests;

public sealed class ConversationTranscriptTests
{
    [Fact]
    public async Task RendersEmptyConversation()
    {
        string html = await RenderTranscriptAsync([], ConversationStatus.Idle);

        Assert.Contains("Messages will appear here", html);
        Assert.DoesNotContain("<ol", html);
    }

    [Fact]
    public async Task RendersMessagesAndCitationsAsEncodedText()
    {
        ChatMessage[] messages =
        [
            new(ChatMessageRole.User, "<script>alert(1)</script>", []),
            new(ChatMessageRole.Assistant, "Partial\nresponse",
                [new("<img src=x onerror=alert(1)>", new Uri("https://example.test/private?token=hidden"), "file_private")])
        ];

        string html = await RenderTranscriptAsync(messages, ConversationStatus.Responding);

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("&lt;img src=x onerror=alert(1)&gt;", html);
        Assert.Contains("Partial\nresponse", System.Net.WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("file_private", html);
        Assert.DoesNotContain("token=hidden", html);
        Assert.Contains("Assistant is responding.", html);
        Assert.Contains("aria-live=\"polite\"", html);
    }

    [Theory]
    [InlineData("Answer \uE200filecite\uE202turn0file0.", "Answer .")]
    [InlineData("Answer \uE200filecite\uE202turn12file34\uE201!", "Answer !")]
    [InlineData("A\uE200filecite\uE202turn0file0\uE200filecite\uE202turn1file2\uE201 B", "A B")]
    [InlineData("A \uE200filecite\uE202turn0file0word", "A \uE200filecite\uE202turn0file0word")]
    [InlineData("A \uE200filecite\uE202turn0file", "A \uE200filecite\uE202turn0file")]
    [InlineData("A \uE200other\uE202turn0file0\uE201", "A \uE200other\uE202turn0file0\uE201")]
    public async Task FormatsOnlyRecognizedAssistantMarkers(string text, string expected)
    {
        ChatMessage[] messages =
        [
            new(ChatMessageRole.User, text, []),
            new(ChatMessageRole.Assistant, text, [new("product_info_1.md", null, "file_private")])
        ];

        string html = System.Net.WebUtility.HtmlDecode(
            await RenderTranscriptAsync(messages, ConversationStatus.Completed));

        Assert.Contains($">{text}</div>", html);
        Assert.Contains($">{expected}</div>", html);
        Assert.Contains("product_info_1.md", html);
        Assert.DoesNotContain("file_private", html);
    }

    [Fact]
    public async Task HidesPartialMarkerAtEveryStreamingBoundaryWithoutLosingCanceledText()
    {
        const string marker = "\uE200filecite\uE202turn0file0\uE201";
        for (int length = 1; length <= marker.Length; length++)
        {
            string partial = marker[..length];
            ChatMessage[] messages = [new(ChatMessageRole.Assistant, "Answer " + partial, [])];

            string html = System.Net.WebUtility.HtmlDecode(
                await RenderTranscriptAsync(messages, ConversationStatus.Responding));

            Assert.Contains(">Answer </div>", html);
            Assert.DoesNotContain(partial, html);
        }

        ChatMessage[] canceled = [new(ChatMessageRole.Assistant, "Answer \uE200filecite", [])];
        string canceledHtml = System.Net.WebUtility.HtmlDecode(
            await RenderTranscriptAsync(canceled, ConversationStatus.Canceled));
        Assert.Contains(">Answer \uE200filecite</div>", canceledHtml);
    }

    [Theory]
    [InlineData(ConversationStatus.Completed, "Response complete.")]
    [InlineData(ConversationStatus.Canceled, "Response canceled.")]
    [InlineData(ConversationStatus.Failed, "The response could not be completed.")]
    public async Task AnnouncesTerminalStatus(ConversationStatus status, string message)
    {
        string html = await RenderTranscriptAsync([], status);

        Assert.Contains(message, html);
    }

    private static Task<string> RenderTranscriptAsync(
        IReadOnlyList<ChatMessage> messages,
        ConversationStatus status)
    {
        return ComponentRenderer.RenderAsync<ConversationTranscript>(new Dictionary<string, object?>
        {
            [nameof(ConversationTranscript.Messages)] = messages,
            [nameof(ConversationTranscript.Status)] = status
        });
    }
}

internal static class ComponentRenderer
{
    public static async Task<string> RenderAsync<TComponent>(Dictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            services,
            services.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
