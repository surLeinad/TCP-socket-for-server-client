using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace server
{
    class Program
    {
        static void Main(string[] args)
        {
            IPHostEntry iph = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress serverAddress = iph.AddressList[1];
            int server_Port = 1337;
            int maxConnections = 10;

            Listener listener = new Listener(serverAddress, server_Port);   // Setup server
            listener.StartListening(maxConnections);                        // Start server

            Console.Read();
        }
    }


    // Here we accept new connections
    class Listener
    {
        //This is the socket that will listen to any incoming connections
        public Socket _serverSocket { get; private set; }
        public int Port             { get; private set; }
        public int maxConnections   { get; private set; }
        public IPAddress ipAddress  { get; private set; }

        public Listener(IPAddress ServerIp, int ServerPort)
        {
            ipAddress = ServerIp;
            Port = ServerPort;
            
            _serverSocket = new Socket(ServerIp.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }


        // Here we start waiting for new client
        public void StartListening(int MaxConnections)
        {
            maxConnections = MaxConnections;
            try
            {
                Console.WriteLine("Server started at IP:" + ipAddress.ToString() + "; port:" + Port.ToString() + ";\n");

                _serverSocket.Bind(new IPEndPoint(ipAddress, Port));                            // Setup server at selected endpoint
                _serverSocket.Listen(MaxConnections);                                           // Limit maximum number of clients
                _serverSocket.BeginAccept(new AsyncCallback(AcceptCallback), _serverSocket);    // Actual waiting
            }
            catch (Exception ex)
            {
                throw new Exception("Server starting error" + ex);
            }
        }

        // Here we go after receiving connection request
        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket temp = (Socket)ar.AsyncState;                            // ??
                Socket acceptedSocket = temp.EndAccept(ar);                     // Get socket of new client 
                ClientController.AddNewClient(acceptedSocket);                  // Handle new client

                IPEndPoint REP = (IPEndPoint)acceptedSocket.RemoteEndPoint;
                Console.WriteLine("Received request from IP:" + REP.Address.ToString() + "; port:" + REP.Port.ToString() + ";");
                
                Console.WriteLine(ClientController.AllClients.Count() + " clients connected now");
                Console.WriteLine();


                // Resume waiting for new clients
                _serverSocket.BeginAccept(new AsyncCallback(AcceptCallback), _serverSocket);
            }
            catch (Exception ex)
            {
                throw new Exception("Server listening error" + ex);
            }
        }
    }


    // Class for receiving messages from client
    public class ClientReceiver
    {
        private byte[] _buffer;
        private Socket _receiveSocket;
        private int _clientId;

        public ClientReceiver(Socket receiveSocket, int Id)
        {
            _receiveSocket = receiveSocket;
            _clientId = Id;
        }


        // Start waiting for message from client
        public void StartReceiving()
        {
            try
            {
                _buffer = new byte[4];
                _receiveSocket.BeginReceive(_buffer, 0, _buffer.Length, 0, ReceiveCallback, null);
            }
            catch (Exception ex)
            {
                throw new Exception("Receiving start error" + ex);
            }
        }

        // Receiving message
        private void ReceiveCallback(IAsyncResult AR)
        {
            try
            {
                if (_receiveSocket.EndReceive(AR) > 1)
                {
                    // First 4 bytes store the size of incoming messages - read them
                    int MessageLength = BitConverter.ToInt32(_buffer, 0);

                    // Knowing the full size of incoming message - prepare for receiving
                    _buffer = new byte[MessageLength];

                    // Receive
                    _receiveSocket.Receive(_buffer, MessageLength, SocketFlags.None);
                    string data = Encoding.Unicode.GetString(_buffer);
                    
                    Console.WriteLine("User " + _clientId.ToString() + " sent following request: " + data);

                    // Send received message for handling
                    ClientController.AddClientRequest(_clientId, data);

                    // Resume waiting for new message 
                    StartReceiving();
                }

                // if we didn't receive anything - disconnect client
                else
                {
                    Disconnect();
                }
            }
            catch
            {
                if (!_receiveSocket.Connected)
                {
                    Disconnect();
                }
                else
                {
                    Console.WriteLine("Data receive error");
                    StartReceiving();
                }
            }
        }

        // Disconnecting client
        private void Disconnect()
        {
            // Close connection
            _receiveSocket.Disconnect(true);
            ClientController.RemoveClient(_clientId);
        }
    }


    // Class, used to send messages back to selected client
    class ClientSender
    {
        private Socket _senderSocket;
        private int _clientId;
        
        public ClientSender(Socket receiveSocket, int Id)
        {
            _senderSocket = receiveSocket;
            _clientId = Id;
        }
        
        // Sending message to client
        public void AnswerRequest(string data)
        {
            try
            {
                byte[] DataPart = Encoding.Unicode.GetBytes(data);

                int SendMsgLength = DataPart.Length;
                byte[] InfoPart = BitConverter.GetBytes(SendMsgLength);

                var fullPacket = new List<byte>();
                fullPacket.AddRange(InfoPart);
                fullPacket.AddRange(DataPart);

                _senderSocket.Send(fullPacket.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception("Data sending error" + ex);
            }
        }

        // Disconnecting client
        private void Disconnect()
        {
            // Close connection
            _senderSocket.Disconnect(true);
            ClientController.RemoveClient(_clientId);
        }
    }


    // Client class
    class Client
    {
        public int Id                   { get; private set; }
        public Socket _clientSocket     { get; private set; }
        public ClientSender Sender      { get; private set; }
        public ClientReceiver Receive   { get; private set; }

        public Client(Socket socket, int id)
        {
            Sender = new ClientSender(socket, id);

            Receive = new ClientReceiver(socket, id);
            Receive.StartReceiving();

            _clientSocket = socket;
            Id = id;
        }


        // Handling client's request
        public void HandleRequest(string request)
        {
            string[] cmd = request.Split('_');

            // Here as an example I return points on sine wave based on user's request
            double tSpan;   double.TryParse(cmd[1], out tSpan);
            int nPoints;    int.TryParse(cmd[3], out nPoints);
            
            double tStep = tSpan / nPoints;
            
            for (int i = 0; i < nPoints; i++)
            {
                double ti = 0 + i * tStep;
                double val = 10 * Math.Sin(2 * Math.PI * ti);
                string DataToSend = "Точка (_" + ti.ToString() + "_,_" + val.ToString() + "_)";

                Sender.AnswerRequest(DataToSend);

                Thread.Sleep((int)(1000.0 * tStep));
            }
        }
    }



    // Class, which controlls all connected clients
    static class ClientController
    {
        // All connected clients in a list
        public static List<Client> AllClients = new List<Client>();


        // Handling new client (accepting/denying connection)
        public static void AddNewClient(Socket socket)
        {
            Client newClient = new Client(socket, AllClients.Count);
            AllClients.Add(newClient);
        }


        // Removing client
        public static void RemoveClient(int id)
        {
            int TargetClientIndex = AllClients.FindIndex(x => x.Id == id);
            AllClients.RemoveAt(TargetClientIndex);
        }

        
        // Serving client request (accepting/denying it)
        public static void AddClientRequest(int id, string data)
        {
            int TargetClientIndex = AllClients.FindIndex(x => x.Id == id);
            AllClients.ElementAt(TargetClientIndex).HandleRequest(data);
        }
    }
}
