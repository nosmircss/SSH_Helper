using System;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Rebex.Net;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class SshConnectionPoolCompatibilityTests
{
    [Fact]
    public void ReleaseSession_ObsoleteOverload_ReleasesLeaseForMatchingHostAndUsernameRegardlessOfPassword()
    {
        using var pool = new SshConnectionPool();
        var host = new HostConnection
        {
            IpAddress = "10.0.0.10",
            Port = 22
        };

        var key = SshConnectionPool.CreateConnectionKey(host, "admin", "secret-password");
        var leasedKeys = GetPrivateField(pool, "_leasedKeys");
        InvokeDictionaryMethod<bool>(leasedKeys, "TryAdd", key, true).Should().BeTrue();

        pool.ReleaseSession(host, "admin");

        InvokeDictionaryMethod<bool>(leasedKeys, "ContainsKey", key).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAsync_ObsoleteOverload_RemovesConnectionForMatchingHostAndUsernameRegardlessOfPassword()
    {
        using var pool = new SshConnectionPool();
        var host = new HostConnection
        {
            IpAddress = "10.0.0.11",
            Port = 22
        };

        var key = SshConnectionPool.CreateConnectionKey(host, "admin", "secret-password");
        var connections = GetPrivateField(pool, "_connections");
        var pooledConnection = CreatePooledConnection(host, "admin", key);
        InvokeDictionaryMethod<bool>(connections, "TryAdd", key, pooledConnection).Should().BeTrue();

        await pool.RemoveAsync(host, "admin");

        InvokeDictionaryMethod<bool>(connections, "ContainsKey", key).Should().BeFalse();
    }

    private static object GetPrivateField(object instance, string fieldName)
    {
        return instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(instance)!;
    }

    private static T InvokeDictionaryMethod<T>(object dictionary, string methodName, params object[] args)
    {
        return (T)dictionary.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)!
            .Invoke(dictionary, args)!;
    }

    private static object CreatePooledConnection(HostConnection host, string username, string key)
    {
        var pooledType = typeof(SshConnectionPool).GetNestedType("PooledConnection", BindingFlags.NonPublic)!;
        var instance = Activator.CreateInstance(pooledType)!;
        var now = DateTime.UtcNow;

        SetProperty(instance, "Client", new Ssh());
        SetProperty(instance, "Key", key);
        SetProperty(instance, "Host", host);
        SetProperty(instance, "Username", username);
        SetProperty(instance, "Created", now);
        SetProperty(instance, "LastUsed", now);
        SetProperty(instance, "LastHealthCheck", now);
        SetProperty(instance, "LastKeepAlive", now);
        SetProperty(instance, "KeepAliveInterval", TimeSpan.FromSeconds(30));

        return instance;
    }

    private static void SetProperty(object target, string propertyName, object value)
    {
        target.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(target, value);
    }
}
