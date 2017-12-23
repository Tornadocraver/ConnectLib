using ConnectLib;
using ConnectLib.Exceptions;
using ConnectLib.Networking;

using ImageLib;

using System;
using System.Security;
using System.Threading;

namespace TestSTD
{
    class Program
    {
        static void Main(string[] args)
        {
            SecureString ss = new SecureString();
            foreach (char c in "Hello, World!")
                ss.AppendChar(c);
            Console.Write("Creating server ... ");
            Server server = new Server(ss);
            server.OnClientConnected += (c) => { Console.WriteLine($"{c.Name} connected"); };
            server.OnClientDisconnected += (c) => { Console.WriteLine($"{c.Name} disconnected"); };
            server.OnError += (e) => { Console.WriteLine(e); };
            server.Start(8023);
            Console.WriteLine("OK");

            Client client = new Client("Micah", true, false, ss);
            client.OnError += (e) => { Console.WriteLine(e); };
            connect:
            Console.Write("Creating client ... ");
            try { client.Connect(Tools.GetInternalIP(), 8023); }
            catch (ConnectException ex) { Console.WriteLine($"ERROR: {ex.Message}"); Thread.Sleep(1000); goto connect; }
            Console.Write("OK");

            //ImageServer server = new ImageServer(ss);
            //server.Start(8023);
            //server.OnImageReceived += (c, b) => { Test(c); };

            //ImageClient client = new ImageClient("Micah", true, ss);
            //client.OnConnected += () => { Console.WriteLine($"Connected at {DateTime.Now}"); };
            //client.OnImageReceived += Test;
            //client.Connect(ConnectLib.Networking.Tools.GetInternalIP(), 8023);

            //server.WriteImage(new byte[392]);
            //Console.ReadLine();
            //client.WriteImage(new byte[38912]);
            Console.ReadLine();
            client.Disconnect();
            Console.ReadLine();
            server.Stop();
            Console.ReadLine();
            client.Dispose();
            Console.ReadLine();
            server.Dispose();
            Console.ReadLine();

            System.IO.Stream s = new System.IO.MemoryStream();
            s.Dispose();
            string tmp = client.ClientInterface.Information.Name;
        }

        private static void Test(byte[] image)
        {
            string temp = string.Empty;
        }
    }
}
