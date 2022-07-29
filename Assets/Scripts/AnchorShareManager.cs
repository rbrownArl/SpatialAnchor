using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

    private UdpConnection udpBroadcast = null;
    private TcpConnection tcpConnection = null;

    byte[] importedAnchor = null;

    enum ImportState : int
    {
        NoImport = 0,
        StartImport = 1,
        Importing = 2,
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

        udpBroadcast = new UdpConnection();
        tcpConnection = new TcpConnection(anchorPort.ToString());
        tcpConnection.TcpMessageEvent += TcpMessageReceivedEvent;

        ips = new HashSet<string>();
        readIpThread = new Thread(new ThreadStart(IpListener));
        readIpThread.Start();

        IpBroadcastOnce();
    }

    // Update is called once per frame
    void Update()
    {
        if (importState == ImportState.StartImport)
        {
            ImportAnchor(importedAnchor);
        }
    }


    private void IpBroadcastOnce()
    {
        DebugWindow.DebugMessage("IpBroadcast");

        if (machineIp == null)
        {
            DebugWindow.DebugMessage("No IP");
            return;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(machineIp);

        udpBroadcast.SendMulticastUdpData(ipPort, Encoding.UTF8.GetBytes(machineIp));
    }

    private void IpListener()
    {
        byte[] receivedBytes = null;
        string recievedIp = null;

        DebugWindow.DebugMessage("IpListener");
        while (true)
        {
            try
            {
                receivedBytes = udpBroadcast.ReceiveUdpData(ipPort);

                recievedIp = Encoding.UTF8.GetString(receivedBytes);
                IPAddress.Parse(recievedIp);

                if (recievedIp != machineIp)
                {
                    if (!ips.Contains(recievedIp))
                    {
                        ips.Add(recievedIp);
                        DebugWindow.DebugMessage("Known IPs");
                        foreach (string ip in ips)
                        {
                            DebugWindow.DebugMessage("  " + ip);
                        }
                        IpBroadcastOnce();
                    }
                }
            }
            catch (Exception e)
            {
                DebugWindow.DebugMessage("IpListener: " + e);
            }
        }
    }

    public void TcpMessageReceivedEvent(byte[] data) 
    {
        DebugWindow.DebugMessage("Got anchor?" + data.Length);
        importedAnchor = tcpConnection.anchorReceive;
        importState = ImportState.StartImport;
    }

    public GameObject CreateOrUpdateAnchorObject(GameObject prefab, string gameObjectName)
    {
        GameObject existing = GameObject.Find(gameObjectName);

        if (existing == null)
        {
            GameObject defaultObject = Instantiate(prefab, new Vector3(0f, 0f, 0.75f), Quaternion.identity);
            defaultObject.name = gameObjectName;
            defaultObject.GetComponent<MoveMe>().anchorManager = thisAnchorManager;

            return defaultObject;
        }
        else
            return existing;

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

        Debug.Log("Number of anchors: " + store.anchorCount);


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

                tcpConnection.anchorSend = serializedWorldAnchor;

                string path = string.Format("{0}/{1}.bin", Application.persistentDataPath, anchorId + "-serializedTransferAnchor.bin");
                File.WriteAllBytes(path, serializedWorldAnchor);
                foreach (string ip in ips)
                {
                    tcpConnection.SendAnchor(ip);
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

                importState = ImportState.NoImport;
            }

            DebugWindow.DebugMessage("Entered ImportAnchor");

            //Import anchor (automatically updates position)
            WorldAnchorTransferBatch.ImportAsync(importedData, OnImportComplete);
            DebugWindow.DebugMessage("ImportAnchor " + importedData.Length);


        }
        catch (Exception e)
        {
            DebugWindow.DebugMessage("Import Anchor " + e.ToString());
        }
    }

    private void AppendBytes(ref byte[] b1, byte[] b2)
    {
        byte[] b3 = new byte[b1.Length + b2.Length];

        b1.CopyTo(b3, 0);
        b2.CopyTo(b3, b1.Length);

        b1 = b3;
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
