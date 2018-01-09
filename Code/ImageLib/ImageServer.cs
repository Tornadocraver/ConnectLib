using ConnectLib;
using ConnectLib.Data;
using ConnectLib.Networking;
using ConnectLib.Types;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Security;
using System.Threading.Tasks;

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
            Port = port;
            try { base.Start(port); }
            catch (Exception error) { throw error; }
            OnStarted?.BeginInvoke(result => { try { OnStarted.EndInvoke(result); } catch { } }, null);
        }
        public override async Task StartAsync(int port)
        {
            await Task.Run(() => Start(port));
        }
        public override void Stop()
        {
            if (Clients.Count > 0)
            {
                Dictionary<string, object> arguments = new Dictionary<string, object>();
                arguments.Add("Imaging", "Stop");
                foreach (ClientObject client in Clients.Values)
                    client.Connections["Main"].Write(Password, new Command(client.Information, Information, CommandType.Custom) { Properties = arguments });
            }
            try { base.Stop(); }
            catch (Exception error) { throw error; }
            OnStopped?.BeginInvoke(result => { try { OnStopped.EndInvoke(result); } catch { } }, null);
            Port = -1;
        }
        public override async Task StopAsync()
        {
            await Task.Run(() => Stop());
        }
        #endregion

        #region Disposing
        protected override void Dispose(bool disposing)
        {
            if (disposing && Active)
                Stop();
            base.Dispose(disposing);
        }
        #endregion

        #region Other Methods
        public void WriteImage(byte[] imageData)
        {
            if (Clients.Count > 0)
            {
                foreach (ClientObject client in Clients.Values)
                {
                    try
                    {
                        if (client.Connections.ContainsKey("ImageSender"))
                            client.Connections["ImageSender"].Write(Password, imageData);
                    }
                    catch { }
                }
            }
        }
        public async Task WriteImageAsync(byte[] imageData)
        {
            await Task.Run(() => WriteImage(imageData));
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
        }
        private void WatchForImages(ClientInformation client)
        {
            try
            {
                while (true)
                {
                    if (Clients[client.ID].Connections["ImageReceiver"].DataAvailable)
                        OnImageReceived?.BeginInvoke(Clients[client.ID].Connections["ImageReceiver"].Read<byte[]>(Password), client, result => { try { OnImageReceived.EndInvoke(result); } catch { } }, null);
                    else
                        Thread.Sleep(0);
                }
            }
            catch (ThreadInterruptedException) { }
            catch (KeyNotFoundException) { }
        }
        #endregion

        #region Variables
        #region Actions
        /// <summary>
        /// Triggered when an image is received from a client
        /// </summary>
        public Action<byte[], ClientInformation> OnImageReceived { get; set; }
        /// <summary>
        /// Triggered when the Server starts successfully.
        /// </summary>
        public Action OnStarted { get; set; }
        /// <summary>
        /// Triggered when the Server stops successfully.
        /// </summary>
        public Action OnStopped { get; set; }
        #endregion

        /// <summary>
        /// The port that the Server is listening on.
        /// </summary>
        private int Port { get; set; } = -1;
        #endregion
    }
}