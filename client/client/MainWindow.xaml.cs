using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace client
{
    public delegate void UpdateCallback(string message);

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }


        private void ConnectClick(object sender, EventArgs e)
        {
            IPHostEntry iph = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress serverAddress = iph.AddressList[1];
            int server_Port = 1337;
            
            Connection.TryToConnect(serverAddress, server_Port);

            Connection.NewDataReceived += Foo_Changed;

            data_outp.Items.Add("Connection Succesfull");
        }

        private void SendClick(object sender, EventArgs e)
        {
            double tSpan;   double.TryParse(tSpan_input.Text, out tSpan);
            int nPoints;    int.TryParse(nPoints_input.Text, out nPoints);

            string DataToSend = "PLS GIMME THIS tSpan=_" + tSpan.ToString() + "_  nPoints=_" + nPoints.ToString();
            Connection.SendRequest(DataToSend);
        }


        private void Update(string message)
        {
            data_outp.Items.Add(message);
        }

        public void Foo_Changed(object sender, MyEventArgs args)  // the Handler (reacts)
        {
            data_outp.Dispatcher.Invoke(new UpdateCallback(Update), new object[] { args.Message });
        }

    }



    static class Connection
    {
        public static Socket _connectingSocket { get; private set; }
        public static IPAddress ipAddress      { get; private set; }
        public static int Port                 { get; private set; }
        public static string ReceivedData      { get; private set; }
        private static byte[] _buffer;
                
        public static event EventHandler<MyEventArgs> NewDataReceived;
        
        // Trying connecting to selected server
        public static void TryToConnect(IPAddress ServerIp, int ServerPort)
        {
            ipAddress = ServerIp;
            Port = ServerPort;
            _connectingSocket = new Socket(ServerIp.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            while (!_connectingSocket.Connected)
            {
                Thread.Sleep(100);

                try
                {
                    _connectingSocket.Connect(new IPEndPoint(ipAddress, Port));
                    StartReceiving();
                }
                catch (Exception ex)
                {
                    throw new Exception("Connection error" + ex);
                }
            }
        }
        

        // Start waiting for message from client
        public static void StartReceiving()
        {
            try
            {
                _buffer = new byte[4];
                _connectingSocket.BeginReceive(_buffer, 0, _buffer.Length, SocketFlags.None, ReceiveCallback, null);
            }
            catch (Exception ex)
            { 
                throw new Exception("Receiving start error" + ex);
            }
        }


        // Receiving message
        private static void ReceiveCallback(IAsyncResult AR)
        {
            try
            {
                if (_connectingSocket.EndReceive(AR) > 1)
                {
                    // First 4 bytes store the size of incoming messages - read them
                    int MessageLength = BitConverter.ToInt32(_buffer, 0);

                    // Knowing the full size of incoming message - prepare for receiving
                    _buffer = new byte[MessageLength];

                    // Receive
                    _connectingSocket.Receive(_buffer, _buffer.Length, SocketFlags.None);
                    
                    // Handle
                    ReceivedData = Encoding.Unicode.GetString(_buffer, 0, MessageLength);
                    if (ReceivedData.Length != 0)
                        NewDataReceived?.Invoke(null, new MyEventArgs(null, ReceivedData));

                    // Resume waiting for new message 
                    StartReceiving();
                }
                else
                {
                    // Received nothing
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Data receive error" + ex);
            }
        }


        // Send message to server
        public static void SendRequest(string DataToSend)
        {
            try
            {
                byte[] DataPart = Encoding.Unicode.GetBytes(DataToSend);

                int SendMsgLength = DataPart.Length;
                byte[] InfoPart = BitConverter.GetBytes(SendMsgLength);
                

                var fullPacket = new List<byte>();
                fullPacket.AddRange(InfoPart);
                fullPacket.AddRange(DataPart);
                
                _connectingSocket.Send(fullPacket.ToArray());

                Console.WriteLine("Sending request: " + DataToSend);
                Console.WriteLine("Infobytes length=" + InfoPart.Length + " bytes ; Total message length=" + SendMsgLength.ToString() + " bytes;");
            }
            catch (Exception ex)
            {
                throw new Exception("Data send error" + ex);
            }
        }
    }



    // My event to pass received message
    public class MyEventArgs : EventArgs
    {
        public MyEventArgs(Exception ex, string msg)
        {
            Error = ex;
            Message = msg;
        }

        public Exception Error { get; }

        public string Message { get; }
    }


}
