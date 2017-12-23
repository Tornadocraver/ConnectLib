using ConnectLib.Types;

using System;
using System.Collections.Generic;

namespace ConnectLib.Data
{
    public class Command
    {
        #region Constructors
        public Command()
        {
            
        }
        public Command(ClientInformation target, ClientInformation sender, CommandType type, params CommandOption[] options)
        {
            Target = target;
            Sender = sender;
            Type = type;
            Options = (options == null) ? new CommandOption[]{ CommandOption.None } : options;
        }
        #endregion

        #region Variables
        /// <summary>
        /// A user-supplied collection of CommandOption objects.
        /// </summary>
        public CommandOption[] Options { get; set; }
        /// <summary>
        /// User-supplied properties of the Command object.
        /// </summary>
        public Dictionary<string, object> Properties { get; set; }
        /// <summary>
        /// A DateTime object representing when the Command was received.
        /// </summary>
        public DateTime Received { get; private set; } = DateTime.MinValue;
        /// <summary>
        /// A ClientInformation object representing the sender of the Command.
        /// </summary>
        public ClientInformation Sender { get; set; }
        /// <summary>
        /// A DateTime object representing when the Command was sent.
        /// </summary>
        public DateTime Sent { get; private set; } = DateTime.MinValue;
        /// <summary>
        /// A ClientInformation object representing the target (recipient) of the Command.
        /// </summary>
        public ClientInformation Target { get; set; }
        /// <summary>
        /// A CommandType object representing the type of the Command.
        /// </summary>
        public CommandType Type { get; set; } = CommandType.None;
        #endregion
    }
}