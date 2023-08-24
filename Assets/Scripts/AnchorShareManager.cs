using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.XR.WSA;
using UnityEngine.XR.WSA.Persistence;
using UnityEngine.XR.WSA.Sharing;

public class AnchorShareManager : MonoBehaviour
{
    public GameObject anchorPrefab;
    public GameObject objectPrefab;

    private AnchorShareManager thisAnchorManager;
    private NetworkDiscoveryManager thisNetworkDiscoveryManager;

    private WorldAnchorStore store;

    private int textPort = 9999;
    private int anchorPort = 4444;

    private TcpConnection tcpListener = null;

    List<byte[]> receivedMessages = null;

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
        thisAnchorManager = gameObject.GetComponent<AnchorShareManager>();

        thisNetworkDiscoveryManager = GameObject.Find("NetworkDiscoveryManager").GetComponent<NetworkDiscoveryManager>();
         
        //read WorldAnchorTransferBatch for existing anchors
        WorldAnchorStore.GetAsync(AnchorStoreLoaded);

        //udpListener = new UdpListener();
        tcpListener = new TcpConnection(anchorPort.ToString());
        tcpListener.TcpReceiveEvent += TcpMessageReceivedEvent;

        receivedMessages = new List<byte[]>();

    }

    // Update is called once per frame
    void Update()
    {
        if (importState == ImportState.StartImport)
        {
            ImportAnchor(receivedMessages[0]);
            receivedMessages.RemoveAt(0);
        }
    }

    public void TcpMessageReceivedEvent(byte[] data)
    {
        DebugWindow.DebugMessage("Got anchor?" + data.Length);
        receivedMessages.Add(data);

        importState = ImportState.StartImport;
    }

    public void TcpMessageSentEvent(byte[] data)
    {
        DebugWindow.DebugMessage("Tcp Send complete " + BitConverter.ToUInt32(data, 0));
    }

    public GameObject CreateOrUpdateAnchorObject(GameObject anchorPrefab, string gameObjectName)
    {
        GameObject existing = GameObject.Find(gameObjectName);

        if (existing == null)
        {
            GameObject anchor = Instantiate(anchorPrefab, new Vector3(0f, 0f, 0.75f), Quaternion.identity);
            anchor.name = gameObjectName;

            anchor.GetComponent<MoveAnchor>().objectPrefab = objectPrefab;
            thisNetworkDiscoveryManager.BroadcastPosOnce(anchorPrefab);

            return anchor;
        }
        else
        {
            thisNetworkDiscoveryManager.BroadcastPosOnce(existing);
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

        //if no existing anchor, create a anchorPrefab at known location... (don't store until move)
        if (store.anchorCount == 0)
        {
            DebugWindow.DebugMessage(anchorPrefab.name);
            GameObject defaultObject = CreateOrUpdateAnchorObject(anchorPrefab, anchorPrefab.name + "-" + SystemInfo.deviceName);
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

        //Instantiate a anchorPrefab for each existing anchor
        //If/when the existing anchor is located, the anchorPrefab will automatically bounce to correct location.
        foreach (string anchorId in store.GetAllIds())
        {
            DebugWindow.DebugMessage("Trying anchor " + anchorId);
            GameObject newAnchoredObject = CreateOrUpdateAnchorObject(anchorPrefab, anchorId);
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
        WorldAnchor anchor = (anchoredObject.GetComponent<WorldAnchor>() == null) ? 
                             anchoredObject.AddComponent<WorldAnchor>() : 
                             anchoredObject.GetComponent<WorldAnchor>();
        
        bool deleted = store.Delete(anchoredObject.name.ToString());
        bool retTrue = store.Save(anchoredObject.name.ToString(), anchor);
        
        if (!retTrue)
        {
            DebugWindow.DebugMessage("Anchor save failed");
        }
    }

    //Delete the unity world anchor to allow the gameObject to be moved
    private void ClearAnchor(GameObject anchoredObject)
    {
        DebugWindow.DebugMessage("Starting ClearAnchor");
        WorldAnchor anchor = anchoredObject.GetComponent<WorldAnchor>();
        if (anchor)
        {
            DestroyImmediate(anchor);
        }
        DebugWindow.DebugMessage("Ending ClearAnchor");
    }

    //Serialize and send WorldAnchor over network
    private void ExportAnchor(string anchorId, WorldAnchor transferAnchor)
    {

        byte[] serializedWorldAnchor = new byte[0];

        void OnExportDataAvailable(byte[] data)
        {
            //send to other h2
            //DebugWindow.DebugMessage("Export Data available " + data.Length + " " + serializedWorldAnchor.Length);
            Utility.AppendBytes(ref serializedWorldAnchor, data);
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

                foreach (string ip in thisNetworkDiscoveryManager.GetIps())
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
                        GameObject importedObject = CreateOrUpdateAnchorObject(anchorPrefab, id);
                        importedBatch.LockObject(id, importedObject);

                        SaveAnchor(importedObject);
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
}
