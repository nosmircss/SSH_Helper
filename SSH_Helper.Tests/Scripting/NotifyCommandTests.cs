using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;
using SSH_Helper.Services.Notifications;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class NotifyCommandTests
{
    private sealed class CapturingToastDispatcher : ToastDispatcher
    {
        public string? LastTitle { get; private set; }
        public string? LastMessage { get; private set; }
        public NotificationLevel LastLevel { get; private set; }
        public bool LastIncludeLevelAttribution { get; private set; }
        public int CallCount { get; private set; }
        public bool ShouldSucceed { get; set; } = true;

        public override Task<NotificationResult> SendAsync(
            string? title, string message, NotificationLevel level, CancellationToken cancellationToken, bool includeLevelAttribution = true)
        {
            CallCount++;
            LastTitle = title;
            LastMessage = message;
            LastLevel = level;
            LastIncludeLevelAttribution = includeLevelAttribution;
            return Task.FromResult(ShouldSucceed
                ? NotificationResult.Success("toast")
                : NotificationResult.Failure("toast", "toast failed"));
        }
    }

    private sealed class CapturingSmtpDispatcher : SmtpDispatcher
    {
        public NotificationProfile? LastProfile { get; private set; }
        public string? LastPassword { get; private set; }
        public string? LastTitle { get; private set; }
        public string? LastMessage { get; private set; }
        public NotificationLevel LastLevel { get; private set; }
        public int CallCount { get; private set; }

        public override Task<NotificationResult> SendAsync(
            NotificationProfile profile, string? password, string? title, string message,
            NotificationLevel level, CancellationToken cancellationToken)
        {
            CallCount++;
            LastProfile = profile;
            LastPassword = password;
            LastTitle = title;
            LastMessage = message;
            LastLevel = level;
            return Task.FromResult(NotificationResult.Success("smtp"));
        }
    }

    private static NotificationSettings SlackSettings(string profileName = "ops")
    {
        return new NotificationSettings
        {
            Enabled = true,
            DefaultProfileName = profileName,
            Profiles =
            [
                new NotificationProfile
                {
                    Name = profileName,
                    Kind = NotificationChannelKind.Slack
                }
            ]
        };
    }

    private static NotificationSettings TeamsSettings(string profileName = "ops")
    {
        return new NotificationSettings
        {
            Enabled = true,
            DefaultProfileName = profileName,
            Profiles =
            [
                new NotificationProfile
                {
                    Name = profileName,
                    Kind = NotificationChannelKind.Teams
                }
            ]
        };
    }

    private static (NotificationService service, NotifyTestRig rig) CreateService(
        NotificationSettings settings,
        HttpResponseMessage? webhookResponse = null)
    {
        var rig = new NotifyTestRig
        {
            Toast = new CapturingToastDispatcher(),
            Smtp = new CapturingSmtpDispatcher(),
            Handler = new NotifyingHandlerStub(request =>
                webhookResponse ?? new HttpResponseMessage(HttpStatusCode.OK))
        };
        var service = new NotificationService(
            settings,
            webhookUrlProvider: _ => "https://hooks.example.com/test",
            smtpPasswordProvider: _ => "smtp-pw",
            httpHandler: rig.Handler,
            toastDispatcher: rig.Toast,
            smtpDispatcher: rig.Smtp);
        return (service, rig);
    }

    private sealed class NotifyTestRig
    {
        public CapturingToastDispatcher Toast { get; set; } = null!;
        public CapturingSmtpDispatcher Smtp { get; set; } = null!;
        public NotifyingHandlerStub Handler { get; set; } = null!;
    }

    [Fact]
    public async Task ProfileImpliesChannel_SlackProfileRoutesToWebhook()
    {
        var settings = SlackSettings();
        var (service, rig) = CreateService(settings);
        using var _ = service;

        var step = new ScriptStep
        {
            Notify = new NotifyOptions { Profile = "ops", Message = "hello" }
        };
        var context = new ScriptContext { NotificationService = service };
        var result = await new NotifyCommand().ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        rig.Handler.CallCount.Should().Be(1);
        rig.Toast.CallCount.Should().Be(0);
        rig.Smtp.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ToastChannel_NeedsNoProfile_DispatchesToToast()
    {
        var settings = new NotificationSettings { Enabled = true };
        var (service, rig) = CreateService(settings);
        using var _ = service;

        var step = new ScriptStep
        {
            Notify = new NotifyOptions { Channel = "toast", Title = "Done", Message = "yes", Level = "success" }
        };
        var context = new ScriptContext { NotificationService = service };
        var result = await new NotifyCommand().ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        rig.Toast.CallCount.Should().Be(1);
        rig.Toast.LastTitle.Should().Be("Done");
        rig.Toast.LastMessage.Should().Be("yes");
        rig.Toast.LastLevel.Should().Be(NotificationLevel.Success);
        rig.Toast.LastIncludeLevelAttribution.Should().BeTrue();
    }

    [Fact]
    public async Task ToastChannel_WithoutLevel_DoesNotRequestAttribution()
    {
        var settings = new NotificationSettings { Enabled = true };
        var (service, rig) = CreateService(settings);
        using var _ = service;

        var step = new ScriptStep
        {
            Notify = new NotifyOptions { Channel = "toast", Title = "Done", Message = "yes" }
        };
        var context = new ScriptContext { NotificationService = service };
        var result = await new NotifyCommand().ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        rig.Toast.CallCount.Should().Be(1);
        rig.Toast.LastLevel.Should().Be(NotificationLevel.Info);
        rig.Toast.LastIncludeLevelAttribution.Should().BeFalse();
    }

    [Fact]
    public async Task ChannelMismatchWithProfile_ReturnsFailure()
    {
        var settings = SlackSettings();
        var (service, _) = CreateService(settings);
        using var _s = service;

        var step = new ScriptStep
        {
            Notify = new NotifyOptions { Profile = "ops", Channel = "smtp", Message = "hi" }
        };
        var context = new ScriptContext { NotificationService = service };
        var result = await new NotifyCommand().ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("does not match");
    }

    [Fact]
    public async Task MissingMessage_ReturnsFailure()
    {
        var settings = SlackSettings();
        var (service, _) = CreateService(settings);
        using var _s = service;

        var step = new ScriptStep
        {
            Notify = new NotifyOptions { Profile = "ops", Message = "" }
        };
        var context = new ScriptContext { NotificationService = service };
        var result = await new NotifyCommand().ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("message");
    }

    [Fact]
    public async Task VariableSubstitution_AppliesToTitleMessageAndMention()
    {
        var settings = SlackSettings();
        var (service, rig) = CreateService(settings);
        using var _ = service;

        var step = new ScriptStep
        {
            Notify = new NotifyOptions
            {
                Profile = "ops",
                Title = "Host: {{host}}",
                Message = "Status {{status}}",
                Mention = new List<string> { "@{{owner}}" }
            }
        };
        var context = new ScriptContext { NotificationService = service };
        context.SetVariable("host", "web-01");
        context.SetVariable("status", "green");
        context.SetVariable("owner", "alice");

        var result = await new NotifyCommand().ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var payload = JObject.Parse(rig.Handler.LastRequestBody ?? "");
        payload.Value<string>("text").Should().Contain("@alice");
        payload["attachments"]!.First!.Value<string>("text").Should().Be("Status green");
        payload["attachments"]!.First!.Value<string>("title").Should().Be("Host: web-01");
    }

    [Fact]
    public async Task TeamsTypedMentions_ApplyVariableSubstitutionBeforePayloadGeneration()
    {
        var settings = TeamsSettings();
        var (service, rig) = CreateService(settings);
        using var _ = service;

        var step = new ScriptStep
        {
            Notify = new NotifyOptions
            {
                Profile = "ops",
                Title = "Host: {{host}}",
                Message = "Status {{status}}",
                Mention = new List<string>
                {
                    "upn:{{owner_upn}}|{{owner_name}}",
                    "entra:{{owner_id}}"
                }
            }
        };
        var context = new ScriptContext { NotificationService = service };
        context.SetVariable("host", "web-01");
        context.SetVariable("status", "green");
        context.SetVariable("owner_upn", "alice@contoso.com");
        context.SetVariable("owner_name", "Alice");
        context.SetVariable("owner_id", "87d349ed-44d7-43e1-9a83-5f2406dee5bd");

        var result = await new NotifyCommand().ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var payload = JObject.Parse(rig.Handler.LastRequestBody ?? "");
        var card = (JObject)payload["attachments"]!.First!["content"]!;
        card["body"]!.Last!.Value<string>("text").Should().Be("Status green");

        var entities = (JArray)card["msteams"]!["entities"]!;
        entities.Should().HaveCount(2);
        entities[0]!["mentioned"]!["id"]!.Value<string>().Should().Be("alice@contoso.com");
        entities[0]!["mentioned"]!["name"]!.Value<string>().Should().Be("Alice");
        entities[1]!["mentioned"]!["id"]!.Value<string>().Should().Be("87d349ed-44d7-43e1-9a83-5f2406dee5bd");
        entities[1]!["mentioned"]!["name"]!.Value<string>().Should().Be("87d349ed-44d7-43e1-9a83-5f2406dee5bd");
    }

    [Fact]
    public async Task InvalidTeamsMention_EmitsWarningButStillSends()
    {
        var settings = TeamsSettings();
        var (service, rig) = CreateService(settings);
        using var _ = service;

        var warnings = new List<string>();
        var step = new ScriptStep
        {
            Notify = new NotifyOptions
            {
                Profile = "ops",
                Title = "Heads up",
                Message = "Status green",
                Mention = new List<string>
                {
                    "upn:{{owner_upn}}|{{owner_name}}",
                    "@{{fallback_name}}"
                }
            }
        };
        var context = new ScriptContext { NotificationService = service };
        context.OutputReceived += (_, args) =>
        {
            if (args.Type == ScriptOutputType.Warning)
                warnings.Add(args.Message);
        };
        context.SetVariable("owner_upn", "alice@contoso.com");
        context.SetVariable("owner_name", "Alice");
        context.SetVariable("fallback_name", "Bob");

        var result = await new NotifyCommand().ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        warnings.Should().Contain(message => message.Contains("@Bob", StringComparison.Ordinal));

        var payload = JObject.Parse(rig.Handler.LastRequestBody ?? "");
        var card = (JObject)payload["attachments"]!.First!["content"]!;
        card["body"]![1]!.Value<string>("text").Should().Be("<at>Alice</at> @Bob");
    }

    [Fact]
    public async Task IntoCapture_WritesStructuredResultVariables()
    {
        var settings = SlackSettings();
        var (service, _) = CreateService(settings,
            new HttpResponseMessage(HttpStatusCode.OK));
        using var _s = service;

        var step = new ScriptStep
        {
            Notify = new NotifyOptions { Profile = "ops", Message = "ok", Into = "notify_result" }
        };
        var context = new ScriptContext { NotificationService = service };
        var result = await new NotifyCommand().ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("notify_result.sent").Should().Be("true");
        context.GetVariableString("notify_result.channel").Should().Be("slack");
        context.GetVariableString("notify_result.status_code").Should().Be("200");
    }

    [Fact]
    public async Task WebhookFailure_WithOnErrorContinue_Suppresses()
    {
        var settings = SlackSettings();
        var (service, _) = CreateService(settings,
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("server died", Encoding.UTF8, "text/plain")
            });
        using var _s = service;

        var step = new ScriptStep
        {
            Notify = new NotifyOptions { Profile = "ops", Message = "hi", Into = "r" }
        };
        step.OnError = "continue";
        var context = new ScriptContext { NotificationService = service };
        var result = await new NotifyCommand().ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeTrue();
        context.GetVariableString("r.sent").Should().Be("false");
        context.GetVariableString("r.status_code").Should().Be("500");
    }

    [Fact]
    public async Task NoProfileNoChannel_FallsBackToDefaultProfile()
    {
        var settings = SlackSettings("fallback");
        var (service, rig) = CreateService(settings);
        using var _ = service;

        var step = new ScriptStep
        {
            Notify = new NotifyOptions { Message = "default route" }
        };
        var context = new ScriptContext { NotificationService = service };
        var result = await new NotifyCommand().ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        rig.Handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task NoServiceConfigured_ReturnsFailureWithHonorsOnError()
    {
        var step = new ScriptStep
        {
            Notify = new NotifyOptions { Profile = "ops", Message = "hi" }
        };
        step.OnError = "continue";
        var context = new ScriptContext { NotificationService = null };

        var result = await new NotifyCommand().ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeTrue();
        result.Message.Should().Contain("not configured");
    }

    [Fact]
    public async Task SmtpProfile_DispatchesToSmtp()
    {
        var settings = new NotificationSettings
        {
            Enabled = true,
            Profiles =
            [
                new NotificationProfile
                {
                    Name = "mail",
                    Kind = NotificationChannelKind.Smtp,
                    SmtpHost = "smtp.test",
                    SmtpFromAddress = "from@test",
                    SmtpToAddresses = ["to@test"]
                }
            ]
        };
        var (service, rig) = CreateService(settings);
        using var _ = service;

        var step = new ScriptStep
        {
            Notify = new NotifyOptions { Profile = "mail", Title = "Job done", Message = "all ok", Level = "success" }
        };
        var context = new ScriptContext { NotificationService = service };
        var result = await new NotifyCommand().ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        rig.Smtp.CallCount.Should().Be(1);
        rig.Smtp.LastProfile!.Name.Should().Be("mail");
        rig.Smtp.LastPassword.Should().Be("smtp-pw");
        rig.Smtp.LastLevel.Should().Be(NotificationLevel.Success);
    }
}

internal sealed class NotifyingHandlerStub : DelegatingHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
    public int CallCount { get; private set; }
    public string? LastRequestBody { get; private set; }

    public NotifyingHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        if (request.Content != null)
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return _handler(request);
    }
}
