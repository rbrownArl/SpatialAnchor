using System;
using System.Collections;
using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.XR.WSA;
using UnityEngine.XR.WSA.Persistence;
using UnityEngine.XR.WSA.Sharing;

public class MoveAnchor : MonoBehaviour
{
    public Material selectedColor;
    public Material originalColor;
    public Material lostColor;

    public AnchorShareManager anchorManager;

    public GameObject objectPrefab;

    public Vector3 initialObjectPosition = new Vector3(0f, 0f, 0.7f);

    private WorldAnchor anchor;
    private NetworkDiscoveryManager networkDiscoveryManager;

    // Start is called before the first frame update
    void Start()
    {
        networkDiscoveryManager = GameObject.Find("NetworkDiscoverManager").GetComponent<NetworkDiscoveryManager>();

        if (originalColor == null)
            originalColor = gameObject.GetComponent<Renderer>().material;

        DebugWindow.DebugMessage("Hi, I am " + name + " at " + gameObject.transform.position);
        
        anchor = gameObject.GetComponent<WorldAnchor>();
        if (anchor != null)
        {
            anchor.OnTrackingChanged += Anchor_OnTrackingChanged;
            DebugWindow.DebugMessage(anchor.isLocated ? "I am Located" : "I am NOT Located");
        }
        else
        {
            DebugWindow.DebugMessage("I have a null anchor");
        }

        Anchor_OnTrackingChanged(anchor, anchor.isLocated);
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void Grab()
    {
        gameObject.GetComponent<Renderer>().material = selectedColor;
        
        anchorManager.MoveAnchorObject(gameObject);
    }    

    public void Release()
    {
        gameObject.GetComponent<Renderer>().material = originalColor;

        anchorManager.LockAnchorObject(gameObject);
    }

    private void Anchor_OnTrackingChanged(WorldAnchor self, bool located)
    {
        DebugWindow.DebugMessage((located ? "I found myself at " : "I am lost from ") + gameObject.transform.position.ToString());
        if (located)
        {
            gameObject.GetComponent<Renderer>().material = originalColor;
            GameObject objectInst = CreateOrUpdateObject(objectPrefab, this.gameObject.name + ".sphere");
        }
        else
            gameObject.GetComponent<Renderer>().material = lostColor;
    }

    public GameObject CreateOrUpdateObject(GameObject objectPrefab, string gameObjectName)
    {
        GameObject existing = GameObject.Find(gameObjectName);

        if (existing == null)
        {
            //instantiate it 0.5m in front of me (regardless of where the anchor is)
            GameObject objectInst = Instantiate(objectPrefab,initialObjectPosition, anchor.transform.rotation);
            objectInst.name = gameObjectName;
            objectInst.GetComponent<MoveObject>().anchor = this.gameObject;

            networkDiscoveryManager.BroadcastPosOnce(objectInst);

            return objectInst;
        }
        else
        {
            networkDiscoveryManager.BroadcastPosOnce(existing);
            return existing;
        }
    }


}
