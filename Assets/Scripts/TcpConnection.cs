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

public class TcpConnection
{
    public delegate void TcpMessage(byte[] data);
    public event TcpMessage TcpReceiveEvent;
    public event TcpMessage TcpSendCompleteEvent;

    private string port;

    public byte[] anchorSend = Encoding.ASCII.GetBytes("Empty Message");
    //public Dictionary<string, byte[]> receivedMessages;
    public byte[] anchorReceive;

#if !UNITY_EDITOR
    StreamSocket socket;
    StreamSocketListener listener;
#endif

    public TcpConnection(string port) //for recieving
    {
        //receivedMessages = new Dictionary<string, byte[]>();

        TcpReceiveEvent += filler;
        StartServer(port);
    }

    public TcpConnection()  //for sending
    {

    }

    public void filler(byte[] data) { }

    private async void StartServer(string _port)
    {
#if !UNITY_EDITOR
        try
        {
            port = _port;
            listener = new StreamSocketListener();
            //listener.ConnectionReceived += ServeAnchor;
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
    public async void ListenForAnchor(StreamSocketListener anchor, StreamSocketListenerConnectionReceivedEventArgs args)
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


    //public async void SendAnchor(string address)
    public async void SendAnchor(string address, string port, byte[] dataSend)
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
                    dw.DetachStream();
                    TcpSendCompleteEvent(BitConverter.GetBytes((UInt32)dataSend.Length));
                }
            }
        }
        catch(Exception e)
        {
            Windows.Networking.Sockets.SocketErrorStatus webErrorStatus = 
                Windows.Networking.Sockets.SocketError.GetStatus(e.GetBaseException().HResult);
        }
#endif
    }

#if !UNITY_EDITOR
    public async void ServeAnchor(StreamSocketListener anchor, StreamSocketListenerConnectionReceivedEventArgs args)
    {
        try
        {
            using (var dr = new DataReader(args.Socket.InputStream))
            {
                //read anchor message length
                byte[] length_bytes = new byte[4];
                await dr.LoadAsync((uint)4);
                dr.ReadBytes(length_bytes);

                //read anchor message ("Request Message")
                var message_size = BitConverter.ToInt32(length_bytes, 0);
                byte[] data_bytes = new byte[message_size];
                await dr.LoadAsync((uint)message_size);
                dr.ReadBytes(data_bytes);
            }

            using (var dw = new DataWriter(args.Socket.OutputStream))
            {
                //write anchor
                dw.WriteBytes(Combine(BitConverter.GetBytes(anchorSend.Length), anchorSend));
                await dw.StoreAsync();
                dw.DetachStream();
            }
        }
        catch(Exception e)
        {
            Windows.Networking.Sockets.SocketErrorStatus webErrorStatus = 
                Windows.Networking.Sockets.SocketError.GetStatus(e.GetBaseException().HResult);
        }
    }
#endif

    public async void RequestAnchor(string address)
    {
#if !UNITY_EDITOR
        try
        {
            DateTime startRequest = DateTime.Now;

            using (var streamSocket = new Windows.Networking.Sockets.StreamSocket())
            {
                var hostName = new Windows.Networking.HostName(address);
                await streamSocket.ConnectAsync(hostName, port);

                //write anchor message
                using (var dw = new DataWriter(streamSocket.OutputStream))
                {
                    var message = Encoding.ASCII.GetBytes("RequestAnchor");
                    dw.WriteBytes(Combine(BitConverter.GetBytes(message.Length), message));
                    await dw.StoreAsync();
                    dw.DetachStream();
                }

                //listen for incoming anchor
                using (var dr = new DataReader(streamSocket.InputStream))
                {
                    byte[] length_bytes = new byte[4];
                    await dr.LoadAsync((uint)4);
                    dr.ReadBytes(length_bytes);

                    var message_size = BitConverter.ToInt32(length_bytes, 0);
                    anchorReceive = new byte[message_size];
                    await dr.LoadAsync((uint)message_size);
                    dr.ReadBytes(anchorReceive);

                    TcpReceiveEvent(anchorReceive);
                }
            }

            DateTime endRequest = DateTime.Now;
            TimeSpan span = endRequest.Subtract(startRequest);
            DebugWindow.DebugMessage("Request Took " + span.TotalSeconds);
        }
        catch(Exception e)
        {
            Windows.Networking.Sockets.SocketErrorStatus webErrorStatus = 
                    Windows.Networking.Sockets.SocketError.GetStatus(e.GetBaseException().HResult);
            DebugWindow.DebugMessage("Networking Error: " + webErrorStatus);
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