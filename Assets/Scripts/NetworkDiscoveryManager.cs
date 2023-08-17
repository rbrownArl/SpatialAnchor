using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

class NetworkDiscoveryManager : MonoBehaviour
{
    private int ipPort = 9998;
    private string machineName;
    private string machineIp = null;

    private Thread readIpThread;

    private UdpListener udpListener = null;
    private HashSet<string> ips;

    public float iPBroadcastRate = 30.0f;


    public enum MessageType : UInt32
    {
        SendIp = 0x4321,
        SendPos = 0x7EE7,
        SendAnchor = 0xBEEF,
        DoneAnchor = 0xDEAD,
        CancelAnchor = 0x0000
    }

    private void Start()
    {
        machineName = SystemInfo.deviceName;
        machineIp = Utility.getMachineIp(machineName);

        udpListener = new UdpListener();
        ips = new HashSet<string>();
        readIpThread = new Thread(new ThreadStart(UdpListener));
        readIpThread.Start();

        StartCoroutine("IpBroadcast");
    }

    private IEnumerator IpBroadcast()
    {
        if (machineIp == null)
        {
            DebugWindow.DebugMessage("No IP");
            yield return null;
        }

        yield return new WaitForSeconds(2.0f);
        DebugWindow.DebugMessage("IpBroadcast");

        byte[] bytes = Utility.MessageTypeToBytes((UInt32)MessageType.SendIp);
        Utility.AppendBytes(ref bytes, Encoding.UTF8.GetBytes(machineIp));

        //udpBroadcast.SendMulticastUdpData(ipPort, Encoding.UTF8.GetBytes(machineIp));
        new UdpBroadcastData(ipPort, bytes);

        yield return new WaitForSeconds(58.0f);
        StartCoroutine("IpBroadcast");
    }

    public void BroadcastPosOnce()
    {
        Vector3 pos = new Vector3(1, 2, 3);

        DebugWindow.DebugMessage("Send Pos");
        byte[] bytes = Utility.MessageTypeToBytes((UInt32)MessageType.SendPos);
        Utility.AppendBytes(ref bytes, Utility.Vector3ToBytes(pos));

        new UdpBroadcastData(ipPort, bytes);
    }

    public void UpdatePos(MessageType messageType, byte[] posBytes)
    {
        Vector3 pos = Utility.BytesToVector3(posBytes);

        DebugWindow.DebugMessage(messageType.ToString() + ": " + pos.ToString());
    }

    private void UpdateIps(MessageType messageType, byte[] ipBytes)
    {
        string ipString = Encoding.UTF8.GetString(ipBytes);

        DebugWindow.DebugMessage(messageType.ToString() + ": " + ipString);

        IPAddress.Parse(ipString);

        if (ipString != machineIp)
        {
            if (!ips.Contains(ipString))
            {
                ips.Add(ipString);
                DebugWindow.DebugMessage("Known IPs");
                foreach (string ip in ips)
                {
                    DebugWindow.DebugMessage("  " + ip);
                }
                IpBroadcast();
            }
        }
    }

    private void UdpListener()
    {
        byte[] receivedBytes = null;
        byte[] receivedMessage = null;

        while (true)
        {
            try
            {
                receivedBytes = udpListener.ReceiveUdpData(ipPort);

                MessageType messageType = (MessageType)BitConverter.ToUInt32(receivedBytes, 0);

                receivedMessage = receivedBytes.Skip(4).ToArray();

                switch (messageType)
                {
                    case MessageType.SendIp:
                        UpdateIps(messageType, receivedMessage);
                        break;
                    case MessageType.SendPos:
                        UpdatePos(messageType, receivedMessage);
                        break;
                }
            }
            catch (Exception e)
            {
                DebugWindow.DebugMessage("UdpListener: " + e);
            }
        }
    }

    public string[] GetIps()
    {
        return ips.ToArray();
    }
}

