using ConnectLib;
using ConnectLib.Data;
using ConnectLib.Networking;
using ConnectLib.Types;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Security;

namespace ImageLib
{
    public class ImageClient : Client
    {
        #region Constructors
        public ImageClient(string name, bool internalConnection, SecureString password) : base(name, internalConnection, password)
        {
            OnCustomCommand += Custom;
        }
        #endregion

        #region Controls
        public override void Connect(IPAddress remote, int port)
        {
            try
            {
                base.Connect(remote, port);
                while (Clients.Count == 0) { }
                Pause(true);
                Dictionary<string, object> arguments = new Dictionary<string, object>();
                arguments.Add("Imaging", "Start");
                ClientInterface.Connections["Main"].Write(Password, new Command(null, ClientInterface.Information, CommandType.Custom) { Properties = arguments });
                readCommand:
                Command response = ClientInterface.Connections["Main"].Read<Command>(Password);
                if (response == null || response.Properties == null)
                    goto readCommand;
                Socket receiving = new Socket(SocketType.Stream, ProtocolType.Tcp);
                Socket sending = new Socket(SocketType.Stream, ProtocolType.Tcp);
                receiving.Connect(remote, int.Parse(response.Properties["SendingPort"].ToString()));
                sending.Connect(remote, int.Parse(response.Properties["ReceivingPort"].ToString()));
                ClientInterface.Connections.Add("ImageReceiver", new Connection(receiving, true, new Thread(() => { WatchForImages(); })));
                ClientInterface.Connections.Add("ImageSender", new Connection(sending, true));
                ClientInterface.Connections["ImageReceiver"].StartHandler();
                Pause(false);
                OnConnected?.BeginInvoke(result => { try { OnConnected.EndInvoke(result); } catch { } }, null);
            }
            catch (Exception error) { if (Connected) { base.Disconnect(); } throw error; }
        }
        public override async Task ConnectAsync(IPAddress remote, int port)
        {
            await Task.Run(() => Connect(remote, port));
        }
        public override void Disconnect()
        {
            try
            {
                base.Disconnect();
                OnDisconnected?.BeginInvoke(result => { try { OnDisconnected.EndInvoke(result); } catch { } }, null);
            }
            catch (Exception error) { throw error; }
        }
        public override async Task DisconnectAsync()
        {
            await Task.Run(() => Disconnect());
        }
        #endregion

        #region Disposing
        public override void Dispose()
        {
            if (Active)
                Disconnect();
            base.Dispose();
        }
        #endregion

        #region Event Handlers
        private void Custom(Command command)
        {
            if (command.Properties.ContainsKey("Imaging") && (string)command.Properties["Imaging"] == "Stop")
                Disconnect();
        }
        private void WatchForImages()
        {
            try
            {
                while (true)
                {
                    if (ClientInterface.Connections["ImageReceiver"].DataAvailable)
                        OnImageReceived?.BeginInvoke(ClientInterface.Connections["ImageReceiver"].Read<byte[]>(Password), result => { try { OnImageReceived.EndInvoke(result); } catch { } }, null);
                    else
                        Thread.Sleep(0);
                }
            }
            catch (ThreadInterruptedException) { }
        }
        #endregion

        #region Other Methods
        public void WriteImage(byte[] imageData)
        {
            ClientInterface.Connections["ImageSender"].Write(Password, imageData);
        }
        public async Task WriteImageAsync(byte[] imageData)
        {
            await Task.Run(() => WriteImage(imageData));
        }
        #endregion

        #region Variables
        #region Actions
        /// <summary>
        /// Triggered when the Client successfully connects to a remote session.
        /// </summary>
        public Action OnConnected { get; set; }
        /// <summary>
        /// Triggered when the current Client successfully disconnects from a remote session.
        /// </summary>
        public Action OnDisconnected { get; set; }
        /// <summary>
        /// Triggered when an image is received from the server
        /// </summary>
        public Action<byte[]> OnImageReceived { get; set; }
        #endregion
        #endregion
    }
}