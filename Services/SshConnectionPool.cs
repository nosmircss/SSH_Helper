using System.Collections.Concurrent;
using Rebex.Net;
using Rebex.TerminalEmulation;
using SSH_Helper.Models;

// Alias to avoid conflict with SSH_Helper.Services.Scripting namespace
using RebexScripting = Rebex.TerminalEmulation.Scripting;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Manages a pool of SSH connections for efficient reuse.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Benefits of connection pooling:</b>
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Reduced connection overhead:</b> SSH handshakes involve multiple round trips
    ///     (key exchange, authentication). Reusing connections eliminates this overhead.
    ///   </item>
    ///   <item>
    ///     <b>Better resource utilization:</b> Limits the number of concurrent connections
    ///     to prevent overwhelming remote hosts or exhausting local resources.
    ///   </item>
    ///   <item>
    ///     <b>Session state preservation:</b> When used with shell sessions, can maintain
    ///     environment variables, working directory, and authentication context.
    ///   </item>
    ///   <item>
    ///     <b>Faster repeated operations:</b> Executing multiple batches of commands against
    ///     the same host is significantly faster when connections are reused.
    ///   </item>
    /// </list>
    /// <para>
    /// <b>Connection lifecycle:</b>
    /// </para>
    /// <list type="number">
    ///   <item>GetOrCreateAsync - Retrieves existing connection or creates new one</item>
    ///   <item>Connection is used for command execution</item>
    ///   <item>Connection remains in pool for reuse</item>
    ///   <item>Automatic health checks before reuse</item>
    ///   <item>Automatic cleanup of stale/dead connections</item>
    /// </list>
    /// </remarks>
    public class SshConnectionPool : IDisposable
    {
        private readonly ConcurrentDictionary<string, PooledConnection> _connections = new();
        private readonly SemaphoreSlim _creationLock = new(1, 1);
        private readonly SshTimeoutOptions _defaultTimeouts;
        private readonly TimeSpan _maxConnectionAge;
        private readonly TimeSpan _healthCheckInterval;
        private bool _disposed;

        /// <summary>
        /// Event fired when a connection is created.
        /// </summary>
        public event EventHandler<ConnectionEventArgs>? ConnectionCreated;

        /// <summary>
        /// Event fired when a connection is reused from the pool.
        /// </summary>
        public event EventHandler<ConnectionEventArgs>? ConnectionReused;

        /// <summary>
        /// Event fired when a connection is removed from the pool.
        /// </summary>
        public event EventHandler<ConnectionEventArgs>? ConnectionRemoved;

        /// <summary>
        /// Event fired when an error occurs with a pooled connection.
        /// </summary>
        public event EventHandler<ConnectionErrorEventArgs>? ConnectionError;

        /// <summary>
        /// Gets the number of connections currently in the pool.
        /// </summary>
        public int Count => _connections.Count;

        /// <summary>
        /// Gets statistics about the connection pool.
        /// </summary>
        public PoolStatistics Statistics { get; } = new();

        /// <summary>
        /// Creates a new connection pool.
        /// </summary>
        /// <param name="defaultTimeouts">Default timeout settings for connections</param>
        /// <param name="maxConnectionAge">Maximum age before a connection is considered stale (default 30 minutes)</param>
        /// <param name="healthCheckInterval">Minimum interval between health checks (default 30 seconds)</param>
        public SshConnectionPool(
            SshTimeoutOptions? defaultTimeouts = null,
            TimeSpan? maxConnectionAge = null,
            TimeSpan? healthCheckInterval = null)
        {
            _defaultTimeouts = defaultTimeouts ?? SshTimeoutOptions.Default;
            _maxConnectionAge = maxConnectionAge ?? TimeSpan.FromMinutes(30);
            _healthCheckInterval = healthCheckInterval ?? TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Gets or creates a connection to the specified host.
        /// </summary>
        /// <param name="host">Host connection details</param>
        /// <param name="username">Username for authentication</param>
        /// <param name="password">Password for authentication</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A connected SSH client</returns>
        public async Task<Ssh> GetOrCreateAsync(
            HostConnection host,
            string username,
            string password,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var key = CreateConnectionKey(host, username);

            // Try to get existing connection
            if (_connections.TryGetValue(key, out var pooled))
            {
                if (await IsConnectionHealthyAsync(pooled, cancellationToken))
                {
                    pooled.LastUsed = DateTime.UtcNow;
                    Statistics.IncrementReused();
                    OnConnectionReused(host, username);
                    return pooled.Client;
                }
                else
                {
                    // Connection is unhealthy, remove it
                    await RemoveConnectionAsync(key);
                }
            }

            // Create new connection
            await _creationLock.WaitAsync(cancellationToken);
            try
            {
                // Double-check after acquiring lock
                if (_connections.TryGetValue(key, out pooled))
                {
                    if (await IsConnectionHealthyAsync(pooled, cancellationToken))
                    {
                        pooled.LastUsed = DateTime.UtcNow;
                        Statistics.IncrementReused();
                        OnConnectionReused(host, username);
                        return pooled.Client;
                    }
                    else
                    {
                        await RemoveConnectionAsync(key);
                    }
                }

                // Create new connection
                var client = await CreateConnectionAsync(host, username, password, cancellationToken);

                pooled = new PooledConnection
                {
                    Client = client,
                    Key = key,
                    Host = host,
                    Username = username,
                    Created = DateTime.UtcNow,
                    LastUsed = DateTime.UtcNow,
                    LastHealthCheck = DateTime.UtcNow
                };

                _connections[key] = pooled;
                Statistics.IncrementCreated();
                OnConnectionCreated(host, username);

                return client;
            }
            finally
            {
                _creationLock.Release();
            }
        }

        /// <summary>
        /// Creates a shell session from a pooled connection.
        /// </summary>
        /// <param name="host">Host connection details</param>
        /// <param name="username">Username for authentication</param>
        /// <param name="password">Password for authentication</param>
        /// <param name="timeouts">Optional timeout overrides</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An initialized shell session</returns>
        public async Task<(Ssh Client, SshShellSession Session)> CreateSessionAsync(
            HostConnection host,
            string username,
            string password,
            SshTimeoutOptions? timeouts = null,
            CancellationToken cancellationToken = default)
        {
            var client = await GetOrCreateAsync(host, username, password, cancellationToken);
            var effectiveTimeouts = timeouts ?? _defaultTimeouts;

            RebexScripting scripting = client.StartScripting();
            scripting.Timeout = (int)effectiveTimeouts.CommandTimeout.TotalMilliseconds;
            var session = new SshShellSession(client, scripting, effectiveTimeouts);

            await session.InitializeAsync(cancellationToken);

            return (client, session);
        }

        /// <summary>
        /// Removes a connection from the pool.
        /// </summary>
        /// <param name="host">Host to disconnect from</param>
        /// <param name="username">Username used for the connection</param>
        public async Task RemoveAsync(HostConnection host, string username)
        {
            var key = CreateConnectionKey(host, username);
            await RemoveConnectionAsync(key);
        }

        /// <summary>
        /// Removes all connections from the pool.
        /// </summary>
        public async Task ClearAsync()
        {
            var keys = _connections.Keys.ToList();
            foreach (var key in keys)
            {
                await RemoveConnectionAsync(key);
            }
        }

        /// <summary>
        /// Removes stale connections that have exceeded the maximum age.
        /// </summary>
        /// <returns>Number of connections removed</returns>
        public async Task<int> CleanupStaleConnectionsAsync()
        {
            var now = DateTime.UtcNow;
            var staleKeys = _connections
                .Where(kvp => now - kvp.Value.Created > _maxConnectionAge)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in staleKeys)
            {
                await RemoveConnectionAsync(key);
            }

            return staleKeys.Count;
        }

        /// <summary>
        /// Gets information about all connections in the pool.
        /// </summary>
        public IReadOnlyList<ConnectionInfo> GetConnectionInfo()
        {
            return _connections.Values
                .Select(p => new ConnectionInfo
                {
                    Host = p.Host.ToString(),
                    Username = p.Username,
                    Created = p.Created,
                    LastUsed = p.LastUsed,
                    IsConnected = p.Client.IsConnected,
                    Age = DateTime.UtcNow - p.Created
                })
                .ToList();
        }

        private async Task<Ssh> CreateConnectionAsync(
            HostConnection host,
            string username,
            string password,
            CancellationToken cancellationToken)
        {
            var client = new Ssh();
            client.Timeout = (int)_defaultTimeouts.ConnectionTimeout.TotalMilliseconds;

            // Apply algorithm preferences before connecting (from SSH config)
            // TODO: Temporarily disabled to diagnose connection issues
            // ApplyAlgorithmSettings(client, host);

            // Connect and authenticate
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                client.Connect(host.IpAddress, host.Port);

                // Key-based or password authentication
                if (!string.IsNullOrEmpty(host.IdentityFile) && File.Exists(host.IdentityFile))
                {
                    // Use key-based authentication
                    var passphrase = host.IdentityFilePassphrase ?? string.Empty;
                    client.Login(username, new SshPrivateKey(host.IdentityFile, passphrase));
                }
                else
                {
                    // Use password authentication
                    client.Login(username, password);
                }
            }, cancellationToken);

            return client;
        }

        /// <summary>
        /// Applies SSH algorithm preferences from the host connection settings.
        /// These settings typically come from the SSH config file.
        /// </summary>
        private static void ApplyAlgorithmSettings(Ssh client, HostConnection host)
        {
            // Apply host key algorithms if specified
            // Rebex accepts OpenSSH-style algorithm IDs directly
            if (host.HostKeyAlgorithms?.Length > 0)
            {
                client.Settings.SshParameters.SetHostKeyAlgorithms(host.HostKeyAlgorithms);
            }

            // Apply encryption ciphers if specified
            // Rebex accepts OpenSSH-style cipher IDs directly
            if (host.Ciphers?.Length > 0)
            {
                client.Settings.SshParameters.SetEncryptionAlgorithms(host.Ciphers);
            }
        }

        private async Task<bool> IsConnectionHealthyAsync(PooledConnection pooled, CancellationToken cancellationToken)
        {
            // Check if connection age exceeded
            if (DateTime.UtcNow - pooled.Created > _maxConnectionAge)
                return false;

            // Check basic connection state
            if (!pooled.Client.IsConnected)
                return false;

            // Only do active health check if enough time has passed
            if (DateTime.UtcNow - pooled.LastHealthCheck < _healthCheckInterval)
                return true;

            // Perform active health check by running a simple command
            try
            {
                await Task.Run(() =>
                {
                    // Use a simple echo command to verify the connection is working
                    RebexScripting scripting = pooled.Client.StartScripting();
                    scripting.Timeout = 5000; // 5 second timeout for health check
                    scripting.Send("echo 1\r");

                    // Read a small amount of output to confirm connection is responsive
                    var promptEvent = ScriptEvent.FromRegex(@"[#$>%]\s*$");
                    scripting.ReadUntil(promptEvent);
                }, cancellationToken);

                pooled.LastHealthCheck = DateTime.UtcNow;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task RemoveConnectionAsync(string key)
        {
            if (_connections.TryRemove(key, out var pooled))
            {
                try
                {
                    if (pooled.Client.IsConnected)
                    {
                        await Task.Run(() => pooled.Client.Disconnect());
                    }
                    pooled.Client.Dispose();
                    Statistics.IncrementRemoved();
                    OnConnectionRemoved(pooled.Host, pooled.Username);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        private static string CreateConnectionKey(HostConnection host, string username)
        {
            return $"{host.IpAddress}:{host.Port}:{username}";
        }

        protected virtual void OnConnectionCreated(HostConnection host, string username)
        {
            ConnectionCreated?.Invoke(this, new ConnectionEventArgs { Host = host, Username = username });
        }

        protected virtual void OnConnectionReused(HostConnection host, string username)
        {
            ConnectionReused?.Invoke(this, new ConnectionEventArgs { Host = host, Username = username });
        }

        protected virtual void OnConnectionRemoved(HostConnection host, string username)
        {
            ConnectionRemoved?.Invoke(this, new ConnectionEventArgs { Host = host, Username = username });
        }

        protected virtual void OnConnectionError(HostConnection host, string username, Exception exception)
        {
            ConnectionError?.Invoke(this, new ConnectionErrorEventArgs
            {
                Host = host,
                Username = username,
                Exception = exception
            });
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SshConnectionPool));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                foreach (var pooled in _connections.Values)
                {
                    try
                    {
                        if (pooled.Client.IsConnected)
                        {
                            pooled.Client.Disconnect();
                        }
                        pooled.Client.Dispose();
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }

                _connections.Clear();
                _creationLock.Dispose();
            }
        }

        private class PooledConnection
        {
            public Ssh Client { get; set; } = null!;
            public string Key { get; set; } = string.Empty;
            public HostConnection Host { get; set; } = new();
            public string Username { get; set; } = string.Empty;
            public DateTime Created { get; set; }
            public DateTime LastUsed { get; set; }
            public DateTime LastHealthCheck { get; set; }
        }
    }

    /// <summary>
    /// Event arguments for connection pool events.
    /// </summary>
    public class ConnectionEventArgs : EventArgs
    {
        public HostConnection Host { get; set; } = new();
        public string Username { get; set; } = string.Empty;
    }

    /// <summary>
    /// Event arguments for connection errors.
    /// </summary>
    public class ConnectionErrorEventArgs : ConnectionEventArgs
    {
        public Exception Exception { get; set; } = null!;
    }

    /// <summary>
    /// Information about a pooled connection.
    /// </summary>
    public class ConnectionInfo
    {
        public string Host { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public DateTime LastUsed { get; set; }
        public bool IsConnected { get; set; }
        public TimeSpan Age { get; set; }
    }

    /// <summary>
    /// Statistics about connection pool usage.
    /// </summary>
    public class PoolStatistics
    {
        private long _connectionsCreated;
        private long _connectionsReused;
        private long _connectionsRemoved;

        public long ConnectionsCreated => Interlocked.Read(ref _connectionsCreated);
        public long ConnectionsReused => Interlocked.Read(ref _connectionsReused);
        public long ConnectionsRemoved => Interlocked.Read(ref _connectionsRemoved);

        /// <summary>
        /// The ratio of reused connections to total connection requests.
        /// Higher is better (more reuse, less overhead).
        /// </summary>
        public double ReuseRatio
        {
            get
            {
                var total = ConnectionsCreated + ConnectionsReused;
                return total > 0 ? (double)ConnectionsReused / total : 0;
            }
        }

        internal void IncrementCreated() => Interlocked.Increment(ref _connectionsCreated);
        internal void IncrementReused() => Interlocked.Increment(ref _connectionsReused);
        internal void IncrementRemoved() => Interlocked.Increment(ref _connectionsRemoved);

        public void Reset()
        {
            Interlocked.Exchange(ref _connectionsCreated, 0);
            Interlocked.Exchange(ref _connectionsReused, 0);
            Interlocked.Exchange(ref _connectionsRemoved, 0);
        }
    }
}
