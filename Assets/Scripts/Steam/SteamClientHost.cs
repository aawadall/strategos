// SteamClientHost.cs
// #302: one Init/Shutdown for the process. Defaults to NullSteamClient until a real
// Steamworks package implementation is registered.

namespace Strategos.Steam
{
    /// <summary>Process-wide Steam client holder.</summary>
    public static class SteamClientHost
    {
        private static ISteamClient _client;
        private static bool _inited;

        /// <summary>Active client (never null after <see cref="Ensure"/>).</summary>
        public static ISteamClient Client => Ensure();

        /// <summary>
        /// Install a client implementation (tests / future Steamworks.NET wrapper).
        /// Call before <see cref="Bootstrap"/> if replacing the null default.
        /// </summary>
        public static void SetClient(ISteamClient client)
        {
            if (_inited) Shutdown();
            _client = client;
        }

        /// <summary>Ensure a client exists (NullSteamClient by default).</summary>
        public static ISteamClient Ensure()
        {
            return _client ??= new NullSteamClient();
        }

        /// <summary>Init once at boot (#302). Safe without App ID / Steam.</summary>
        public static bool Bootstrap()
        {
            var client = Ensure();
            if (_inited) return client.IsAvailable;
            _inited = true;
            return client.Init();
        }

        /// <summary>Shutdown once at quit.</summary>
        public static void Shutdown()
        {
            if (!_inited) return;
            _inited = false;
            _client?.Shutdown();
        }

        /// <summary>Reset host state (probes).</summary>
        public static void ResetForProbe()
        {
            Shutdown();
            _client = null;
        }
    }
}
