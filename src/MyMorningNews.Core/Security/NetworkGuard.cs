namespace MyMorningNews.Core.Security
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Exceptions;

    /// <summary>
    /// Server-side request forgery protection (plan section 33.3). When enabled (Hosting.RestrictPrivateNetworks, on for
    /// the hosted service), outbound requests to loopback, private, link-local, carrier-grade NAT, unique-local,
    /// multicast, unspecified, and cloud metadata addresses are refused. The handler from <see cref="CreateHandler"/>
    /// checks the address actually connected to, after DNS resolution and on every redirect, so DNS rebinding cannot
    /// slip past it. Thread safe.
    /// </summary>
    public static class NetworkGuard
    {
        #region Public-Members

        /// <summary>
        /// Whether the guard is on. Read on every connection so a settings change applies at once. Default off.
        /// </summary>
        public static Func<bool> IsEnabled { get; set; } = () => false;

        /// <summary>
        /// Host names allowed even though they resolve to private addresses (the operator's own services). Read on every
        /// check. Default none.
        /// </summary>
        public static Func<IReadOnlyCollection<string>> AllowedHosts { get; set; } = () => Array.Empty<string>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Whether an address is one the guard refuses.
        /// </summary>
        /// <param name="ip">Address.</param>
        /// <returns>True when blocked.</returns>
        /// <exception cref="ArgumentNullException">Address is null.</exception>
        public static bool IsBlockedAddress(IPAddress ip)
        {
            ArgumentNullException.ThrowIfNull(ip);
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.None)) return true;
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] b = ip.GetAddressBytes();
                if (b[0] == 10 || b[0] == 0 || b[0] == 127) return true;
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
                if (b[0] == 192 && b[1] == 168) return true;
                if (b[0] == 169 && b[1] == 254) return true;
                if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true;
                if (b[0] == 192 && b[1] == 0 && b[2] == 0) return true;
                if (b[0] >= 224) return true;
                return false;
            }

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;
                byte[] b = ip.GetAddressBytes();
                if ((b[0] & 0xFE) == 0xFC) return true;
                if (b[0] == 0xFD && b[1] == 0x00 && b[2] == 0x0E && b[3] == 0xC2) return true;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Refuse a URL whose host resolves to a blocked address. Does nothing when the guard is off.
        /// </summary>
        /// <param name="url">URL.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="EgressBlockedException">The host is or resolves to a blocked address.</exception>
        public static async Task EnsureAllowedAsync(string? url, CancellationToken token = default)
        {
            if (!IsEnabled()) return;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new EgressBlockedException("Only http and https URLs are allowed.");
            if (IsAllowedHost(uri.Host)) return;
            if (String.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
                throw new EgressBlockedException("Requests to " + uri.Host + " are not allowed on this server.");
            IPAddress[] addresses;
            if (IPAddress.TryParse(uri.Host.Trim('[', ']'), out IPAddress? literal)) addresses = new[] { literal };
            else
            {
                try
                {
                    addresses = await Dns.GetHostAddressesAsync(uri.Host, token).ConfigureAwait(false);
                }
                catch (SocketException)
                {
                    return;
                }
            }

            if (addresses.Any(IsBlockedAddress)) throw new EgressBlockedException("Requests to private or internal network addresses (" + uri.Host + ") are not allowed on this server.");
        }

        /// <summary>
        /// An HTTP handler that checks every connection's resolved address when the guard is on, so redirects and DNS
        /// changes are covered. Redirects are followed by the handler (up to 10).
        /// </summary>
        /// <returns>Handler.</returns>
        public static SocketsHttpHandler CreateHandler()
        {
            return new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                ConnectCallback = async (context, token) =>
                {
                    IPAddress[] addresses = IPAddress.TryParse(context.DnsEndPoint.Host.Trim('[', ']'), out IPAddress? literal)
                        ? new[] { literal }
                        : await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token).ConfigureAwait(false);
                    if (IsEnabled() && !IsAllowedHost(context.DnsEndPoint.Host))
                    {
                        addresses = addresses.Where(a => !IsBlockedAddress(a)).ToArray();
                        if (addresses.Length == 0) throw new EgressBlockedException("Requests to private or internal network addresses (" + context.DnsEndPoint.Host + ") are not allowed on this server.");
                    }

                    Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, token).ConfigureAwait(false);
                        return new NetworkStream(socket, true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                }
            };
        }

        #endregion

        #region Private-Methods

        private static bool IsAllowedHost(string host)
        {
            return AllowedHosts().Any(h => String.Equals(h?.Trim(), host, StringComparison.OrdinalIgnoreCase));
        }

        #endregion
    }
}
