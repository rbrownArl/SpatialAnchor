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
    private TcpListener tcpListener = null;

    List<byte[]> receivedMessages = null;
    //byte[] importedAnchor = null;

    enum MessageType : UInt32
    {
        SendIp       = 0x4321,
        SendPos      = 0x7EE7,
        ClaimAnchor  = 0xB00B,
        ReleaseAnchor= 0xFFEE,
        SendAnchor   = 0xBB00,
        SendingAnchor= 0xBB0F,
        DoneAnchor   = 0xBBFF,
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

        ips = new HashSet<string>();
        receivedMessages = new List<byte[]>();

        tcpListener = new TcpListener(anchorPort.ToString());
        tcpListener.TcpReceiveEvent += TcpMessageReceivedEvent;

        udpListener = new UdpListener();
        readIpThread = new Thread(new ThreadStart(UdpListener));
        readIpThread.Start();

        StartCoroutine("BroadcastIp");
    }

    // Update is called once per frame
    void Update()
    {
        //work on the message queue
        if (receivedMessages.Count > 0)
        {
            ProcessMessage(receivedMessages[0]);
            receivedMessages.RemoveAt(0);

        }
    }

    //Create a UDP message
    //header:
    //  number of bytes for ip 
    //  ip bytes (as string... oops)
    //  4-byte messageType
    //add the data message to the tail of the message
    private void BroadcastUdpData(byte[] dataBytes)
    {
        try
        {
            IPAddress.Parse(machineIp);
        }
        catch (Exception e)
        {
            DebugWindow.DebugMessage("Bad Ip");
            return;
        }

        byte[] bytes = new byte[] { (byte)(machineIp.Length) };
        AppendBytes(ref bytes, Encoding.UTF8.GetBytes(machineIp));
        AppendBytes(ref bytes, dataBytes);

        new UdpBroadcastData(ipPort, bytes);
    }

    private IEnumerator BroadcastIp()
    {
        if (machineIp == null)
        {
            DebugWindow.DebugMessage("No IP");
            yield return null;
        }

        yield return new WaitForSeconds(2.0f);
        //DebugWindow.DebugMessage("BroadcastIp " + machineIp);


        byte[] bytes = MessageTypeToBytes(MessageType.SendIp);
        AppendBytes(ref bytes, Encoding.UTF8.GetBytes(machineIp));

        BroadcastUdpData(bytes);

        yield return new WaitForSeconds(18.0f);
        StartCoroutine("BroadcastIp");
    }

    private void BroadcastPosOnce()
    {
        Vector3 pos = new Vector3(1, 2, 3);

        //DebugWindow.DebugMessage("Send Pos");

        byte[] bytes = MessageTypeToBytes(MessageType.SendPos);
        AppendBytes(ref bytes, Vector3ToBytes(pos));

        BroadcastUdpData(bytes);
    }

    public void BroadcastClaimAnchor(GameObject anchoredObject)
    {
        byte[] bytes = MessageTypeToBytes(MessageType.ClaimAnchor);

        AppendBytes(ref bytes, new byte[] { (byte)anchoredObject.name.Length });
        AppendBytes(ref bytes, Encoding.UTF8.GetBytes(anchoredObject.name));

        //Broadcast that we claimed the anchor
        BroadcastUdpData(bytes);
    }

    public void BroadcastReleaseAnchor(GameObject anchoredObject)
    {
        //Broadcast the we released the anchor?  
        byte[] bytes = MessageTypeToBytes(MessageType.ReleaseAnchor);

        AppendBytes(ref bytes, new byte[] { (byte)anchoredObject.name.Length });
        AppendBytes(ref bytes, Encoding.UTF8.GetBytes(anchoredObject.name));


        //Broadcast that we released the anchor
        BroadcastUdpData(bytes);
    }

    public void BroadcastSendAnchor(GameObject anchoredObject)
    {
        //Broadcast the we released the anchor?  
        byte[] bytes = MessageTypeToBytes(MessageType.SendAnchor);

        AppendBytes(ref bytes, new byte[] { (byte)anchoredObject.name.Length });
        AppendBytes(ref bytes, Encoding.UTF8.GetBytes(anchoredObject.name));


        //Broadcast that we start sending the anchor
        BroadcastUdpData(bytes);
    }

    public void BroadcastDoneAnchor(GameObject anchoredObject)
    {
        //Broadcast the we released the anchor?  
        byte[] bytes = MessageTypeToBytes(MessageType.DoneAnchor);

        AppendBytes(ref bytes, new byte[] { (byte)anchoredObject.name.Length });
        AppendBytes(ref bytes, Encoding.UTF8.GetBytes(anchoredObject.name));


        //Broadcast that we finished sending the anchor
        BroadcastUdpData(bytes);
    }

    //Add udp messages to a FIFO queue
    private void UdpListener()
    {
        while (true)
        {
            try
            {
                byte[] receivedBytes = udpListener.ReceiveUdpData(ipPort);
                receivedMessages.Add(receivedBytes);
            }
            catch(Exception e)
            {
                DebugWindow.DebugMessage("Error UdpListener: " + e);
            }
        }
    }

    //Handle TCP and UDP messages
    //read source IP first
    //read message Type
    //send the rest of the message to the correct header
    public void ProcessMessage(byte[] receivedBytes)
    {
        int sourceIpLen = (int)(receivedBytes[0]);
        string sourceIp = Encoding.UTF8.GetString(receivedBytes.Skip(1).Take(sourceIpLen).ToArray());

        byte[] messageTypeBytes = receivedBytes.Skip(1 + sourceIpLen).Take(4).ToArray();
        MessageType messageType = (MessageType)BitConverter.ToUInt32(messageTypeBytes, 0);

        //                                       ipLen + ipString + messageType
        byte[] receivedMessage = receivedBytes.Skip(1 + sourceIpLen + 4).ToArray();

     
        if (sourceIp.Equals(machineIp))
            return;

        
        DebugWindow.DebugMessage("Process Message from " + sourceIp + ": " + messageType.ToString() + " " + Encoding.UTF8.GetString(receivedMessage));

        switch (messageType)
        {
            case MessageType.SendIp:
                OnUpdateIps(sourceIp, messageType, receivedMessage);
                break;
            //case MessageType.SendPos:
            //    OnUpdatePos(sourceIp, messageType, receivedMessage);
            //    break;
            case MessageType.ClaimAnchor:
                OnClaimAnchor(sourceIp, messageType, receivedMessage);
                break;
            case MessageType.ReleaseAnchor:
                OnReleaseAnchor(sourceIp, messageType, receivedMessage);
                break;
            case MessageType.SendAnchor:
                DebugWindow.DebugMessage("Starting to send an anchor");
                OnSendAnchor(sourceIp, messageType, receivedMessage);
                break;
            case MessageType.SendingAnchor:
                DebugWindow.DebugMessage("Importing an Anchor!?");
                ImportAnchor(receivedMessage);
                break;
            case MessageType.DoneAnchor:
                DebugWindow.DebugMessage("Anchor Completely Sent");
                OnDoneAnchor(sourceIp, messageType, receivedMessage);
                break;
        }
    }

    private void OnUpdatePos(string sourceIp, MessageType messageType, byte[] posBytes)
    {
        Vector3 pos = BytesToVector3(posBytes);

        DebugWindow.DebugMessage(messageType.ToString() + ": " + pos.ToString());


    }

    private void OnUpdateIps(string sourceIp, MessageType messageType, byte[] bytes)
    {
        string ipString = Encoding.UTF8.GetString(bytes);

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
                BroadcastIp();
            }
        }

    }

    private void OnClaimAnchor(string sourceIp, MessageType messageType, byte[] bytes)
    {
        int anchorNameLength = (int)bytes[0];  
        byte[] anchorNameBytes = bytes.Skip(1).Take(anchorNameLength).ToArray();

        string anchorName = Encoding.UTF8.GetString(anchorNameBytes);

        DebugWindow.DebugMessage(sourceIp + " has claimed anchor " + anchorName);

        if (GameObject.Find(anchorName) != null)
        {
            GameObject.Find(anchorName).GetComponent<MoveMe>().OnClaimed();
        }
    }

    private void OnReleaseAnchor(string sourceIp, MessageType messageType, byte[] bytes)
    {
        int anchorNameLength = (int)bytes[0];
        byte[] anchorNameBytes = bytes.Skip(1).Take(anchorNameLength).ToArray();

        string anchorName = Encoding.UTF8.GetString(anchorNameBytes);

        DebugWindow.DebugMessage(sourceIp + " has released anchor " + anchorName);

        if (GameObject.Find(anchorName) != null)
        {
            GameObject.Find(anchorName).GetComponent<MoveMe>().OnUpdated();
        }
    }

    private void OnSendAnchor(string sourceIp, MessageType messageType, byte[] bytes)
    {
        int anchorNameLength = (int)bytes[0];
        byte[] anchorNameBytes = bytes.Skip(1).Take(anchorNameLength).ToArray();

        string anchorName = Encoding.UTF8.GetString(anchorNameBytes);

        DebugWindow.DebugMessage(sourceIp + " is starting to send " + anchorName);

        if (GameObject.Find(anchorName) != null)
        {
            GameObject.Find(anchorName).GetComponent<MoveMe>().OnClaimed();
        }
    }

    private void OnDoneAnchor(string sourceIp, MessageType messageType, byte[] bytes)
    {
        int anchorNameLength = (int)bytes[0];
        byte[] anchorNameBytes = bytes.Skip(1).Take(anchorNameLength).ToArray();

        string anchorName = Encoding.UTF8.GetString(anchorNameBytes);

        DebugWindow.DebugMessage(sourceIp + " has finished sending anchor " + anchorName);

        if (GameObject.Find(anchorName) != null)
        {
            GameObject.Find(anchorName).GetComponent<MoveMe>().OnUpdated();
        }
    }

    public void TcpMessageReceivedEvent(byte[] data) 
    {
        DebugWindow.DebugMessage("TcpMessageRecieved" + data.Length);
        receivedMessages.Add(data);
    }

    public void TcpMessageSentEvent(byte[] data)
    {
        DebugWindow.DebugMessage("TcpMessageSent Complete " + BitConverter.ToUInt32(data,0) );
    }

    public GameObject CreateOrUpdateAnchorObject(GameObject prefab, string gameObjectName)
    {
        GameObject existing = GameObject.Find(gameObjectName);

        if (existing == null)
        {
            GameObject defaultObject = Instantiate(prefab, new Vector3(0f, 0f, 0.75f), Quaternion.identity);
            defaultObject.name = gameObjectName;
            defaultObject.GetComponent<MoveMe>().anchorManager = thisAnchorManager;
            //BroadcastPosOnce();

            return defaultObject;
        }
        else
        {
            //BroadcastPosOnce();
            return existing;
        }

    }

    public void MoveAnchorObject(GameObject anchoredObject)
    {
        DebugWindow.DebugMessage(machineIp + " is claiming " + anchoredObject.name);

        //Claim the anchor from other users
        BroadcastClaimAnchor(anchoredObject);

        //Destroy the unity world anchor attached to anchoredObject
        ClearAnchor(anchoredObject);

    }

    //On move anchor
    public void LockAnchorObject(GameObject anchoredObject)
    {
        DebugWindow.DebugMessage(machineIp + " is releasing " + anchoredObject.name);

        //Store updated anchor
        SaveAnchor(anchoredObject);

        //Export anchor
        //Broadcast updated anchor
        ExportAnchor(anchoredObject.name, anchoredObject.GetComponent<WorldAnchor>());

        //Release the anchor back to other users
        BroadcastReleaseAnchor(anchoredObject);
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

        DebugWindow.DebugMessage("Saved anchor " + anchoredObject.name);
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

                string path = string.Format("{0}/{1}.bin", Application.persistentDataPath, anchorId + "-serializedTransferAnchor.bin");
                File.WriteAllBytes(path, serializedWorldAnchor);

                foreach (string ip in ips)
                {
                    void OnTcpMessageSent(byte[] data)
                    {
                        DebugWindow.DebugMessage("Tcp Send complete " + BitConverter.ToUInt32(data, 0));

                        //Release the anchor back to other users
                        //BroadcastReleaseAnchor(GameObject.Find(anchorId));

                        BroadcastDoneAnchor(GameObject.Find(anchorId));
                    }

                    BroadcastSendAnchor(GameObject.Find(anchorId));

                    byte[] bytes = new byte[] { };
                    AppendSourceIp(ref bytes);
                    AppendBytes(ref bytes, MessageTypeToBytes(MessageType.SendingAnchor));
                    AppendBytes(ref bytes, serializedWorldAnchor);

                    //TcpSend tcpSender = new TcpSend(ip, anchorPort.ToString(), bytes, OnTcpMessageSent);
                    //tcpSender.TcpSendCompleteEvent += OnTcpMessageSent; //ew... this gets set AFTER data is sent. bad.

                    TcpSend tcpSender = new TcpSend();
                    tcpSender.TcpSendCompleteEvent += OnTcpMessageSent;
                    tcpSender.SendData(ip, anchorPort.ToString(), serializedWorldAnchor);
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


    private void AppendSourceIp(ref byte[] bytes)
    {
        byte[] sourceIpBytes = Encoding.UTF8.GetBytes(machineIp);
        byte[] sourceIpLengthBytes = new byte[] { (byte)machineIp.Length };

        byte[] b3 = new byte[bytes.Length + sourceIpBytes.Length + 1];

        bytes.CopyTo(b3, 0);
        sourceIpLengthBytes.CopyTo(b3, bytes.Length);
        sourceIpBytes.CopyTo(b3, bytes.Length + 1);

        bytes = b3;
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
