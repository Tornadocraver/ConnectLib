using ConnectLib.Cryptography;

using Newtonsoft.Json;

using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Text;
using System.Threading;

namespace ConnectLib.Networking
{
    public class Connection : IDisposable
    {
        #region Constructors
        /// <summary>
        /// Creates a new Connection object.
        /// </summary>
        /// <param name="connection">The Socket connected to a remote host.</param>
        /// <param name="ownsSocket">Indicates whether the subsequent streams should take ownership of the socket.</param>
        public Connection(Socket connection, bool ownsSocket)
        {
            if (connection == null)
                throw new ArgumentNullException();
            Stream = new NetworkStream(connection, ownsSocket);
            Reader = new BinaryReader(Stream);
            Writer = new BinaryWriter(Stream);
            Settings.Converters.Add(new IPAddressConverter());
            Settings.Formatting = Formatting.None;
            Settings.TypeNameHandling = TypeNameHandling.All;
            Settings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple;
        }
        /// <summary>
        /// Creates a new Connection object.
        /// </summary>
        /// <param name="connection">The Socket connected to a remote host.</param>
        /// <param name="ownsSocket">Indicates whether the subsequent streams should take ownership of the socket.</param>
        /// <param name="handler">A custom Thread to handle incomming transmissions.</param>
        public Connection(Socket connection, bool ownsSocket, Thread handler) : this(connection, ownsSocket)
        {
            CommandHandler = handler;
        }
        /// <summary>
        /// A finalizer for the Connection object
        /// </summary>
        ~Connection()
        {
            Dispose(false);
        }
        #endregion

        #region Disposing
        /// <summary>
        /// Releases all resources used by the Connection object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all resources used by the Connection object.
        /// </summary>
        /// <param name="disposing">Indicates whether the Dispose method is being called by the Finalizer (false) or other code (true).</param>
        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
                return;
            if (disposing)
            {
                Disconnect();
                Reader?.Dispose();
                Writer?.Dispose();
                Stream?.Dispose();
                Socket?.Dispose();
                CommandHandler = null;
                Reader = null;
                Settings = null;
                Socket = null;
                Stream = null;
                Writer = null;
            }
            Disposed = true;
        }
        #endregion

        #region Other Methods
        /// <summary>
        /// Disconnects the main Socket and all related connection objects.
        /// </summary>
        public void Disconnect()
        {
            StopHandler();
            Reader?.Close();
            Writer?.Close();
            Stream?.Close();
            Socket?.Close();
        }
        /// <summary>
        /// Starts the custom handler Thread.
        /// </summary>
        public void StartHandler()
        {
            CommandHandler.Start();
        }
        /// <summary>
        /// Stops the custom handler Thread.
        /// </summary>
        public void StopHandler()
        {
            if (CommandHandler != null && CommandHandler.ThreadState == ThreadState.Running)
            {
                CommandHandler.Interrupt();
                CommandHandler.Join();
            }
        }
        #endregion

        #region Read/Write
        /// <summary>
        /// Reads an object from the remote session.
        /// </summary>
        /// <typeparam name="T">The type of object to be parsed.</typeparam>
        /// <returns>An object of type T.</returns>
        public T Read<T>()
        {
            while (!Stream.DataAvailable)
                Thread.Sleep(10);
            int byteCount = Reader.ReadInt32();
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(Reader.ReadBytes(byteCount)), Settings);
        }
        /// <summary>
        /// Reads an object from the remote session that has been encrypted with the specified password.
        /// </summary>
        /// <typeparam name="T">The type of object to parse and return.</typeparam>
        /// <param name="password">The password used to encrypt the object.</param>
        /// <returns>An object of type T.</returns>
        public T Read<T>(SecureString password)
        {
            while (!Stream.DataAvailable)
                Thread.Sleep(10);
            int byteCount = Reader.ReadInt32();
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(Reader.ReadBytes(byteCount)).AESDecrypt(password), Settings);
        }

        /// <summary>
        /// Writes the specified objects to the remote session.
        /// </summary>
        /// <param name="objects">The object(s) to be written.</param>
        public void Write(params object[] objects)
        {
            foreach (object obj in objects)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj, Settings));
                Writer.Write(bytes.Length);
                Writer.Write(bytes);
                Writer.Flush();
            }
        }
        /// <summary>
        /// Encrypts and writes the specified objects to the remote session.
        /// </summary>
        /// <param name="password">The password to encrypt the objects with</param>
        /// <param name="objects">The object(s) to be encrypted and written.</param>
        public void Write(SecureString password, params object[] objects)
        {
            foreach (object obj in objects)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj, Settings).AESEncrypt(password));
                Writer.Write(bytes.Length);
                Writer.Write(bytes);
                Writer.Flush();
            }
        }
        #endregion

        #region Variables
        /// <summary>
        /// Indicates whether data is available from the remote host.
        /// </summary>
        public bool DataAvailable { get { return (Stream == null) ? false : Stream.DataAvailable; } }
        /// <summary>
        /// A Socket used to transmit/receive data to/from the remote host.
        /// </summary>
        public Socket Socket { get; set; }
        /// <summary>
        /// A NDNStream (Non-Disposable Network Stream) used to read/write data to/from the connected socket.
        /// </summary>
        public NetworkStream Stream { get; private set; }

        /// <summary>
        /// A Thread that listens for commands from the remote host. Must implement ThreadInterruptedException.
        /// </summary>
        protected Thread CommandHandler { get; set; }
        /// <summary>
        /// A BinaryReader used to read data from the NDNStream (Non-Disposable Network Stream).
        /// </summary>
        protected BinaryReader Reader { get; private set; }
        /// <summary>
        /// A BinaryWriter used to write data to the NDNStream (Non-Disposable Network Stream).
        /// </summary>
        protected BinaryWriter Writer { get; private set; }

        /// <summary>
        /// Indicates whether the current Connection object has been disposed. Initial value is false.
        /// </summary>
        private bool Disposed = false;
        /// <summary>
        /// The settings used to serialize/deserialize JSON.
        /// </summary>
        private JsonSerializerSettings Settings = new JsonSerializerSettings();
        #endregion
    }
}