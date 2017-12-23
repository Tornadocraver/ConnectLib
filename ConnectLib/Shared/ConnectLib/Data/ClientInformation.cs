using ConnectLib.Types;
using ConnectLib.Networking;

using System;
using System.Net;

namespace ConnectLib.Data
{
    /// <summary>
    /// An object containing information specific to a computer.
    /// </summary>
    public class ClientInformation
    {
        #region Constructors
        /// <summary>
        /// Creates a new ClientInformation object representing the current computer.
        /// </summary>
        public ClientInformation()
        {
            ID = Tools.GetUniqueID();
        }
        /// <summary>
        /// Creates a new ClientInformation object representing the current computer.
        /// </summary>
        /// <param name="ip">The IPAddress of the current computer.</param>
        /// <param name="name">The name of the current computer.</param>
        /// <param name="state">The ClientState of the current computer.</param>
        /// <param name="timeConnected">The time when the computer connected to the remote session.</param>
        public ClientInformation(IPAddress ip, string name, ClientState state, DateTime timeConnected) : this()
        {
            IP = ip;
            Name = name;
            State = state;
            TimeConnected = timeConnected;
        }
        #endregion

        #region Variables
        /// <summary>
        /// A unique string representing the current machine.
        /// </summary>
        public string ID { get; set; }
        /// <summary>
        /// An IPAddress representing the IP of the ClientInformation object.
        /// </summary>
        public IPAddress IP { get; set; }
        /// <summary>
        /// A string representing the name of the ClientInformation object.
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// A ClientState object representing the current state of the ClientInformation object. Initial value is ClientState.None.
        /// </summary>
        public ClientState State { get; set; } = ClientState.None;
        /// <summary>
        /// A DateTime object representing when the client originally connected. Initial value is DateTime.MinValue.
        /// </summary>
        public DateTime TimeConnected { get; set; } = DateTime.MinValue;
        #endregion
    }
}