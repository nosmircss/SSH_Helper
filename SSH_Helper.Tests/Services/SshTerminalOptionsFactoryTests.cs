using System.Text;
using FluentAssertions;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class SshTerminalOptionsFactoryTests
{
    [Fact]
    public void Create_UsesUtf8Encoding()
    {
        var options = SshTerminalOptionsFactory.Create();

        options.Encoding.Should().Be(Encoding.UTF8);
    }
}
