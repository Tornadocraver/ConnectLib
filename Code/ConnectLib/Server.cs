using ConnectLib.Data;
using ConnectLib.Exceptions;
using ConnectLib.Networking;
using ConnectLib.Types;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            SendConnectedClients(thisClient);
            while (Active)
            {
                try
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
                                        if (command.Options.Contains(CommandOption.Broadcast))
                                            ClientRemoved(thisClient);
                                        throw new ThreadInterruptedException();
                                    case CommandType.Custom:
                                        if (command.Options.Contains(CommandOption.Broadcast))
                                            BroadcastAsync(command);
                                        OnCustomCommand?.BeginInvoke(command, result => { try { OnCustomCommand.EndInvoke(result); }catch { } }, null);
                                        break;
                                    default:
                                        throw new CommandException($"The command type \"{command.Type}\" was not recognized in this context. Time: {DateTime.Now.ToString()}", command);
                                }
                            }
                        }
                    }
                    else
                        Thread.Sleep(0);
                }
                catch (ThreadInterruptedException) { break; }
                catch (Exception error) { Debug.WriteLine($"'{error.GetType()}' thrown in thread {Thread.CurrentThread.ManagedThreadId}. Message: {error.Message}"); }
            }
        }
        /// <summary>
        /// Listens for new connections to the Server.
        /// </summary>
        /// <param name="result">Contains the port to listen in the AsyncState object.</param>
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
                    while (Active)
                    {
                        try
                        {
                            if (listener.Poll(0, SelectMode.SelectRead) && !Pausing)
                            {
                                ClientObject client = new ClientObject();
                                client.Connections.Add("Main", new Connection(listener.Accept(), true, new Thread(() => HandleClient(client))));
                                if (Authenticate(client))
                                    client.Connections["Main"].StartHandler();
                                else
                                    client.Dispose();
                            }
                            else
                                Thread.Sleep(0);
                        }
                        catch (ThreadInterruptedException) { break; }
                        catch (Exception error) { Debug.WriteLine($"'{error.GetType()}' thrown in thread {Thread.CurrentThread.ManagedThreadId}. Message: {error.Message}"); }
                    }
                }
                catch (Exception error) { Debug.WriteLine($"'{error.GetType()}' thrown in thread {Thread.CurrentThread.ManagedThreadId}. Message: {error.Message}"); }
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
        #endregion

        #region Controls
        /// <summary>
        /// Broadcasts the specified command(s) to all connected hosts.
        /// </summary>
        /// <param name="commands">The command(s) to be broadcast.</param>
        public void Broadcast(params Command[] commands)
        {
            foreach (ClientObject client in Clients.Values)
                if (commands.FirstOrDefault(c => c.Sender.ID == client.Information.ID) != null)
                    Send(client, commands);
        }
        /// <summary>
        /// Broadcasts the specified command(s) to all connected hosts asynchronously.
        /// </summary>
        /// <param name="commands">The command(s) to be broadcast.</param>
        public async Task BroadcastAsync(params Command[] commands)
        {
            await Task.Run(() => Broadcast(commands));
        }
        /// <summary>
        /// Sends the specified command(s) to the specified host.
        /// </summary>
        /// <param name="target">The ClientInformation to send to.</param>
        /// <param name="commands">The command(s) to be sent.</param>
        public void Send(ClientObject client, params Command[] commands)
        {
            if (client != null && client.Connections.ContainsKey("Main") && client.Connections["Main"] != null)
                client.Connections["Main"].Write(Password, commands);
        }
        /// <summary>
        /// Sends the specified command(s) to the specified host asynchronously.
        /// </summary>
        /// <param name="target">The ClientInformation to send to.</param>
        /// <param name="commands">The command(s) to be sent.</param>
        public async Task SendAsync(ClientObject client, params Command[] commands)
        {
            await Task.Run(() => Send(client, commands));
        }
        /// <summary>
        /// Starts listening for connections on the specified port.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        public virtual void Start(int port)
        {
            if (Active)
                throw new InvalidOperationException("The server is already started.");
            if (!Tools.HasConnection(false))
                throw new NetworkException("You must be connected to a network in order to start the server.");
            if (port <= 0)
                throw new ArgumentException("The port must be greater than zero.");
            Active = true;
            ConnectionListener = new Thread(() => ListenForConnections(port));
            ConnectionListener.Start();
        }
        /// <summary>
        /// Starts asynchronously listening for connections on the specified port.
        /// </summary>
        /// <param name="port">The port to listen on.</param>
        public virtual async Task StartAsync(int port)
        {
            await Task.Run(() => Start(port));
        }
        /// <summary>
        /// Stops listening and waits for all remote connections to close.
        /// </summary>
        public virtual void Stop()
        {
            if (!Active)
                throw new InvalidOperationException("The server has not been started yet.");
            Broadcast(new Command(null, Information, CommandType.Close));
            Active = false;
            foreach (ClientObject client in Clients.Values)
                client.Dispose();
            Clients.Clear();
            if (ConnectionListener.IsAlive)
            {
                ConnectionListener.Interrupt();
                ConnectionListener.Join();
            }
            while (Clients.Count > 0) { }
        }
        /// <summary>
        /// Asynchronously stops listening and waits for all remote connections to close.
        /// </summary>
        public virtual async Task StopAsync()
        {
            await Task.Run(() => Stop());
        }
        
        /// <summary>
        /// Pauses or unpauses the listening and connection handling Thread's used by the Server.
        /// </summary>
        /// <param name="pausing">Indicates whether to pause (true) or unpause (false).</param>
        protected void Pause(bool pausing)
        {
            Pausing = pausing;
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
            if (disposing && Active)
                Stop();
            Clients = null;
            ConnectionListener = null;
            Information = null;
            Password = null;
            //Pausing = false;
            Disposed = true;
        }
        #endregion

        #region Other Methods
        /// <summary>
        /// Determines whether the remote host is authorized to access the session.
        /// </summary>
        /// <param name="client">The ClientObject to authenticate.</param>
        private bool Authenticate(ClientObject client)
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
                    return true;
                }
                else
                    throw new JsonException();
            }
            catch (JsonException) { client.Connections["Main"].Write(new Command(null, client.Information, CommandType.Unauthorized)); return false; }
        }
        /// <summary>
        /// Called when a remote host connects to the session.
        /// </summary>
        /// <param name="newClient">The ClientObject representing the remote host.</param>
        private void ClientAdded(ClientObject newClient)
        {
            Clients.Add(newClient.Information.ID, newClient);
            BroadcastAsync(new Command(null, newClient.Information, CommandType.ClientAdded));
            OnClientConnected?.BeginInvoke(newClient.Information, result => { try { OnClientConnected.EndInvoke(result); } catch { } }, null);
        }
        /// <summary>
        /// Called when a remote host disconnects from the session.
        /// </summary>
        /// <param name="oldClient">The ClientObject representing the remote host.</param>
        private void ClientRemoved(ClientObject oldClient)
        {
            Clients.Remove(oldClient.Information.ID);
            BroadcastAsync(new Command(null, oldClient.Information, CommandType.ClientRemoved));
            OnClientDisconnected?.BeginInvoke(oldClient.Information, result => { try { OnClientDisconnected.EndInvoke(result); } catch { } }, null);
        }
        /// <summary>
        /// Sends the information of each connected client to the new client.
        /// </summary>
        /// <param name="client">The newly-connected ClientObject.</param>
        private void SendConnectedClients(ClientObject client)
        {
            foreach (ClientObject cli in Clients.Values)
                if (client.Information.ID != cli.Information.ID)
                    client.Connections["Main"].Write(Password, new Command(client.Information, cli.Information, CommandType.ClientAdded));
        }
        #endregion

        #region Variables
        #region Actions
        /// <summary>
        /// Triggered when a client disconnects from the Server.
        /// </summary>
        public Action<ClientInformation> OnClientDisconnected { get; set; }
        /// <summary>
        /// Triggered when a command is received that is not internally implemented by the Server.
        /// </summary>
        public Action<Command> OnCustomCommand { get; set; }
        /// <summary>
        /// Triggered when a new client connects to the Server.
        /// </summary>
        public Action<ClientInformation> OnClientConnected { get; set; }
        #endregion

        /// <summary>
        /// Inidicates whether the Server is active. Initial value is false.
        /// </summary>
        public bool Active { get; private set; } = false;
        /// <summary>
        /// A collection of ClientObject objects representing the Clients connected to the Server.
        /// </summary>
        public Dictionary<string, ClientObject> Clients { get; set; } = new Dictionary<string, ClientObject>();
        /// <summary>
        /// Indicates the number of remote hosts connected to the Server.
        /// </summary>
        public int Count { get { return (Clients != null) ? Clients.Count : 0; } }
        /// <summary>
        /// A ClientInformation object containing the Server's information.
        /// </summary>
        public ClientInformation Information { get; private set; }

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
        private bool Disposed { get; set; } = false;
        #endregion
    }
}