using ConnectLib;
using ConnectLib.Data;
using ConnectLib.Networking;
using ConnectLib.Types;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Security;

namespace ImageLib
{
    public class ImageClient : Client
    {
        #region Constructors
        public ImageClient(string name, bool internalConnection, SecureString password) : base(name, internalConnection, false, password)
        {
            OnCustomCommand += Custom;
        }
        #endregion

        #region Controls
        public override void Connect(IPAddress remote, int port)
        {
            base.Connect(remote, port);
            try
            {
                while (Clients.Count == 0)
                    Thread.Sleep(10);
                Pause(true);
                Dictionary<string, object> arguments = new Dictionary<string, object>();
                arguments.Add("Imaging", "Start");
                ClientInterface.Connections["Main"].Write(Password, new Command(null, ClientInterface.Information, CommandType.Custom) { Properties = arguments });
                Command response = ClientInterface.Connections["Main"].Read<Command>(Password);
                Socket receiving = new Socket(SocketType.Stream, ProtocolType.Tcp);
                Socket sending = new Socket(SocketType.Stream, ProtocolType.Tcp);
                receiving.Connect(remote, int.Parse(response.Properties["SendingPort"].ToString()));
                sending.Connect(remote, int.Parse(response.Properties["ReceivingPort"].ToString()));
                ClientInterface.Connections.Add("ImageReceiver", new Connection(receiving, true, new Thread(() => { WatchForImages(); })));
                ClientInterface.Connections.Add("ImageSender", new Connection(sending, true));
                ClientInterface.Connections["ImageReceiver"].StartHandler();
                ImageWriter = new BinaryWriter(ClientInterface.Connections["ImageSender"].Stream);
                Pause(false);
            }
            catch (Exception error) { RaiseOnError(error); Disconnect(); }
            OnConnected?.Invoke();
        }
        public override void Disconnect()
        {
            try
            {
                if (ClientInterface.Connections.ContainsKey("ImageReceiver") && ClientInterface.Connections.ContainsKey("ImageSender"))
                {
                    Dictionary<string, object> arguments = new Dictionary<string, object>();
                    arguments.Add("Imaging", "Stop");
                    ClientInterface.Connections["Main"].Write(Password, new Command(null, ClientInterface.Information, CommandType.Custom) { Properties = arguments });
                    ClientInterface.Connections["ImageReceiver"].Dispose();
                    ClientInterface.Connections["ImageSender"].Dispose();
                    ClientInterface.Connections.Remove("ImageReceiver");
                    ClientInterface.Connections.Remove("ImageSender");
                }
            }
            catch (Exception error) { RaiseOnError(error); }
            finally { ImageWriter?.Dispose(); base.Disconnect(); OnDisconnected?.Invoke(); }
        }
        #endregion

        #region Disposing
        public override void Dispose()
        {
            if (Active)
                Disconnect();
            ImageWriter = null;
            base.Dispose();
        }
        #endregion

        #region Event Handlers
        private void Custom(Command command)
        {
            if ((string)command.Properties["Imaging"] == "Stop")
                Disconnect();
        }
        private void WatchForImages()
        {
            try
            {
                using (BinaryReader reader = new BinaryReader(ClientInterface.Connections["ImageReceiver"].Stream))
                {
                    while (true)
                    {
                        try
                        {
                            if (!ClientInterface.Connections.ContainsKey("ImageReceiver"))
                                break;
                            if (ClientInterface.Connections["ImageReceiver"].DataAvailable)
                            {
                                int bytes = reader.ReadInt32();
                                OnImageReceived?.Invoke(reader.ReadBytes(bytes));
                            }
                            else
                                Thread.Sleep(10);
                        }
                        catch (ThreadInterruptedException) { break; }
                        catch (Exception error) { RaiseOnError(error); }
                    }
                }
            }
            catch (Exception error) { RaiseOnError(error); }
        }
        #endregion

        #region Events
        public delegate void ConnectedEventHandler();
        /// <summary>
        /// Triggered when the Client successfully connects to a remote session.
        /// </summary>
        public virtual event ConnectedEventHandler OnConnected;

        public delegate void DisconnectedEventHandler();
        /// <summary>
        /// Triggered when the current Client successfully disconnects from a remote session.
        /// </summary>
        public virtual event DisconnectedEventHandler OnDisconnected;

        public delegate void ImageReceivedEventHandler(byte[] imageData);
        /// <summary>
        /// Triggered when an image is received from the server
        /// </summary>
        public event ImageReceivedEventHandler OnImageReceived;
        #endregion

        #region Other Methods
        public void WriteImage(byte[] imageData)
        {
            try
            {
                ImageWriter?.Write(imageData.Length);
                ImageWriter?.Write(imageData);
            }
            catch (Exception error) { RaiseOnError(error); }
        }
        #endregion

        #region Variables
        private BinaryWriter ImageWriter { get; set; }
        #endregion
    }
}