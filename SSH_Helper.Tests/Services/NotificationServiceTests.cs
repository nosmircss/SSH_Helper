using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;
using SSH_Helper.Services.Notifications;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class NotificationServiceTests
{
    private sealed class CapturingToastDispatcher : ToastDispatcher
    {
        public string? LastTitle { get; private set; }
        public string? LastMessage { get; private set; }
        public NotificationLevel LastLevel { get; private set; }
        public int CallCount { get; private set; }

        public override Task<NotificationResult> SendAsync(
            string? title, string message, NotificationLevel level, CancellationToken cancellationToken)
        {
            CallCount++;
            LastTitle = title;
            LastMessage = message;
            LastLevel = level;
            return Task.FromResult(NotificationResult.Success("toast"));
        }
    }

    private sealed class CapturingHandler : DelegatingHandler
    {
        public string? LastBody { get; private set; }
        public Uri? LastUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            if (request.Content != null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static (NotificationService service, CapturingHandler handler) Build(NotificationChannelKind kind)
    {
        var settings = new NotificationSettings
        {
            Enabled = true,
            DefaultProfileName = "p",
            Profiles = [new NotificationProfile { Name = "p", Kind = kind }]
        };
        var handler = new CapturingHandler();
        var service = new NotificationService(
            settings,
            webhookUrlProvider: _ => "https://hooks.example.com/test",
            httpHandler: handler);
        return (service, handler);
    }

    [Theory]
    [InlineData(NotificationLevel.Info, "#2196F3")]
    [InlineData(NotificationLevel.Warn, "#FFC107")]
    [InlineData(NotificationLevel.Error, "#F44336")]
    [InlineData(NotificationLevel.Success, "#4CAF50")]
    public async Task SlackPayload_HasLevelColoredAttachment(NotificationLevel level, string expectedColor)
    {
        var (service, handler) = Build(NotificationChannelKind.Slack);
        using var _ = service;

        var result = await service.SendAsync("p", null, "t", "m", level, null, CancellationToken.None);

        result.Sent.Should().BeTrue();
        var body = JObject.Parse(handler.LastBody!);
        body["attachments"]!.First!.Value<string>("color").Should().Be(expectedColor);
        body["attachments"]!.First!.Value<string>("text").Should().Be("m");
        body["attachments"]!.First!.Value<string>("title").Should().Be("t");
    }

    [Theory]
    [InlineData(NotificationLevel.Info, "Accent")]
    [InlineData(NotificationLevel.Warn, "Warning")]
    [InlineData(NotificationLevel.Error, "Attention")]
    [InlineData(NotificationLevel.Success, "Good")]
    public async Task TeamsPayload_IsAdaptiveCardEnvelopeWithSeverityStyledTitle(NotificationLevel level, string expectedColor)
    {
        var (service, handler) = Build(NotificationChannelKind.Teams);
        using var _ = service;

        await service.SendAsync("p", null, "Title", "Body", level, null, CancellationToken.None);

        var body = JObject.Parse(handler.LastBody!);
        body.Value<string>("type").Should().Be("message");

        var attachment = (JObject)body["attachments"]!.First!;
        attachment.Value<string>("contentType").Should().Be("application/vnd.microsoft.card.adaptive");

        var card = (JObject)attachment["content"]!;
        card.Value<string>("type").Should().Be("AdaptiveCard");

        var titleBlock = (JObject)card["body"]!.First!;
        titleBlock.Value<string>("text").Should().Be("Title");
        titleBlock.Value<string>("color").Should().Be(expectedColor);

        card["body"]!.Last!.Value<string>("text").Should().Be("Body");
    }

    [Fact]
    public async Task TeamsPayload_NormalizesTypedMentionsIntoAdaptiveCardEntities()
    {
        var (service, handler) = Build(NotificationChannelKind.Teams);
        using var _ = service;

        const string entraId = "87d349ed-44d7-43e1-9a83-5f2406dee5bd";

        var result = await service.SendAsync(
            "p",
            null,
            "Title",
            "Body",
            NotificationLevel.Info,
            new[] { "upn:alice@contoso.com|Alice", $"entra:{entraId}|Adele" },
            CancellationToken.None);

        result.Sent.Should().BeTrue();

        var body = JObject.Parse(handler.LastBody!);
        var card = (JObject)body["attachments"]!.First!["content"]!;
        var mentionBlock = (JObject)card["body"]![1]!;
        mentionBlock.Value<string>("text").Should().Be("<at>Alice</at> <at>Adele</at>");

        var entities = (JArray)card["msteams"]!["entities"]!;
        entities.Should().HaveCount(2);
        entities[0]!["text"]!.Value<string>().Should().Be("<at>Alice</at>");
        entities[0]!["mentioned"]!["id"]!.Value<string>().Should().Be("alice@contoso.com");
        entities[0]!["mentioned"]!["name"]!.Value<string>().Should().Be("Alice");
        entities[1]!["text"]!.Value<string>().Should().Be("<at>Adele</at>");
        entities[1]!["mentioned"]!["id"]!.Value<string>().Should().Be(entraId);
        entities[1]!["mentioned"]!["name"]!.Value<string>().Should().Be("Adele");
    }

    [Fact]
    public async Task TeamsPayload_UsesIdentifierAsDisplayWhenTypedMentionLabelIsOmitted()
    {
        var (service, handler) = Build(NotificationChannelKind.Teams);
        using var _ = service;

        const string entraId = "87d349ed-44d7-43e1-9a83-5f2406dee5bd";

        await service.SendAsync(
            "p",
            null,
            "Title",
            "Body",
            NotificationLevel.Info,
            new[] { "upn:alice@contoso.com", $"entra:{entraId}" },
            CancellationToken.None);

        var body = JObject.Parse(handler.LastBody!);
        var card = (JObject)body["attachments"]!.First!["content"]!;
        var mentionBlock = (JObject)card["body"]![1]!;
        mentionBlock.Value<string>("text").Should().Be($"<at>alice@contoso.com</at> <at>{entraId}</at>");

        var entities = (JArray)card["msteams"]!["entities"]!;
        entities[0]!["mentioned"]!["name"]!.Value<string>().Should().Be("alice@contoso.com");
        entities[1]!["mentioned"]!["name"]!.Value<string>().Should().Be(entraId);
    }

    [Fact]
    public async Task TeamsPayload_InvalidMentionEntriesRemainLiteralAlongsideValidMentions()
    {
        var (service, handler) = Build(NotificationChannelKind.Teams);
        using var _ = service;

        await service.SendAsync(
            "p",
            null,
            "Title",
            "Body",
            NotificationLevel.Info,
            new[] { "upn:alice@contoso.com|Alice", "@Bob", "entra:not-a-guid|Broken" },
            CancellationToken.None);

        var body = JObject.Parse(handler.LastBody!);
        var card = (JObject)body["attachments"]!.First!["content"]!;
        var mentionBlock = (JObject)card["body"]![1]!;
        mentionBlock.Value<string>("text").Should().Be("<at>Alice</at> @Bob entra:not-a-guid|Broken");

        var entities = (JArray)card["msteams"]!["entities"]!;
        entities.Should().HaveCount(1);
        entities[0]!["mentioned"]!["id"]!.Value<string>().Should().Be("alice@contoso.com");
    }

    [Theory]
    [InlineData(NotificationLevel.Info, 3447003)]
    [InlineData(NotificationLevel.Warn, 16776960)]
    [InlineData(NotificationLevel.Error, 15158332)]
    [InlineData(NotificationLevel.Success, 3066993)]
    public async Task DiscordPayload_EmbedHasLevelColor(NotificationLevel level, int expectedColor)
    {
        var (service, handler) = Build(NotificationChannelKind.Discord);
        using var _ = service;

        await service.SendAsync("p", null, "t", "m", level, null, CancellationToken.None);

        var body = JObject.Parse(handler.LastBody!);
        body["embeds"]!.First!.Value<int>("color").Should().Be(expectedColor);
        body["embeds"]!.First!.Value<string>("description").Should().Be("m");
    }

    [Fact]
    public async Task DiscordPayload_IncludesMentionsAsContent()
    {
        var (service, handler) = Build(NotificationChannelKind.Discord);
        using var _ = service;

        await service.SendAsync("p", null, null, "m", NotificationLevel.Info,
            new[] { "@here", "@alice" }, CancellationToken.None);

        var body = JObject.Parse(handler.LastBody!);
        body.Value<string>("content").Should().Be("@here @alice");
    }

    [Fact]
    public async Task DiscordPayload_NormalizesTypedMentionShorthand()
    {
        var (service, handler) = Build(NotificationChannelKind.Discord);
        using var _ = service;

        await service.SendAsync(
            "p",
            null,
            "Script finished",
            "body",
            NotificationLevel.Success,
            new[] { "user:123456789012345678", "ROLE:234567890123456789", "channel:345678901234567890", "here", "@everyone" },
            CancellationToken.None);

        var body = JObject.Parse(handler.LastBody!);
        body.Value<string>("content").Should().Be(
            "<@123456789012345678> <@&234567890123456789> <#345678901234567890> @here @everyone");
    }

    [Fact]
    public async Task DiscordPayload_PreservesLiteralMentionMarkup()
    {
        var (service, handler) = Build(NotificationChannelKind.Discord);
        using var _ = service;

        await service.SendAsync(
            "p",
            null,
            "Script finished",
            "body",
            NotificationLevel.Success,
            new[] { "<@123456789012345678>", "<@&234567890123456789>", "<#345678901234567890>", "@here" },
            CancellationToken.None);

        var body = JObject.Parse(handler.LastBody!);
        body.Value<string>("content").Should().Be(
            "<@123456789012345678> <@&234567890123456789> <#345678901234567890> @here");
    }

    [Fact]
    public async Task DiscordPayload_LeavesBareIdsAndDisplayNamesLiteral()
    {
        var (service, handler) = Build(NotificationChannelKind.Discord);
        using var _ = service;

        await service.SendAsync(
            "p",
            null,
            "Script finished",
            "body",
            NotificationLevel.Success,
            new[] { "123456789012345678", "@Thomas Farral" },
            CancellationToken.None);

        var body = JObject.Parse(handler.LastBody!);
        body.Value<string>("content").Should().Be("123456789012345678 @Thomas Farral");
    }

    [Fact]
    public async Task SlackPayload_NormalizesSafeMentionShorthand()
    {
        var (service, handler) = Build(NotificationChannelKind.Slack);
        using var _ = service;

        await service.SendAsync(
            "p",
            null,
            "Script finished",
            "body",
            NotificationLevel.Success,
            new[] { "U12345678", "@here", "channel" },
            CancellationToken.None);

        var body = JObject.Parse(handler.LastBody!);
        body.Value<string>("text").Should().Be("<@U12345678> <!here> <!channel> Script finished");
    }

    [Fact]
    public async Task SlackPayload_LeavesDisplayNameMentionLiteral()
    {
        var (service, handler) = Build(NotificationChannelKind.Slack);
        using var _ = service;

        await service.SendAsync(
            "p",
            null,
            "Script finished",
            "body",
            NotificationLevel.Success,
            new[] { "@Thomas Farral" },
            CancellationToken.None);

        var body = JObject.Parse(handler.LastBody!);
        body.Value<string>("text").Should().Be("@Thomas Farral Script finished");
    }

    [Fact]
    public async Task UnknownProfile_ReturnsFailure()
    {
        var settings = new NotificationSettings { Enabled = true };
        using var service = new NotificationService(settings);

        var result = await service.SendAsync("missing", null, null, "m", NotificationLevel.Info,
            null, CancellationToken.None);

        result.Sent.Should().BeFalse();
        result.ErrorMessage.Should().Contain("missing");
    }

    [Fact]
    public async Task MissingWebhookUrl_ReturnsFailure()
    {
        var settings = new NotificationSettings
        {
            Enabled = true,
            DefaultProfileName = "p",
            Profiles = [new NotificationProfile { Name = "p", Kind = NotificationChannelKind.Slack }]
        };
        // No webhookUrlProvider provided → resolves to null → empty URL
        using var service = new NotificationService(settings);

        var result = await service.SendAsync("p", null, null, "m", NotificationLevel.Info,
            null, CancellationToken.None);

        result.Sent.Should().BeFalse();
        result.Channel.Should().Be("slack");
        result.ErrorMessage.Should().Contain("Webhook URL");
    }

    [Fact]
    public async Task WebhookReturnsNon2xx_ResultCapturesStatusAndBody()
    {
        var settings = new NotificationSettings
        {
            Enabled = true,
            DefaultProfileName = "p",
            Profiles = [new NotificationProfile { Name = "p", Kind = NotificationChannelKind.Slack }]
        };
        var handler = new StaticHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("invalid channel")
        });
        using var service = new NotificationService(
            settings,
            webhookUrlProvider: _ => "https://hooks.example.com/test",
            httpHandler: handler);

        var result = await service.SendAsync("p", null, null, "m", NotificationLevel.Info,
            null, CancellationToken.None);

        result.Sent.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.ErrorMessage.Should().Contain("invalid channel");
    }

    [Fact]
    public async Task ToastChannel_WhenNotificationsDisabled_StillDispatches()
    {
        var settings = new NotificationSettings { Enabled = false };
        var toast = new CapturingToastDispatcher();
        using var service = new NotificationService(settings, toastDispatcher: toast);

        var result = await service.SendAsync(
            profileName: null,
            channelOverride: "toast",
            title: "Done",
            message: "Body",
            level: NotificationLevel.Success,
            mentions: null,
            cancellationToken: CancellationToken.None);

        result.Sent.Should().BeTrue();
        toast.CallCount.Should().Be(1);
        toast.LastTitle.Should().Be("Done");
        toast.LastMessage.Should().Be("Body");
        toast.LastLevel.Should().Be(NotificationLevel.Success);
    }

    [Fact]
    public async Task NonToastProfile_WhenNotificationsDisabled_ReturnsDisabledFailure()
    {
        var settings = new NotificationSettings
        {
            Enabled = false,
            Profiles = [new NotificationProfile { Name = "ops", Kind = NotificationChannelKind.Slack }]
        };

        using var service = new NotificationService(
            settings,
            webhookUrlProvider: _ => "https://hooks.example.com/test",
            httpHandler: new CapturingHandler());

        var result = await service.SendAsync(
            profileName: "ops",
            channelOverride: null,
            title: null,
            message: "Body",
            level: NotificationLevel.Info,
            mentions: null,
            cancellationToken: CancellationToken.None);

        result.Sent.Should().BeFalse();
        result.Channel.Should().Be("slack");
        result.ErrorMessage.Should().Contain("disabled");
    }

    private sealed class StaticHandler : DelegatingHandler
    {
        private readonly HttpResponseMessage _response;
        public StaticHandler(HttpResponseMessage response) { _response = response; }
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }
}
