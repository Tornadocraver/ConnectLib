using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace ConnectLib.Networking
{
    public static class Tools
    {
        /// <summary>
        /// Returns whether the current computer has a connection to a network (true).
        /// </summary>
        /// <returns>A bool indicating the state of the computer.</returns>
        public static bool HasConnection()
        {
            return NetworkInterface.GetIsNetworkAvailable();
        }

        /// <summary>
        /// Returns the external (remote) IPAddress of the current computer.
        /// </summary>
        /// <returns>An external IPAddress.</returns>
        public static IPAddress GetExternalIP()
        {
            try { return IPAddress.Parse((new WebClient()).DownloadString("https://api.ipify.org")); }
            catch { return GetInternalIP(); }
        }
        /// <summary>
        /// Returns the internal (local) IPAddress of the current computer.
        /// </summary>
        /// <returns>An internal IPAddress.</returns>
        public static IPAddress GetInternalIP()
        {
            if (!HasConnection())
                return IPAddress.Loopback;
            try { return Dns.GetHostEntry(Dns.GetHostName()).AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork); }
            catch { return IPAddress.Loopback; }
        }
        
        /// <summary>
        /// Creates a unique ID to represent the current computer.
        /// </summary>
        /// <returns>A string containg the uniqure ID.</returns>
        public static string GetUniqueID()
        {
            StringBuilder builder = new StringBuilder();
            Enumerable.Range(65, 26).Select(e => ((char)e).ToString()).Concat(Enumerable.Range(97, 26).Select(e => ((char)e).ToString())).Concat(Enumerable.Range(0, 10).Select(e => e.ToString())).OrderBy(e => Guid.NewGuid()).Take(32).ToList().ForEach(e => builder.Append(e));
            return builder.ToString();
        }
    }
}