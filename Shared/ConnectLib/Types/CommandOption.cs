namespace ConnectLib.Types
{
    public enum CommandOption
    {
        /// <summary>
        /// Requests that the Server broadcasts the Command to all connected Client's.
        /// </summary>
        Broadcast,
        /// <summary>
        /// Requests that the Server forwards the Command to the target.
        /// </summary>
        Forward,
        /// <summary>
        /// Indicates no special CommandOption's.
        /// </summary>
        None
    }
}