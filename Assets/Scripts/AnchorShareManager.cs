using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;

using UnityEngine;
using UnityEngine.XR.WSA;
using UnityEngine.XR.WSA.Persistence;
using UnityEngine.XR.WSA.Sharing;

public class AnchorShareManager : MonoBehaviour
{
    public GameObject prefab;

    private AnchorShareManager thisAnchorManager;

    private WorldAnchorStore store;
    private WorldAnchorTransferBatch transferBatch;

    private Thread readAnchorThread;
    private Thread readThread;
    private Thread readIpThread;

    private int textPort = 9999;
    private int anchorPort = 4444;
    private int ipPort = 9998;

    public float iPBroadcastRate = 30.0f;
    HashSet<string> ips;

    private string machineName;
    private string machineIp = "No Ip";

    public string targetIp;

    private UdpListener udpListener = null;
    private TcpConnection tcpListener = null;

    List<byte[]> receivedMessages = null;
    //byte[] importedAnchor = null;

    enum MessageType : UInt32
    {
        SendIp       = 0x4321,
        SendPos      = 0x7EE7,
        SendAnchor   = 0xBEEF,
        DoneAnchor   = 0xDEAD,
        CancelAnchor = 0x0000
    }

    enum ImportState : int
    {
        NoImport      = 0,
        StartImport   = 1,
        Importing     = 2,
        DoneImporting = 3
    }

    private ImportState importState = ImportState.NoImport;

    // Start is called before the first frame update
    void Start()
    {
        machineName = SystemInfo.deviceName;
        machineIp = getMachineIp();

        thisAnchorManager = gameObject.GetComponent<AnchorShareManager>();
        
        //read WorldAnchorTransferBatch for existing anchors
        WorldAnchorStore.GetAsync(AnchorStoreLoaded);

        udpListener = new UdpListener();
        tcpListener = new TcpConnection(anchorPort.ToString());
        tcpListener.TcpReceiveEvent += TcpMessageReceivedEvent;

        receivedMessages = new List<byte[]>();

        ips = new HashSet<string>();
        readIpThread = new Thread(new ThreadStart(UdpListener));
        readIpThread.Start();

        StartCoroutine("IpBroadcast");
        //IpBroadcastOnce();
    }

    // Update is called once per frame
    void Update()
    {
        if (importState == ImportState.StartImport)
        {
            DebugWindow.DebugMessage("ImportState StartImport");
            //ImportAnchor(importedAnchor);
            ImportAnchor(receivedMessages[0]);
            receivedMessages.RemoveAt(0);
        }


    }


    private IEnumerator IpBroadcast()
    //private void IpBroadcastOnce()
    {
        if (machineIp == null)
        {
            DebugWindow.DebugMessage("No IP");
            yield return null;
        }
        
        yield return new WaitForSeconds(2.0f);
        DebugWindow.DebugMessage("IpBroadcast");

        byte[] bytes = MessageTypeToBytes(MessageType.SendIp);
        AppendBytes(ref bytes, Encoding.UTF8.GetBytes(machineIp));

        //udpBroadcast.SendMulticastUdpData(ipPort, Encoding.UTF8.GetBytes(machineIp));
        new UdpBroadcastData(ipPort, bytes);

        yield return new WaitForSeconds(58.0f);
        StartCoroutine("IpBroadcast");
    }

    private void BroadcastPosOnce()
    {
        Vector3 pos = new Vector3(1, 2, 3);

        DebugWindow.DebugMessage("Send Pos");
        byte[] bytes = MessageTypeToBytes(MessageType.SendPos);
        AppendBytes(ref bytes, Vector3ToBytes(pos));

        new UdpBroadcastData(ipPort, bytes);
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
            catch(Exception e)
            {
                DebugWindow.DebugMessage("UdpListener: " + e);
            }
        }
    }
    
    private void UpdatePos(MessageType messageType, byte[] posBytes)
    {
        Vector3 pos = BytesToVector3(posBytes);

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

    public void TcpMessageReceivedEvent(byte[] data) 
    {
        DebugWindow.DebugMessage("Got anchor?" + data.Length);
        receivedMessages.Add(data);
        //ImportAnchor(data);
        //importedAnchor = data;
        importState = ImportState.StartImport;
    }

    public void TcpMessageSentEvent(byte[] data)
    {
        DebugWindow.DebugMessage("Tcp Send complete " + BitConverter.ToUInt32(data,0));
    }

    public GameObject CreateOrUpdateAnchorObject(GameObject prefab, string gameObjectName)
    {
        GameObject existing = GameObject.Find(gameObjectName);

        if (existing == null)
        {
            GameObject defaultObject = Instantiate(prefab, new Vector3(0f, 0f, 0.75f), Quaternion.identity);
            defaultObject.name = gameObjectName;
            defaultObject.GetComponent<MoveMe>().anchorManager = thisAnchorManager;
            BroadcastPosOnce();

            return defaultObject;
        }
        else
        {
            BroadcastPosOnce();
            return existing;
        }

    }

    public void MoveAnchorObject(GameObject anchoredObject)
    {
        //Destroy the unity world anchor attached to anchoredObject
        ClearAnchor(anchoredObject);
    }

    //On move anchor
    public void LockAnchorObject(GameObject anchoredObject)
    {
        //Store updated anchor
        SaveAnchor(anchoredObject);

        //Export anchor
        //Broadcast updated anchor
        ExportAnchor(anchoredObject.name, anchoredObject.GetComponent<WorldAnchor>());
    }

    private void AnchorStoreLoaded(WorldAnchorStore store)
    {
        this.store = store;
        LoadAnchors();

        //if no existing anchor, create a prefab at known location... (don't store until move)
        if (store.anchorCount == 0)
        {
            GameObject defaultObject = CreateOrUpdateAnchorObject(prefab, prefab.name + "-" + SystemInfo.deviceName);
            DebugWindow.DebugMessage("Created " + defaultObject.name + " at " + defaultObject.transform.position.ToString());
        }
    }

    private void LoadAnchor()
    {
        bool retTrue;
        retTrue = store.Load(gameObject.name.ToString(), gameObject);

        if (!retTrue)
        {
            //until anchoredObject has anchor saved at least once, it will not be in the store
        }
    }

    private void LoadAnchors()
    {

        DebugWindow.DebugMessage("Number of anchors: " + store.anchorCount);


        //Instantiate a prefab for each existing anchor
        //If/when the existing anchor is located, the prefab will automatically bounce to correct location.
        foreach (string anchorId in store.GetAllIds())
        {
            DebugWindow.DebugMessage("Trying anchor " + anchorId);
            GameObject newAnchoredObject = CreateOrUpdateAnchorObject(prefab, anchorId);
            DebugWindow.DebugMessage("It's position is " + newAnchoredObject.transform.position.ToString());
            
            WorldAnchor anchor = store.Load(anchorId, newAnchoredObject);
            if (anchor != null)
            {
                DebugWindow.DebugMessage(anchor.isLocated ? "It is Located" : "It is NOT Located");
            }
            else
            {
                DebugWindow.DebugMessage("It has a null anchor");
            }
        }

    }

    //Create the unity world anchor attached to anchoredObject
    //Delete the named spatial anchor from the store
    //Save a named spatial anchor to the store with the unity world anchor
    private void SaveAnchor(GameObject anchoredObject)
    {
        bool retTrue;
        WorldAnchor anchor = anchoredObject.AddComponent<WorldAnchor>();
        store.Delete(anchoredObject.name.ToString());

        retTrue = store.Save(anchoredObject.name.ToString(), anchor);
        if (!retTrue)
        {
            DebugWindow.DebugMessage("Anchor save failed");
        }
    }

    //Delete the unity world anchor to allow the gameObject to be moved
    private void ClearAnchor(GameObject anchoredObject)
    {
        WorldAnchor anchor = anchoredObject.GetComponent<WorldAnchor>();
        if (anchor)
        {
            DestroyImmediate(anchor);
        }
    }

    //Serialize and send WorldAnchor over network
    private void ExportAnchor(string anchorId, WorldAnchor transferAnchor)
    {
        
        byte[] serializedWorldAnchor = new byte[0];

        void OnExportDataAvailable(byte[] data)
        {
            //send to other h2
            //DebugWindow.DebugMessage("Export Data available " + data.Length + " " + serializedWorldAnchor.Length);
            AppendBytes(ref serializedWorldAnchor, data);
        }

        void OnExportComplete(SerializationCompletionReason reason)
        {
            
            if (reason == SerializationCompletionReason.Succeeded)
            {
                DebugWindow.DebugMessage("Export Complete " + reason.ToString());
                DebugWindow.DebugMessage("Sending anchor");

                //tcpConnection.anchorSend = serializedWorldAnchor;

                string path = string.Format("{0}/{1}.bin", Application.persistentDataPath, anchorId + "-serializedTransferAnchor.bin");
                File.WriteAllBytes(path, serializedWorldAnchor);

                foreach (string ip in ips)
                {
                    void OnTcpMessageSent(byte[] data)
                    {
                        DebugWindow.DebugMessage("Tcp Send complete " + BitConverter.ToUInt32(data, 0));
                    }

                    TcpConnection tcpSender = new TcpConnection();
                    tcpSender.TcpSendCompleteEvent += OnTcpMessageSent;
                    tcpSender.SendAnchor(ip, anchorPort.ToString(), serializedWorldAnchor);
                }
            }
            else
            {
                DebugWindow.DebugMessage("Export Failed cuz " + reason.ToString());
            }
        }

        try
        {
            DebugWindow.DebugMessage("ExportAnchor" + anchorId);

            WorldAnchorTransferBatch transferBatch = new WorldAnchorTransferBatch();

            transferBatch.AddWorldAnchor(anchorId, transferAnchor);
            WorldAnchorTransferBatch.ExportAsync(transferBatch, OnExportDataAvailable, OnExportComplete);



        }
        catch (Exception e)
        {
            DebugWindow.DebugMessage("Export Anchor " + e.ToString());
        }
    }

    //Read WorldAnchor from network and deserialize it, update anchor
    private void ImportAnchor(byte[] importedData)
    {
        importState = ImportState.Importing;
        try
        {
            void OnImportComplete(SerializationCompletionReason reason, WorldAnchorTransferBatch importedBatch)
            {
                DebugWindow.DebugMessage("Import Complete cuz " + reason.ToString());

                if (reason == SerializationCompletionReason.Succeeded)
                {
                    string[] ids = importedBatch.GetAllIds();

                    foreach (string id in ids)
                    {
                        DebugWindow.DebugMessage("--" + id);
                        GameObject importedObject = CreateOrUpdateAnchorObject(prefab, id);
                        importedBatch.LockObject(id, importedObject);
                    }
                }
                else
                {
                    DebugWindow.DebugMessage("Import Complete cuz failed " + reason.ToString());
                }

                importState = ImportState.NoImport;
            }

            DebugWindow.DebugMessage("Entered ImportAnchor");

            //Import anchor (automatically updates position)
            WorldAnchorTransferBatch.ImportAsync(importedData, OnImportComplete);
            DebugWindow.DebugMessage("ImportAnchor " + importedData.Length);


        }
        catch (Exception e)
        {
            DebugWindow.DebugMessage("Import Anchor Failed " + e.ToString());
        }
    }

    private byte[] MessageTypeToBytes(MessageType message)
    {
        return BitConverter.GetBytes((UInt32)message);
    }

    private void AppendBytes(ref byte[] b1, byte[] b2)
    {
        byte[] b3 = new byte[b1.Length + b2.Length];

        b1.CopyTo(b3, 0);
        b2.CopyTo(b3, b1.Length);

        b1 = b3;
    }

    private byte[] Vector3ToBytes(Vector3 vect)
    {
        byte[] buff = new byte[sizeof(float) * 3];
        Buffer.BlockCopy(BitConverter.GetBytes(vect.x), 0, buff, 0 * sizeof(float), sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(vect.y), 0, buff, 1 * sizeof(float), sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(vect.z), 0, buff, 2 * sizeof(float), sizeof(float));

        return buff;
    }

    private Vector3 BytesToVector3(byte[] bytes)
    {
        Vector3 vect = Vector3.zero;
        vect.x = BitConverter.ToSingle(bytes, 0 * sizeof(float));
        vect.y = BitConverter.ToSingle(bytes, 1 * sizeof(float));
        vect.z = BitConverter.ToSingle(bytes, 2 * sizeof(float));

        return vect;
    }

    private string getMachineIp()
    {
        try
        {
            foreach (IPAddress ip in Dns.GetHostAddresses(machineName))
            {
                DebugWindow.DebugMessage(ip.ToString());
                if (ip.ToString().StartsWith("192"))
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
