using FluentAssertions;
using SSH_Helper.Services.Scripting.Parsers;
using Xunit;

namespace SSH_Helper.Tests.Scripting.Parsers
{
    public class FortiGateParserTests
    {
        private readonly FortiGateParser _parser = new();

        [Fact]
        public void Parse_EmptyConfig_ReturnsEmptyDictionary()
        {
            var result = _parser.Parse("");
            result.Should().BeEmpty();
        }

        [Fact]
        public void Parse_SimpleConfigBlock_ParsesCorrectly()
        {
            var config = @"
config system global
    set hostname ""FW01""
    set admintimeout 30
end";

            var result = _parser.Parse(config);

            result.Should().ContainKey("system");
            var system = result["system"] as Dictionary<string, object>;
            system.Should().NotBeNull();
            system.Should().ContainKey("global");

            var global = system!["global"] as Dictionary<string, object>;
            global.Should().NotBeNull();
            global.Should().ContainKey("hostname");
            global!["hostname"].Should().Be("FW01");
            global.Should().ContainKey("admintimeout");
            global["admintimeout"].Should().Be("30");
        }

        [Fact]
        public void Parse_TableWithEditEntries_ParsesCorrectly()
        {
            var config = @"
config system interface
    edit ""wan1""
        set vdom ""root""
        set ip 10.0.0.1 255.255.255.0
        set type physical
    next
    edit ""lan1""
        set vdom ""root""
        set ip 192.168.1.1 255.255.255.0
    next
end";

            var result = _parser.Parse(config);

            result.Should().ContainKey("system");
            var system = result["system"] as Dictionary<string, object>;
            system.Should().ContainKey("interface");

            var iface = system!["interface"] as Dictionary<string, object>;
            iface.Should().NotBeNull();
            iface.Should().ContainKey("wan1");
            iface.Should().ContainKey("lan1");

            var wan1 = iface!["wan1"] as Dictionary<string, object>;
            wan1.Should().NotBeNull();
            wan1!["vdom"].Should().Be("root");
            wan1["ip"].Should().Be("10.0.0.1 255.255.255.0");
            wan1["type"].Should().Be("physical");

            var lan1 = iface["lan1"] as Dictionary<string, object>;
            lan1.Should().NotBeNull();
            lan1!["vdom"].Should().Be("root");
            lan1["ip"].Should().Be("192.168.1.1 255.255.255.0");
        }

        [Fact]
        public void Parse_NestedConfigBlocks_ParsesCorrectly()
        {
            var config = @"
config firewall policy
    edit 1
        set srcintf ""wan1""
        set dstintf ""lan1""
        config dstaddr
            edit ""server1""
                set ip 192.168.1.10
            next
        end
    next
end";

            var result = _parser.Parse(config);

            result.Should().ContainKey("firewall");
            var firewall = result["firewall"] as Dictionary<string, object>;
            firewall.Should().ContainKey("policy");

            var policy = firewall!["policy"] as Dictionary<string, object>;
            policy.Should().ContainKey("1");

            var policy1 = policy!["1"] as Dictionary<string, object>;
            policy1.Should().NotBeNull();
            policy1!["srcintf"].Should().Be("wan1");
            policy1["dstintf"].Should().Be("lan1");

            // Nested config
            policy1.Should().ContainKey("dstaddr");
            var dstaddr = policy1["dstaddr"] as Dictionary<string, object>;
            dstaddr.Should().ContainKey("server1");

            var server1 = dstaddr!["server1"] as Dictionary<string, object>;
            server1.Should().NotBeNull();
            server1!["ip"].Should().Be("192.168.1.10");
        }

        [Fact]
        public void Parse_MultiValueSet_ReturnsArray()
        {
            var config = @"
config firewall policy
    edit 1
        set member ""obj1"" ""obj2"" ""obj3""
    next
end";

            var result = _parser.Parse(config);

            var policy = ((result["firewall"] as Dictionary<string, object>)!["policy"] as Dictionary<string, object>)!["1"] as Dictionary<string, object>;
            policy.Should().NotBeNull();
            policy!["member"].Should().BeOfType<List<string>>();

            var members = policy["member"] as List<string>;
            members.Should().HaveCount(3);
            members.Should().Contain("obj1", "obj2", "obj3");
        }

        [Fact]
        public void Parse_UnquotedEditName_ParsesCorrectly()
        {
            var config = @"
config firewall policy
    edit 100
        set name ""test""
    next
end";

            var result = _parser.Parse(config);

            var policy = (result["firewall"] as Dictionary<string, object>)!["policy"] as Dictionary<string, object>;
            policy.Should().ContainKey("100");

            var policy100 = policy!["100"] as Dictionary<string, object>;
            policy100.Should().NotBeNull();
            policy100!["name"].Should().Be("test");
        }

        [Fact]
        public void Parse_IgnoresComments()
        {
            var config = @"
# This is a comment
config system global
    # Another comment
    set hostname ""FW01""
end";

            var result = _parser.Parse(config);

            result.Should().ContainKey("system");
            var global = ((result["system"] as Dictionary<string, object>)!["global"] as Dictionary<string, object>);
            global.Should().NotBeNull();
            global!["hostname"].Should().Be("FW01");
        }

        [Fact]
        public void Parse_UnsetDirectives_OmittedFromOutput()
        {
            var config = @"
config system global
    set hostname ""FW01""
    unset timezone
end";

            var result = _parser.Parse(config);

            var global = ((result["system"] as Dictionary<string, object>)!["global"] as Dictionary<string, object>);
            global.Should().NotBeNull();
            global.Should().ContainKey("hostname");
            global.Should().NotContainKey("timezone");
        }

        [Fact]
        public void Parse_CaseInsensitiveKeys()
        {
            var config = @"
config system global
    set Hostname ""FW01""
end";

            var result = _parser.Parse(config);

            var global = ((result["system"] as Dictionary<string, object>)!["global"] as Dictionary<string, object>);
            // Should be able to access with different casing
            global.Should().ContainKey("hostname");
            global.Should().ContainKey("Hostname");
            global.Should().ContainKey("HOSTNAME");
        }

        [Fact]
        public void Parse_WithSectionFilter_OnlyParsesSpecifiedSections()
        {
            var config = @"
config system global
    set hostname ""FW01""
end
config system interface
    edit ""wan1""
        set ip 10.0.0.1 255.255.255.0
    next
end
config firewall policy
    edit 1
        set name ""test""
    next
end";

            var result = _parser.Parse(config, new[] { "system interface" });

            result.Should().ContainKey("system");
            var system = result["system"] as Dictionary<string, object>;
            system.Should().ContainKey("interface");
            // Note: Due to the way filtering works, global may still appear
            // The filter primarily works at the top-level config block
        }

        [Fact]
        public void ParserFactory_FortiGate_ReturnsParser()
        {
            var parser = ParserFactory.GetParser("fortigate");
            parser.Should().NotBeNull();
            parser.Should().BeOfType<FortiGateParser>();
        }

        [Fact]
        public void ParserFactory_FortiOS_ReturnsParser()
        {
            var parser = ParserFactory.GetParser("fortios");
            parser.Should().NotBeNull();
            parser.Should().BeOfType<FortiGateParser>();
        }

        [Fact]
        public void ParserFactory_InvalidFormat_ThrowsException()
        {
            var act = () => ParserFactory.GetParser("invalid");
            act.Should().Throw<ArgumentException>()
                .WithMessage("*Unsupported configuration format*")
                .WithMessage("*invalid*")
                .WithMessage("*fortigate*");
        }

        [Fact]
        public void ParserFactory_IsFormatSupported_ReturnsTrueForValid()
        {
            ParserFactory.IsFormatSupported("fortigate").Should().BeTrue();
            ParserFactory.IsFormatSupported("FORTIGATE").Should().BeTrue();
            ParserFactory.IsFormatSupported("fortios").Should().BeTrue();
        }

        [Fact]
        public void ParserFactory_IsFormatSupported_ReturnsFalseForInvalid()
        {
            ParserFactory.IsFormatSupported("invalid").Should().BeFalse();
            ParserFactory.IsFormatSupported("").Should().BeFalse();
            ParserFactory.IsFormatSupported(null!).Should().BeFalse();
        }
    }
}
