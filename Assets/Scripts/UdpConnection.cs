using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System;

public class UdpConnection
{

/*    public static async Task<byte[]> RecieveUdpDataAsync(int port)
    {
        IPEndPoint localEP = new IPEndPoint(IPAddress.Any, port);
        UdpClient client = new UdpClient(localEP);
        client.Client.Blocking = false;

        DebugWindow.DebugMessage("Starting to listen on port " + port);
        UdpReceiveResult result =  await client.ReceiveAsync();
        DebugWindow.DebugMessage("Started Listener awaiting");

        client.Close();
        DebugWindow.DebugMessage("Closed Listener");
        return result.Buffer;
    }*/

/*    public static async Task SendMulticastUdpDataAsync(int port, byte[] data)
    {
        IPAddress multiAddress = IPAddress.Broadcast;

        await SendUdpDataAsync(multiAddress, port, data);
    }

    public static async Task SendUdpDataAsync(IPAddress ip, int port, byte[] data)
    {
        IPEndPoint sendEP = new IPEndPoint(ip, port);
        UdpClient client = new UdpClient(sendEP);
        client.MulticastLoopback = false;

        DebugWindow.DebugMessage("Sending data");
        await client.SendAsync(data, data.Length);

        client.Close();

    }*/

    public void SendMulticastUdpData(int port, byte[] data)
    {
        IPAddress multiAddress = IPAddress.Broadcast;
        SendUdpData(multiAddress, port, data);
    }

    public void SendUdpData(IPAddress ip, int port, byte[] data)
    {
        IPEndPoint sendEP = null;
        UdpClient client = null;

        try
        {
            sendEP = new IPEndPoint(ip, port);
            client = new UdpClient();

            client.EnableBroadcast = true;
            client.MulticastLoopback = false; //not working.  does it have to be save client?

            DebugWindow.DebugMessage("Sending data");
            client.Send(data, data.Length, sendEP);

            client.Close();
        }
        catch (Exception e)
        {
            DebugWindow.DebugMessage("send error " + e);
        }
    }

    public byte[] ReceiveUdpData(int port)
    {
        IPEndPoint anyEp = new IPEndPoint(IPAddress.Any, port);
        UdpClient client = new UdpClient(port);
        byte[] data = null;

        try
        {
            DebugWindow.DebugMessage("Listening");

            data = client.Receive(ref anyEp);

        }
        catch (Exception e)
        {
            DebugWindow.DebugMessage("Receive Data " + e.ToString());
        }
        finally
        {
            client.Close();
        }

        return data;
    }

/*    public void SendUdpData(IPAddress ip, int port, byte[] data)
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(ip, port);
        UdpClient sender = new UdpClient();

        try
        {
            sender.MulticastLoopback = false;
            sender.DontFragment = true;

            int sent = sender.Send(data, data.Length, remoteEndPoint);
        }
        catch (Exception e)
        {
            DebugWindow.DebugMessage("SendData " + e.ToString());
        }
        finally
        {
            sender.Close();
        }
    }*/

/*
    public void SendData(IPAddress ip, int port, byte[] data)
    {
        SendUdpData(ip, port, data);
    }

    public byte[] ReceiveData(int port)
    {
        return ReceiveUdpData(port);
    }*/

}