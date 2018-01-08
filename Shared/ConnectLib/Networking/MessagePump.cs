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

namespace ConnectLib.Networking
{
    public class MessagePump : IDisposable
    {
        private bool Disposed { get; set; } = false;





        public static void Subscribe<T>()
        {

        }

        public void Send<T>()
        {

        }

        #region IDisposable Support
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
                return;
            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
            }
            Disposed = true;
        }
        #endregion
    }
}