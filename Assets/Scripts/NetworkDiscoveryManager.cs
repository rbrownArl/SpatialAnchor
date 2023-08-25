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
    private HashSet<string> ips = new HashSet<string>();

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

        DebugWindow.DebugMessage("IpBroadcast");

        byte[] bytes = Utility.MessageTypeToBytes((UInt32)MessageType.SendIp);
        Utility.AppendBytes(ref bytes, Encoding.UTF8.GetBytes(machineIp));

        new UdpBroadcastData(ipPort, bytes);

        yield return new WaitForSeconds(iPBroadcastRate);
        StartCoroutine("IpBroadcast");
    }

    //Read new known Ips
    private void UpdateIps(MessageType messageType, byte[] ipBytes)
    {
        string ipString = Encoding.UTF8.GetString(ipBytes);

        DebugWindow.DebugMessage(messageType.ToString() + ": " + ipString);

        IPAddress.Parse(ipString);

        if (ipString != machineIp)
        {
            // encountered a new IP
            //   print the updated known ips
            //   immediately, broadcast our address so the new guy gets it
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

    public void BroadcastPosOnce(GameObject gameObject)
    {
        byte[] bytes = Utility.MessageTypeToBytes((UInt32)MessageType.SendPos);
        Utility.AppendBytes(ref bytes, Utility.Vector3ToBytes(gameObject.transform.position));
        Utility.AppendBytes(ref bytes, Utility.StringToBytes(gameObject.name));

        DebugWindow.DebugMessage("Sending " + gameObject.name + ": " + gameObject.transform.position);
        new UdpBroadcastData(ipPort, bytes);
    }

    //Read new positions for object
    public void UpdatePos(MessageType messageType, byte[] message)
    {        
        byte[] posBytes = new byte[sizeof(float) * 3];
        //BlockCopy(source, srcStart, dst, destStart, length)
        Buffer.BlockCopy(message, 0, posBytes, 0, sizeof(float) * 3);
        Vector3 pos = Utility.BytesToVector3(posBytes);

        byte[] nameBytes = new byte[message.Length - posBytes.Length];
        Buffer.BlockCopy(message, posBytes.Length, nameBytes, 0, nameBytes.Length);
        String name = Utility.BytesToString(nameBytes);

        DebugWindow.DebugMessage("Recv'd " + name + ": " + pos.ToString());
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

