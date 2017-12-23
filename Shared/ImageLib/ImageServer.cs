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
    public class ImageServer : Server
    {
        #region Constructors
        public ImageServer(SecureString password) : base(password)
        {
            OnCustomCommand += Custom;
        }
        #endregion

        #region Controls
        public override void Start(int port)
        {
            base.Start(port);
            OnStarted?.Invoke();
        }
        public override void Stop()
        {
            try
            {
                if (Clients.Count > 0)
                {
                    Dictionary<string, object> arguments = new Dictionary<string, object>();
                    arguments.Add("Imaging", "Stop");
                    foreach (ClientObject client in Clients.Values)
                        client.Connections["Main"].Write(Password, new Command(client.Information, Information, CommandType.Custom) { Properties = arguments });
                    while (Clients.Count > 0)
                        Thread.Sleep(10);
                }
            }
            catch (Exception error) { RaiseOnError(error); }
            finally { base.Stop(); OnStopped?.Invoke(); }
        }
        #endregion

        #region Disposing
        public override void Dispose()
        {
            if (Active)
                Stop();
            base.Dispose();
        }
        #endregion

        #region Events
        public delegate void StartedEventHandler();
        /// <summary>
        /// Triggered when the Server starts successfully.
        /// </summary>
        public event StartedEventHandler OnStarted;

        public delegate void StoppedEventHandler();
        /// <summary>
        /// Triggered when the Server stops successfully.
        /// </summary>
        public event StoppedEventHandler OnStopped;

        public delegate void ImageReceivedEventHandler(byte[] imageData, ClientInformation sender);
        /// <summary>
        /// Triggered when an image is received from a client
        /// </summary>
        public event ImageReceivedEventHandler OnImageReceived;
        #endregion

        #region Other Methods
        public void WriteImage(byte[] imageData)
        {
            try
            {
                if (Clients.Count > 0)
                {
                    foreach (ClientObject client in Clients.Values)
                    {
                        if (client.Connections.ContainsKey("ImageSender"))
                            using (BinaryWriter writer = new BinaryWriter(client.Connections["ImageSender"].Stream))
                            {
                                writer.Write(imageData.Length);
                                writer.Write(imageData);
                                writer.Flush();
                            }
                    }
                }
            }
            catch (Exception error) { RaiseOnError(error); }
        }

        private void Custom(Command command)
        {
            string imageCommand = (string)command.Properties["Imaging"];
            if (imageCommand == "Start")
            {
                Pause(true);
                Dictionary<string, object> arguments = new Dictionary<string, object>();
                arguments.Add("ReceivingPort", (Port + (6 * (Clients.Count - 1) + 1)));
                arguments.Add("SendingPort", (Port + (6 * (Clients.Count - 1) + 2)));
                Clients[command.Sender.ID].Connections["Main"].Write(Password, new Command(command.Sender, Information, CommandType.Custom) { Properties = arguments });
                using (Socket receiver = new Socket(SocketType.Stream, ProtocolType.Tcp))
                using (Socket sender = new Socket(SocketType.Stream, ProtocolType.Tcp))
                {
                    receiver.Bind(new IPEndPoint(Information.IP, (int)arguments["ReceivingPort"]));
                    sender.Bind(new IPEndPoint(Information.IP, (int)arguments["SendingPort"]));
                    receiver.Listen(1);
                    sender.Listen(1);
                    Clients[command.Sender.ID].Connections.Add("ImageReceiver", new Connection(receiver.Accept(), true, new Thread(() => { WatchForImages(command.Sender); })));
                    Clients[command.Sender.ID].Connections.Add("ImageSender", new Connection(sender.Accept(), true));
                    Clients[command.Sender.ID].Connections["ImageReceiver"].StartHandler();
                }
                Pause(false);
            }
            else if (imageCommand == "Stop")
            {
                Clients[command.Sender.ID].Connections["ImageReceiver"].Dispose();
                Clients[command.Sender.ID].Connections["ImageSender"].Dispose();
                Clients[command.Sender.ID].Connections.Remove("ImageReceiver");
                Clients[command.Sender.ID].Connections.Remove("ImageSender");
            }
        }
        private void WatchForImages(ClientInformation client)
        {
            try
            {
                using (BinaryReader reader = new BinaryReader(Clients[client.ID].Connections["ImageReceiver"].Stream))
                {
                    while (true)
                    {
                        try
                        {
                            int bytes = reader.ReadInt32();
                            OnImageReceived?.Invoke(reader.ReadBytes(bytes), client);
                        }
                        catch (ThreadInterruptedException) { break; }
                        catch (Exception error) { RaiseOnError(error); }
                    }
                }
            }
            catch (Exception error) { RaiseOnError(error); }
        }
        #endregion
    }
}