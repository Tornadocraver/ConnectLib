using ConnectLib.Data;
using ConnectLib.Exceptions;
using ConnectLib.Networking;
using ConnectLib.Types;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace ConnectLib
{
    /// <summary>
    /// Allows Client's to connect to a session and transmit Command's. Also allows for custom Command implementation.
    /// </summary>
    public class Server : IDisposable
    {
        #region Connection Handlers
        /// <summary>
        /// Handles incomming transmissions from the remote host.
        /// </summary>
        /// <param name="thisClient">The ClientObject to handle transmissions from.</param>
        private void HandleClient(ClientObject thisClient)
        {
            try
            {
                Authenticate(thisClient);
                SendConnectedClients(thisClient);
                while (Active)
                {
                    if (thisClient.Connections["Main"].DataAvailable && !Pausing)
                    {
                        Command command = thisClient.Connections["Main"].Read<Command>(Password);
                        if (command != null)
                        {
                            if (command.Options.Contains(CommandOption.Forward))
                                Clients.Values.First(c => c.Information.ID == command.Target.ID).Connections["Main"].Write(Password, command);
                            else
                            {
                                switch (command.Type)
                                {
                                    case CommandType.Authenticate:
                                    case CommandType.Authorized:
                                    case CommandType.ClientAdded:
                                    case CommandType.ClientRemoved:
                                    case CommandType.None:
                                    case CommandType.Unauthorized:
                                        break;
                                    case CommandType.Close:
                                        {
                                            if (command.Options.Contains(CommandOption.Broadcast))
                                                ClientRemoved(thisClient);
                                            throw new ThreadInterruptedException();
                                        }
                                    case CommandType.Custom:
                                        {
                                            if (command.Options.Contains(CommandOption.Broadcast))
                                                Broadcast(Information, command);
                                            OnCustomCommand?.Invoke(command);
                                            break;
                                        }
                                    default:
                                        throw new CommandException($"The command type \"{command.Type}\" was not recognized in this context. Time: {DateTime.Now.ToString()}", command);
                                }
                            }
                        }
                    }
                    Thread.Sleep(10);
                }
            }
            catch (ThreadInterruptedException) { }
            catch (Exception error) { OnError?.Invoke(error); }
            finally { new Thread(() => { Thread.Sleep(1000); thisClient.Dispose(); }).Start(); }
        }
        /// <summary>
        /// Listens for incomming connections on the specified port.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        private void ListenForConnections(int port)
        {
            using (Socket listener = new Socket(SocketType.Stream, ProtocolType.Tcp))
            {
                try
                {
                    listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
                    listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    listener.Bind(new IPEndPoint(Information.IP, port));
                    listener.Listen(100);
                    while (Listening)
                    {
                        if (listener.Poll(0, SelectMode.SelectRead))
                        {
                            ClientObject client = new ClientObject();
                            client.Connections.Add("Main", new Connection(listener.Accept(), true, new Thread(() => { HandleClient(client); })));
                            client.Connections["Main"].StartHandler();
                        }
                        else
                            Thread.Sleep(10);
                    }
                }
                catch (ThreadInterruptedException) { }
                catch (Exception error) { OnError?.Invoke(error); }
                finally { Listening = false; listener.Close(); }
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new Server object with the specified information.
        /// </summary>
        /// <param name="internalConnection">Indicates whether the Server will be hosted on a local network (true) or the internet (false).</param>
        /// <param name="password">The password used to encrypt/decrypt transmissions to/from remote hosts.</param>
        public Server(SecureString password)
        {
            Information = new ClientInformation(Tools.GetInternalIP(), "{server}", ClientState.Connected, DateTime.Now);
            Password = password;
        }
        /// <summary>
        /// A finalizer for the Server object.
        /// </summary>
        ~Server()
        {
            Dispose(false);
        }
        #endregion

        #region Controls
        /// <summary>
        /// Starts listening for connections on the specified port.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        public virtual async Task Start(int port)
        {
            if (!Active)
            {
                Port = port;
                await StartListening(port);
            }
            else
            {
                await Stop();
                await Start(port);
            }
        }
        /// <summary>
        /// Stops listening and closes all connections to remote hosts.
        /// </summary>
        public virtual async Task Stop()
        {
            await StopListening();
            await Broadcast(new Command(null, Information, CommandType.Close, null));
            ClearClients();
            Active = false;
        }
        /// <summary>
        /// Broadcasts the specified command(s) to all connected hosts.
        /// </summary>
        /// <param name="commands">The command(s) to be broadcast.</param>
        public async Task Broadcast(params Command[] commands)
        {
            try
            {
                foreach (ClientObject client in Clients.Values)
                    await Send(client, commands);
            }
            catch (Exception error) { OnError?.Invoke(error); }
        }
        /// <summary>
        /// Broadcasts the specified command(s) to all connected hosts.
        /// </summary>
        /// <param name="exclude">The ClientInformation to exclude from the broadcast.</param>
        /// <param name="commands">The command(s) to be broadcast.</param>
        public async Task Broadcast(ClientInformation exclude, params Command[] commands)
        {
            try
            {
                foreach (ClientObject client in Clients.Values)
                    if (client.Information.ID != exclude.ID)
                        Send(client, commands);
            }
            catch (Exception error) { OnError?.Invoke(error); }
        }
        /// <summary>
        /// Sends the specified command(s) to the specified host.
        /// </summary>
        /// <param name="target">The ClientInformation to send to.</param>
        /// <param name="commands">The command(s) to be sent.</param>
        public async Task Send(ClientInformation target, params Command[] commands)
        {
            try { Send(Clients[target.ID], commands); }
            catch (Exception error) { OnError?.Invoke(error); }
        }
        /// <summary>
        /// Sends the specified command(s) to the specified host.
        /// </summary>
        /// <param name="target">The ClientObject to send to.</param>
        /// <param name="commands">The command(s) to be sent.</param>
        public async Task Send(ClientObject target, params Command[] commands)
        {
            try { target.Connections["Main"].Write(Password, commands); }
            catch (Exception error) { OnError?.Invoke(error); }
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
        /// <summary>
        /// Starts listening for connections on the specified port.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        protected async Task StartListening(int port)
        {
            try
            {
                if (!Tools.HasConnection())
                    throw new NetworkException();
                if (port <= 0)
                    throw new ArgumentException("The port must be greater than zero.");
                if (Listening)
                    throw new InvalidOperationException("Cannot start listening more than once.");
                ConnectionListener = new Thread(() => { Active = true; Listening = true; ListenForConnections(port); });
                ConnectionListener.Start();
            }
            catch (Exception error) { StopListening(); OnError?.Invoke(error); }
        }
        /// <summary>
        /// Stops listening and continues to handle remote hosts that were previously connected.
        /// </summary>
        protected async Task StopListening()
        {
            try
            {
                Port = -1;
                Listening = false;
                ConnectionListener?.Interrupt();
                ConnectionListener?.Join();
            }
            catch (Exception error) { OnError?.Invoke(error); }
            finally { ConnectionListener = null; }
        }
        #endregion

        #region Disposing
        /// <summary>
        /// Releases all resources used by the Server object.
        /// </summary>
        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all resources used by the Server object.
        /// </summary>
        /// <param name="disposing">Indicates whether the Dispose method is being called by the Finalizer (false) or other code (true).</param>
        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
                return;
            if (disposing)
            {
                if (Active)
                    Stop();
                Clients = null;
                ConnectionListener = null;
                Information = null;
                Password = null;
                Pausing = false;
            }
            Disposed = true;
        }
        #endregion

        #region Events
        public delegate void ClientDisconnectedEventHandler(ClientInformation client);
        /// <summary>
        /// Triggered when a client disconnects from the Server.
        /// </summary>
        public event ClientDisconnectedEventHandler OnClientDisconnected;

        public delegate void CustomCommandReceived(Command command);
        /// <summary>
        /// Triggered when a command is received that is not internally implemented by the Server.
        /// </summary>
        public event CustomCommandReceived OnCustomCommand;

        public delegate void NewClientEventHandler(ClientInformation newClient);
        /// <summary>
        /// Triggered when a new client connects to the Server.
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
        private async Task Run()
        {
            Task.Factory.
        }

        /// <summary>
        /// Determines whether the remote host is authorized to access the session.
        /// </summary>
        /// <param name="client">The ClientObject to authenticate.</param>
        private void Authenticate(ClientObject client)
        {
            try
            {
                Command unencrypted = client.Connections["Main"].Read<Command>();
                Command encrypted = client.Connections["Main"].Read<Command>(Password);
                if (unencrypted != null || encrypted != null)
                {
                    client.Information = encrypted.Sender;
                    client.Connections["Main"].Write(new Command(encrypted.Sender, Information, CommandType.Authorized));
                    ClientAdded(client);
                }
                else
                    throw new JsonException();
            }
            catch (JsonException) { client.Connections["Main"].Write(new Command(null, client.Information, CommandType.Unauthorized)); throw new ThreadInterruptedException(); }
            catch (Exception error) { OnError?.Invoke(error); }
        }
        /// <summary>
        /// Disposes each ClientObject in the Clients collection and then clears it.
        /// </summary>
        private void ClearClients()
        {
            try
            {
                foreach (ClientObject client in Clients.Values)
                    client.Dispose();
            }
            catch (Exception error) { OnError?.Invoke(error); }
            finally { Clients.Clear(); }
        }
        /// <summary>
        /// Called when a remote host connects to the session.
        /// </summary>
        /// <param name="newClient">The ClientObject representing the remote host.</param>
        private void ClientAdded(ClientObject newClient)
        {
            Clients.Add(newClient.Information.ID, newClient);
            Broadcast(newClient.Information, new Command(null, newClient.Information, CommandType.ClientAdded));
            OnClientConnected?.Invoke(newClient.Information);
        }
        /// <summary>
        /// Called when a remote host disconnects from the session.
        /// </summary>
        /// <param name="oldClient">The ClientObject representing the remote host.</param>
        private void ClientRemoved(ClientObject oldClient)
        {
            Clients.Remove(oldClient.Information.ID);
            Broadcast(oldClient.Information, new Command(null, oldClient.Information, CommandType.ClientRemoved));
            OnClientDisconnected?.Invoke(oldClient.Information);
        }
        /// <summary>
        /// Sends the information of each connected client to the new client.
        /// </summary>
        /// <param name="client">The newly-connected ClientObject.</param>
        private void SendConnectedClients(ClientObject client)
        {
            foreach (ClientObject cli in Clients.Values)
                if (cli.Information.ID != client.Information.ID)
                    client.Connections["Main"].Write(Password, new Command(client.Information, cli.Information, CommandType.ClientAdded));
        }
        #endregion

        #region Variables
        /// <summary>
        /// Inidicates whether the Server is active. Initial value is false.
        /// </summary>
        public bool Active { get; private set; } = false;
        /// <summary>
        /// Indicates the number of remote hosts connected to the Server.
        /// </summary>
        public int Count { get { return (Clients != null) ? Clients.Count : 0; } }
        /// <summary>
        /// Indicates whether the Server is actively listening for new connections. Initial value is false.
        /// </summary>
        public bool Listening { get; private set; } = false;
        /// <summary>
        /// A ClientInformation object containing the Server's information.
        /// </summary>
        public ClientInformation Information { get; private set; } 
        /// <summary>
        /// The port number that the server is currently listening on.
        /// </summary>
        public int Port { get; private set; } = -1;

        /// <summary>
        /// A collection of ClientObject objects representing the Clients connected to the Server.
        /// </summary>
        protected Dictionary<string, ClientObject> Clients { get; set; } = new Dictionary<string, ClientObject>();
        /// <summary>
        /// A Thread that listens for new client connections.
        /// </summary>
        protected Thread ConnectionListener { get; set; }
        /// <summary>
        /// A password used to encrypt and decrypt data transmissions.
        /// </summary>
        protected SecureString Password { get; set; }
        /// <summary>
        /// Indicates whether the command handler is currently paused.
        /// </summary>
        protected bool Pausing { get; private set; }

        /// <summary>
        /// Indicates whether the current Server object has been disposed. Initial value is false.
        /// </summary>
        private bool Disposed = false;
        #endregion
    }
}