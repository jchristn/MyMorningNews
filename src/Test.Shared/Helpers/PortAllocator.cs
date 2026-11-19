namespace Test.Shared.Helpers
{
    using System.Net;
    using System.Net.Sockets;

    /// <summary>
    /// Allocates free loopback TCP ports for in-process servers.
    /// </summary>
    public static class PortAllocator
    {
        /// <summary>
        /// Get a free port on 127.0.0.1.
        /// </summary>
        /// <returns>Port number.</returns>
        public static int GetFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
