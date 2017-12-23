namespace ConnectLib.Types
{
    public enum ClientState
    {
        /// <summary>
        /// Indicates that the Client's credentials are being authenticated.
        /// </summary>
        Authenticating,
        /// <summary>
        /// Indicates a successful connection has taken place.
        /// </summary>
        Connected,
        /// <summary>
        /// Indicates that the Client is connecting to a remote host.
        /// </summary>
        Connecting,
        /// <summary>
        /// Indicates a Client with no active connections.
        /// </summary>
        None
    }
}