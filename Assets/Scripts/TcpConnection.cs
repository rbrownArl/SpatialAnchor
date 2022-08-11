using System;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

#if !UNITY_EDITOR
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
#endif

using System.Linq;
using System.Net;
using System.Threading.Tasks;

public class TcpSend
{
    public delegate void TcpMessage(byte[] data);
    public event TcpMessage TcpSendCompleteEvent;

    private string port;


#if !UNITY_EDITOR
    StreamSocket socket;
    StreamSocketListener listener;
#endif

    public void filler(byte[] data) { }

    public TcpSend(string address, string port, byte[] dataSend)
    {
        TcpSendCompleteEvent += filler;

        SendData(address, port, dataSend);
        DebugWindow.DebugMessage("Sent data");
        //TcpSendCompleteEvent(BitConverter.GetBytes((UInt32)(dataSend[0])));
    }

    public TcpSend(string address, string port, byte[] dataSend, TcpMessage sendCompleteCallback)
    {
        TcpSendCompleteEvent += sendCompleteCallback;

        SendData(address, port, dataSend);
        DebugWindow.DebugMessage("Sent data");
        TcpSendCompleteEvent(BitConverter.GetBytes((UInt32)(dataSend[0])));
    }

    private async Task SendData(string address, string port, byte[] dataSend)
    {
#if !UNITY_EDITOR
        try
        {
            using (var streamSocket = new Windows.Networking.Sockets.StreamSocket())
            {
                var hostName = new Windows.Networking.HostName(address);
                await streamSocket.ConnectAsync(hostName, port);

                using (var dw = new DataWriter(streamSocket.OutputStream))
                {
                    dw.WriteBytes(Combine(BitConverter.GetBytes(dataSend.Length), dataSend));
                    await dw.StoreAsync();
                    await dw.FlushAsync();
                    dw.DetachStream();
                }
            }
        }
        catch (Exception e)
        {
            Windows.Networking.Sockets.SocketErrorStatus webErrorStatus =
                Windows.Networking.Sockets.SocketError.GetStatus(e.GetBaseException().HResult);
        }
#endif
    }

    private byte[] Combine(byte[] b1, byte[] b2)
    {
        byte[] b3 = new byte[b1.Length + b2.Length];

        b1.CopyTo(b3, 0);
        b2.CopyTo(b3, b1.Length);

        return b3;
    }
}

public class TcpListener
{
    public delegate void TcpMessage(byte[] data);
    public event TcpMessage TcpReceiveEvent;

    private string port;

#if !UNITY_EDITOR
    StreamSocket socket;
    StreamSocketListener listener;
#endif

    public TcpListener(string port) //for recieving
    {
        //receivedMessages = new Dictionary<string, byte[]>();

        TcpReceiveEvent += filler;
        StartServer(port);
    }

    private void filler(byte[] data) { }

    private async void StartServer(string _port)
    {
#if !UNITY_EDITOR
        try
        {
            port = _port;
            listener = new StreamSocketListener();

            listener.ConnectionReceived += ListenForAnchor;
            listener.Control.KeepAlive = false;
            await listener.BindServiceNameAsync(_port);
        }
        catch(Exception e)
        {
            DebugWindow.DebugMessage("Error: " + e.Message);
        }
#endif
    }

#if !UNITY_EDITOR
    private async void ListenForAnchor(StreamSocketListener anchor, StreamSocketListenerConnectionReceivedEventArgs args)
    {
        using (var dr = new DataReader(args.Socket.InputStream))
        {
            byte[] length_bytes = new byte[4];
            await dr.LoadAsync((uint)4);
            dr.ReadBytes(length_bytes);

            var message_size = BitConverter.ToInt32(length_bytes, 0);
            byte[] receivedBytes = new byte[message_size];
            await dr.LoadAsync((uint)message_size);
            dr.ReadBytes(receivedBytes);
            
            // receivedMessages(uuid, receivedBytes); Count on garbage collection?
            TcpReceiveEvent(receivedBytes);
        }
    }
#endif
}
