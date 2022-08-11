using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System;


public class UdpBroadcastData
{
    public UdpBroadcastData(int port, byte[] data)
    {
        IPAddress multiAddress = IPAddress.Broadcast;
        SendUdpData(multiAddress, port, data);
    }

    private void SendUdpData(IPAddress ip, int port, byte[] data)
    {
        IPEndPoint sendEP = null;
        UdpClient client = null;

        try
        {
            sendEP = new IPEndPoint(ip, port);
            client = new UdpClient();

            client.EnableBroadcast = true;
            client.MulticastLoopback = false; //not working.  does it have to be save client?

            //DebugWindow.DebugMessage("Sending UDP data");
            client.Send(data, data.Length, sendEP);

            client.Close();
        }
        catch (Exception e)
        {
            DebugWindow.DebugMessage("Error Sending UDP data " + e);
        }
    }
}

public class UdpListener
{
    public byte[] ReceiveUdpData(int port)
    {
        IPEndPoint anyEp = new IPEndPoint(IPAddress.Any, port);
        UdpClient client = new UdpClient(port);
        byte[] data = null;

        try
        {
            DebugWindow.DebugMessage("Listening Udp");

            data = client.Receive(ref anyEp);

        }
        catch (Exception e)
        {
            DebugWindow.DebugMessage("Error Listening UDP Data " + e.ToString());
        }
        finally
        {
            client.Close();
        }

        return data;
    }

}
