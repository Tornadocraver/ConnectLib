using ConnectLib.Networking;

using System;
using System.Collections.Generic;

namespace ConnectLib.Data
{
    public class ClientObject : IDisposable
    {
        #region Constructors
        /// <summary>
        /// Creates a new ClientObject.
        /// </summary>
        public ClientObject()
        {

        }
        /// <summary>
        /// Creates a new ClientObject with the specified ClientInformation object.
        /// </summary>
        /// <param name="information">The information of the current computer.</param>
        public ClientObject(ClientInformation information)
        {
            Information = information;
        }
        /// <summary>
        /// A finalizer for the ClientObject object.
        /// </summary>
        ~ClientObject()
        {
            Dispose(false);
        }
        #endregion

        #region Disposing
        /// <summary>
        /// Releases all resources used by the ClientObject.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all resources used by the ClientObject.
        /// </summary>
        /// <param name="disposing">Indicates whether the Dispose method is being called by the Finalizer (false) or other code (true).</param>
        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
                return;
            if (disposing)
            {
                foreach (Connection connection in Connections.Values)
                    connection.Dispose();
                Connections.Clear();
                Connections = null;
                Information = null;
            }
            Disposed = true;
        }
        #endregion

        #region Variables
        /// <summary>
        /// A collection of Connection objects representing each connection that the ClientObject has.
        /// </summary>
        public Dictionary<string, Connection> Connections { get; private set; } = new Dictionary<string, Connection>();
        /// <summary>
        /// Information representing the current ClientObject.
        /// </summary>
        public ClientInformation Information { get; set; }

        /// <summary>
        /// Indicates whether the current ClientObject has been disposed. Initial value is "false".
        /// </summary>
        private bool Disposed = false;
        #endregion
    }
}