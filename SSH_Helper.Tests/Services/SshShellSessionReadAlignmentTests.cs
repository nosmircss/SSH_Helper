using FluentAssertions;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services
{
    /// <summary>
    /// Regression tests for the command/output alignment bug where a stale prompt redraw left
    /// in the stream buffer (e.g. FortiGate redrawing its prompt after <c>end</c> exits a config
    /// submode) caused every subsequent command's output to lag one command behind.
    ///
    /// These drive the real <see cref="SshShellSession"/> read loop through a fake
    /// <see cref="IShellStream"/> that replays the exact byte sequence captured in the bug report,
    /// with no live SSH connection.
    /// </summary>
    public class SshShellSessionReadAlignmentTests
    {
        private const string BasePrompt = "FG-VM64-KVM #";

        /// <summary>
        /// Deterministic stand-in for the Rebex scripting stream. <see cref="ReadData"/> returns
        /// queued chunks and throws a timeout-style exception when the buffer is empty, mirroring
        /// Rebex's behaviour. A command's response is enqueued when that command is sent.
        /// </summary>
        private sealed class FakeShellStream : IShellStream
        {
            private readonly Queue<string> _pending = new();
            private readonly Dictionary<string, string[]> _responses;

            public FakeShellStream(Dictionary<string, string[]> responses)
            {
                _responses = responses;
            }

            public int Timeout { get; set; }
            public bool IsConnected => true;
            public List<string> Sent { get; } = new();

            /// <summary>Queues data already sitting in the buffer before the next command is sent.</summary>
            public void PreloadResidual(params string[] chunks)
            {
                foreach (var chunk in chunks)
                    _pending.Enqueue(chunk);
            }

            public string ReadData()
            {
                if (_pending.Count == 0)
                    throw new TimeoutException("read timed out");
                return _pending.Dequeue();
            }

            public void Send(string data)
            {
                Sent.Add(data);
                if (_responses.TryGetValue(data, out var chunks))
                {
                    foreach (var chunk in chunks)
                        _pending.Enqueue(chunk);
                }
            }

            public void KeepAlive() { }
        }

        private static SshTimeoutOptions FastTimeouts() => new()
        {
            CommandTimeout = TimeSpan.FromSeconds(2),
            IdleTimeout = TimeSpan.FromMilliseconds(200),
            KeepAliveInterval = TimeSpan.Zero,
        };

        private static SshShellSession CreateSession(FakeShellStream stream) =>
            new(stream, FastTimeouts(), BasePrompt);

        private static string Run(SshShellSession session, string command) =>
            session.ExecuteAsync(command).GetAwaiter().GetResult();

        [Fact]
        public void Execute_TwoCommands_AfterStalePromptRedraw_KeepsEachOutputWithItsOwnCommand()
        {
            // The exact failure from the bug report: a prior `end` left a base-prompt redraw in
            // the buffer. Pre-fix, the first read consumes that stale prompt and every later
            // command's output is shifted back by one ("config log..." shows up under the next
            // command, that command's echo shows up under the one after, etc.).
            var responses = new Dictionary<string, string[]>
            {
                ["config log syslogd4 setting\r"] = new[] { " config log syslogd4 setting\r\r\n\r\nFG-VM64-KVM (setting) #" },
                ["set status enable\r"] = new[] { " set status enable\r\r\n\r\nFG-VM64-KVM (setting) #" },
            };
            var stream = new FakeShellStream(responses);
            stream.PreloadResidual(" \r\r\nFG-VM64-KVM #"); // leftover prompt redraw from the previous command

            using var session = CreateSession(stream);

            var first = Run(session, "config log syslogd4 setting");
            var second = Run(session, "set status enable");

            first.Should().Contain("config log syslogd4 setting",
                "the first command's output must be its own echo, not a stale prompt");
            first.Should().NotContain("set status enable",
                "the first command must not absorb a later command's output");

            second.Should().Contain("set status enable",
                "the second command's output must be its own echo, not the previous command's");
            second.Should().NotContain("config log syslogd4 setting",
                "output must not lag one command behind");
        }

        [Fact]
        public void Execute_StalePromptArrivingAfterSend_DoesNotTerminateReadEarly()
        {
            // A redraw still in flight when the command is sent slips past the pre-send drain.
            // The echo guard must still refuse to match that stale prompt and wait for this
            // command's own echo, so the output is attributed correctly.
            var responses = new Dictionary<string, string[]>
            {
                ["config log syslogd4 setting\r"] = new[]
                {
                    " \r\r\nFG-VM64-KVM #",                                          // stale redraw, arrives first
                    " config log syslogd4 setting\r\r\n\r\nFG-VM64-KVM (setting) #", // real echo + prompt
                },
            };
            var stream = new FakeShellStream(responses);
            using var session = CreateSession(stream);

            var output = Run(session, "config log syslogd4 setting");

            output.Should().Contain("config log syslogd4 setting",
                "the read must wait for this command's echo rather than matching the stale prompt");
        }

        [Fact]
        public void Execute_WithBufferedStalePrompt_DrainsItSoOutputStartsWithTheCommand()
        {
            // When the redraw is already buffered before the send, the pre-send drain removes it
            // so the command's output is not just correctly attributed but also free of the
            // spurious leading prompt line.
            var responses = new Dictionary<string, string[]>
            {
                ["config log syslogd4 setting\r"] = new[] { " config log syslogd4 setting\r\r\n\r\nFG-VM64-KVM (setting) #" },
            };
            var stream = new FakeShellStream(responses);
            stream.PreloadResidual(" \r\r\nFG-VM64-KVM #");

            using var session = CreateSession(stream);
            var output = Run(session, "config log syslogd4 setting");

            output.TrimStart().Should().StartWith("config log syslogd4 setting",
                "the stale prompt must be drained so the output begins with the command's echo");
        }

        [Fact]
        public void Execute_NoResidual_ReturnsCommandOutput()
        {
            // Sanity: with a clean buffer the loop behaves normally and returns the command output.
            var responses = new Dictionary<string, string[]>
            {
                ["get system status\r"] = new[] { " get system status\r\r\nVersion: v7.2.1\r\n\r\nFG-VM64-KVM #" },
            };
            var stream = new FakeShellStream(responses);
            using var session = CreateSession(stream);

            var output = Run(session, "get system status");

            output.Should().Contain("Version: v7.2.1");
        }
    }
}
