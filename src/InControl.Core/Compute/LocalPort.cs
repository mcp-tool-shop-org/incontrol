using System.Net;
using System.Net.Sockets;

namespace InControl.Core.Compute;

/// <summary>
/// Asks the OS for an unused loopback port. The SSH config then binds that exact port.
/// </summary>
public static class LocalPort
{
    public static int Reserve()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
