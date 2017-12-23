using ConnectLib.Data;
using ConnectLib.Exceptions;
using ConnectLib.Networking;
using ConnectLib.Types;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Threading;

namespace ConnectLib
{
    /// <summary>
    /// Allows a computer to connect to a remote session (Server or another Client) and transmit Command's. Also allows for custom Command implementation
    /// </summary>
    public class Client : IDisposable
    {
        #region Connection Handlers
        /// <summary>
        /// Handles incomming commands from the remote host.
        /// </summary>
        private void HandleCommands()
        {
            try
            {
                Authenticate();
                while (Active)
                {
                    if (!ClientInterface.Connections["Main"].DataAvailable || Pausing)
                        Thread.Sleep(10);
                    Command command = ClientInterface.Connections["Main"].Read<Command>(Password);
                    if (command != null)
                    {
                        switch (command.Type)
                        {
                            case CommandType.Authenticate:
                            case CommandType.Authorized:
                            case CommandType.None:
                            case CommandType.Unauthorized:
                                break;
                            case CommandType.ClientAdded:
                                Clients.Add(command.Sender.ID, command.Sender);
                                OnClientConnected?.Invoke(command.Sender);
                                break;
                            case CommandType.ClientRemoved:
                                Clients.Remove(command.Sender.ID);
                                OnClientDisconnected?.Invoke(command.Sender);
                                break;
                            case CommandType.Close:
                                throw new ThreadInterruptedException();
                            case CommandType.Custom:
                                OnCustomCommand?.Invoke(command);
                                break;
                            default:
                                throw new CommandException($"The command type \"{command.Type}\" was not recognized in this context.", command);
                        }
                    }
                }
            }
            catch (ThreadInterruptedException) { }
            catch (Exception error) { OnError?.Invoke(error); }
            finally { if (Connected) { Disconnect(true); } }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new Client object with the specified information.
        /// </summary>
        /// <param name="name">The name of the Client.</param>
        /// <param name="internalConnection">Indicates whether the Client will be conencting on a local network (true) or the internet (false).</param>
        /// <param name="peerToPeer">Indicates whether the Client will be connecting to a peer (true) or server (false).</param>
        /// <param name="password">The password used to encrypt/decrypt transmissions to/from the remote host.</param>
        public Client(string name, bool internalConnection, bool peerToPeer, SecureString password)
        {
            if (name.Contains("{") || name.Contains("}"))
                throw new NameException("Names are not allowed to contain curly-brackets!", name);
            ClientInterface = new ClientObject(new ClientInformation((internalConnection) ? Tools.GetInternalIP() : Tools.GetExternalIP(), name, ClientState.None, DateTime.Now));
            P2P = peerToPeer;
            Password = password;
        }
        /// <summary>
        /// A finalizer for the Client object.
        /// </summary>
        ~Client()
        {
            Dispose(false);
        }
        #endregion

        #region Controls
        /// <summary>
        /// Connects the Client to the specified remote host.
        /// </summary>
        /// <param name="remoteHost">The IPAddress of the remote host.</param>
        /// <param name="remotePort">The port of the remote host.</param>
        public virtual void Connect(IPAddress remoteHost, int remotePort)
        {
            if (!Tools.HasConnection())
                throw new NetworkException();
            if (Active)
                throw new InvalidOperationException("Cannot connect twice.");
            Active = true;
            Connecting = true;
            Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            try
            {
                if (P2P) //WORK IN PROGRESS
                {
                    using (Socket listener = new Socket(SocketType.Stream, ProtocolType.Tcp))
                    {
                        listener.Bind(new IPEndPoint(IPAddress.Loopback, remotePort));
                        listener.Listen(1);
                        ClientInterface.Connections.Add("Main", new Connection(null, true, new Thread(() => { HandleCommands(); })));
                        while (Connecting)
                        {
                            try
                            {
                                ClientInterface.Connections["Main"].Socket.Connect(remoteHost, remotePort);
                                P2PServer = false;
                                ClientInterface.Connections["Main"].StartHandler();
                            }
                            catch { }
                            if (listener.Poll(10000, SelectMode.SelectRead))
                            {
                                ClientInterface.Connections["Main"].Socket = listener.Accept();
                                P2PServer = true;
                                ClientInterface.Connections["Main"].StartHandler();
                            }
                        }
                    }
                }
                else
                {
                    socket.Connect(remoteHost, remotePort);
                    ClientInterface.Connections.Add("Main", new Connection(socket, true, new Thread(() => { HandleCommands(); })));
                    ClientInterface.Connections["Main"].StartHandler();
                }
            }
            catch (SocketException error) { Disconnect(false); throw new ConnectException(error.Message); }
            catch (Exception error) { Disconnect(false); OnError?.Invoke(error); }
        }
        /// <summary>
        /// Disconnects the Client from the remote host if connected.
        /// </summary>
        public virtual void Disconnect()
        {
            Disconnect(true);
        }

        /// <summary>
        /// Disconnects the Client from the remote host if connected.
        /// </summary>
        /// <param name="sendDisconnect">Indicates whether the remote host should be notified about the disconnect.</param>
        protected void Disconnect(bool sendDisconnect)
        {
            Active = false;
            Connecting = false;
            try
            {
                if (ClientInterface.Connections.ContainsKey("Main") && sendDisconnect)
                    ClientInterface.Connections["Main"].Write(Password, new Command(null, ClientInterface.Information, CommandType.Close, CommandOption.Broadcast));
            }
            catch (Exception error) { OnError?.Invoke(error); }
            finally { Connected = false; ClientInterface?.Dispose(); ClientInterface = null; Clients.Clear(); }
        }
        /// <summary>
        /// Pauses or unpauses the message handler.
        /// </summary>
        /// <param name="pausing">Indicates whether to pause (true) or unpause (false).</param>
        protected void Pause(bool pausing)
        {
            if (pausing)
                Pausing = true;
            else
                Pausing = false;
        }
        #endregion

        #region Disposing
        /// <summary>
        /// Releases all resources used by the Client object.
        /// </summary>
        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all resources user by the Client object.
        /// </summary>
        /// <param name="disposing">Indicates whether the Dispose method is being called by the Finalizer (false) or other code (true).</param>
        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
                return;
            if (disposing)
            {
                if (Active)
                    Disconnect();
                Clients = null;
                P2P = false;
                P2PServer = false;
                Password = null;
                Pausing = false;
            }
            Disposed = true;
        }
        #endregion

        #region Events
        public delegate void ClientDisconnectedEventHandler(ClientInformation client);
        /// <summary>
        /// Triggered when a Client disconnects from the remote session (if connected to a Server).
        /// </summary>
        public event ClientDisconnectedEventHandler OnClientDisconnected;

        public delegate void CustomCommandReceived(Command command);
        /// <summary>
        /// Triggered when a command is receives that is not internally implemented in the Client's code.
        /// </summary>
        public event CustomCommandReceived OnCustomCommand;

        public delegate void NewClientEventHandler(ClientInformation newClient);
        /// <summary>
        /// Triggered when a new Client connects to the remote session (if connected to a Server).
        /// </summary>
        public event NewClientEventHandler OnClientConnected;

        public delegate void ErrorEventHandler(Exception exception);
        /// <summary>
        /// Triggered when an exception is thrown.
        /// </summary>
        public event ErrorEventHandler OnError;
        /// <summary>
        /// Triggers the OnError event in derrived classes.
        /// </summary>
        /// <param name="exception"></param>
        protected void RaiseOnError(Exception exception)
        {
            OnError?.Invoke(exception);
        }
        #endregion

        #region Other Methods
        /// <summary>
        /// Determines whether the current computer is authorized to access the remote host.
        /// </summary>
        /// <param name="connection">The Connection to the remote host.</param>
        private void Authenticate()
        {
            try
            {
                if (P2PServer)
                {
                    try
                    {
                        Command unencrypted = ClientInterface.Connections["Main"].Read<Command>();
                        Command encrypted = ClientInterface.Connections["Main"].Read<Command>(Password);
                        if (unencrypted != null || encrypted != null)
                        {
                            ClientInterface.Connections["Main"].Write(new Command(encrypted.Sender, ClientInterface.Information, CommandType.Authorized));
                            ConnectionSuccessful();
                        }
                        else
                            throw new JsonException();
                    }
                    catch (JsonException) { ClientInterface.Connections["Main"].Write(new Command(null, ClientInterface.Information, CommandType.Unauthorized)); }
                }
                else
                {
                    ClientInterface.Information.State = ClientState.Authenticating;
                    ClientInterface.Connections["Main"].Write(new Command(null, ClientInterface.Information, CommandType.Authenticate));
                    ClientInterface.Connections["Main"].Write(Password, new Command(null, ClientInterface.Information, CommandType.Authenticate));
                    Command response = ClientInterface.Connections["Main"].Read<Command>();
                    if (response.Type == CommandType.Authorized)
                    {
                        Clients.Add(response.Sender.ID, response.Sender);
                        ConnectionSuccessful();
                    }
                    else
                        throw new ConnectException("Authentication rejected by the remote host.");
                }
            }
            catch (Exception error) { OnError?.Invoke(error); }
        }
        /// <summary>
        /// Called when the Client has connected to and been authenticated by the remote host.
        /// </summary>
        private void ConnectionSuccessful()
        {
            ClientInterface.Information.TimeConnected = DateTime.Now;
            ClientInterface.Information.State = ClientState.Connected;
            Connecting = false;
            Connected = true;
        }
        #endregion

        #region Variables
        /// <summary>
        /// Indicates whether the Client is active. Initial value is false.
        /// </summary>
        public bool Active { get; private set; } = false;
        /// <summary>
        /// Indicates whether the Client is connected to a remote session. Initial value is false.
        /// </summary>
        public bool Connected { get; private set; } = false;
        /// <summary>
        /// A ClientObject used for all transmissions within the Client object.
        /// </summary>
        public ClientObject ClientInterface { get; private set; }
        /// <summary>
        /// Indicates whether the Client is connecting to a peer (true) or server (false).
        /// </summary>
        public bool P2P { get; private set; } = false;

        /// <summary>
        /// A collection of ClientInformation objects representing the Clients connected to the session.
        /// </summary>
        protected Dictionary<string, ClientInformation> Clients { get; set; } = new Dictionary<string, ClientInformation>();
        /// <summary>
        /// Indicates whether the Client is in the process of connecting to a remote session. Initial value is false.
        /// </summary>
        protected bool Connecting { get; private set; } = false;
        /// <summary>
        /// A password used to encrypt and decrypt data transmissions.
        /// </summary>
        protected SecureString Password { get; set; }
        /// <summary>
        /// Indicates whether the command handler is currently paused.
        /// </summary>
        protected bool Pausing { get; private set; }

        /// <summary>
        /// Indicates whether the current Client object has been disposed. Initial value is false.
        /// </summary>
        private bool Disposed = false;
        /// <summary>
        /// Indicates whether the current Client object is acting as the server in Peer-to-Peer mode.
        /// </summary>
        private bool P2PServer = false;
        #endregion
    }
}