using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

using UnityEngine;
using System.Net.NetworkInformation;

class Utility
{
    public static byte[] MessageTypeToBytes(UInt32 message)
    {
        return BitConverter.GetBytes((UInt32)message);
    }

    public static void AppendBytes(ref byte[] b1, byte[] b2)
    {
        byte[] b3 = new byte[b1.Length + b2.Length];

        b1.CopyTo(b3, 0);
        b2.CopyTo(b3, b1.Length);

        b1 = b3;
    }

    public static byte[] Vector3ToBytes(Vector3 vect)
    {
        byte[] buff = new byte[sizeof(float) * 3];
        Buffer.BlockCopy(BitConverter.GetBytes(vect.x), 0, buff, 0 * sizeof(float), sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(vect.y), 0, buff, 1 * sizeof(float), sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(vect.z), 0, buff, 2 * sizeof(float), sizeof(float));

        return buff;
    }

    public static Vector3 BytesToVector3(byte[] bytes)
    {
        Vector3 vect = Vector3.zero;
        vect.x = BitConverter.ToSingle(bytes, 0 * sizeof(float));
        vect.y = BitConverter.ToSingle(bytes, 1 * sizeof(float));
        vect.z = BitConverter.ToSingle(bytes, 2 * sizeof(float));

        return vect;
    }

    public static byte[] StringToBytes(String text)
    {
        byte[] buff = System.Text.Encoding.UTF8.GetBytes(text);

        return buff;
    }

    public static String BytesToString(byte[] bytes)
    {
        String text = System.Text.Encoding.UTF8.GetString(bytes);

        return text;
    }

    //public static string DisplayUnicastAddresses()
    public static string getMachineIp(string machineName)
    {
        string machineIp = "";
        try
        {
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in adapters)
            {
                DebugWindow.DebugMessage("checking *" + adapter.Description + ":" + adapter.Name + "*");
                if (adapter.Name.StartsWith("Wi-Fi"))
                {
                    IPInterfaceProperties adapterProperties = adapter.GetIPProperties();
                    IPv4InterfaceProperties ipv4adapterProperties = adapterProperties.GetIPv4Properties();
                    UnicastIPAddressInformationCollection uniCast = adapterProperties.UnicastAddresses;
                    if (uniCast.Count > 0)
                    {
                        DebugWindow.DebugMessage(adapter.Name);
                        DebugWindow.DebugMessage(adapter.Description);
                        foreach (UnicastIPAddressInformation uni in uniCast)
                        {
                            DebugWindow.DebugMessage(uni.Address.ToString());

                            if (uni.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                DebugWindow.DebugMessage("Using Ip: " + uni.Address);
                                machineIp = uni.Address.ToString();
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            DebugWindow.DebugMessage("get HostIp error " + e);
        }
        return machineIp;
    }

    public static string getMachineIpX(string machineName)
    {
        string machineIp = "";
        try
        {
            foreach (IPAddress ip in Dns.GetHostAddresses(machineName))
            {
                DebugWindow.DebugMessage(ip.ToString());
                /*if (ip.ToString().StartsWith("10"))*/
                {
                    machineIp = ip.ToString();
                    DebugWindow.DebugMessage("Using IP: " + machineIp);
                }
            }
        }
        catch (Exception e)
        {
            DebugWindow.DebugMessage("get HostIp error " + e);
        }
        return machineIp;
    }
}

