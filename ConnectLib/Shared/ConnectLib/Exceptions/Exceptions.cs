using ConnectLib.Data;
using System;

namespace ConnectLib.Exceptions
{
    /// <summary>
    /// Thrown when an error occurrs regarding a Command.
    /// </summary>
    public class CommandException : Exception
    {
        public Command Command { get; }
        public override string Message { get; }

        public CommandException(string errorMessage, Command command)
        {
            Message = errorMessage;
            Command = command;
        }
    }
    /// <summary>
    /// Thrown when an error occurs while connecting to a session.
    /// </summary>
    public class ConnectException : Exception
    {
        public override string Message { get; }

        public ConnectException(string errorMessage)
        {
            Message = errorMessage;
        }
    }
    /// <summary>
    /// Thrown when an invalid name is supplied.
    /// </summary>
    public class NameException : Exception
    {
        public string OffendingName { get; }
        public override string Message { get; }

        public NameException(string errorMessage, string offendingName)
        {
            Message = errorMessage;
            OffendingName = offendingName;
        }
    }
    /// <summary>
    /// Thrown when a network error occurs.
    /// </summary>
    public class NetworkException : Exception
    {
        public override string Message { get; }

        public NetworkException()
        {
            Message = "You must be connected to a network.";
        }
        public NetworkException(string errorMessage)
        {
            Message = errorMessage;
        }
    }
}