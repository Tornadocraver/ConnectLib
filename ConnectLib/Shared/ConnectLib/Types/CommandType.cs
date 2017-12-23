namespace ConnectLib.Types
{
    public enum CommandType
    {
        /// <summary>
        /// Requests that the remote host begins authenticating the Client.
        /// </summary>
        Authenticate,
        /// <summary>
        /// Indicates that the Client's credentials are valid.
        /// </summary>
        Authorized,
        /// <summary>
        /// Indicates that a Client has joined the session and it's imformation is in the Command's Sender property.
        /// </summary>
        ClientAdded,
        /// <summary>
        /// Indicates that a Client has left the session and it's information is in the Command's Sender property.
        /// </summary>
        ClientRemoved,
        /// <summary>
        /// Indicates either a Client or a Server is closing.
        /// </summary>
        Close,
        /// <summary>
        /// Indicates a custom Command that is not otherwise implemented in the ConnectLib code.
        /// </summary>
        Custom,
        /// <summary>
        /// Indicates an empty Command.
        /// </summary>
        None,
        /// <summary>
        /// Indicates that the Client's credentials are invalid.
        /// </summary>
        Unauthorized
    }
}