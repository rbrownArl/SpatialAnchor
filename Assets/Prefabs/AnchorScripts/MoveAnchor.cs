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

    public GameObject objectPrefab;

    public Vector3 initialObjectPosition = new Vector3(0f, 0f, 0.7f);

    private WorldAnchor anchor;

    private AnchorShareManager anchorShareManager;
    private NetworkDiscoveryManager networkDiscoveryManager;

    // Start is called before the first frame update
    void Start()
    {
        anchorShareManager = GameObject.Find("AnchorShareManager").GetComponent<AnchorShareManager>();
        networkDiscoveryManager = GameObject.Find("NetworkDiscoveryManager").GetComponent<NetworkDiscoveryManager>();

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

    //When Anchor is grabbed
    //change color... except it doesnt change on the anchor cuz it is made up up a bunch of different peieces
    //call the anchor managers moveanchor (remove worldanchor)
    public void Grab()
    {
        gameObject.GetComponent<Renderer>().material = selectedColor;
        
        anchorShareManager.MoveAnchorObject(gameObject);
    }    

    //when the anchor is released
    //change color back... except it dont work
    //call the anchor managers lock anchor (add worldanchor, save it, send it)
    public void Release()
    {
        gameObject.GetComponent<Renderer>().material = originalColor;

        anchorShareManager.LockAnchorObject(gameObject);

        Anchor_OnTrackingChanged(anchor, anchor.isLocated);
    }

    //we know an anchor exists, but we havent figured out where it is (havent mapped current location to anchor location)
    //set color based on lost or found
    //if found show the sphere object   (except dont do it right now)
    private void Anchor_OnTrackingChanged(WorldAnchor self, bool located)
    {
        DebugWindow.DebugMessage((located ? "I found myself at " : "I am lost from ") + gameObject.transform.position.ToString());
        if (located)
        {
            DebugWindow.DebugMessage("Located, so creating object");
            gameObject.GetComponent<Renderer>().material = originalColor;
            GameObject objectInst = CreateOrUpdateObject(objectPrefab, this.gameObject.name + ".sphere");
        }
        else
            gameObject.GetComponent<Renderer>().material = lostColor;
    }

    //create a sphere object if we havent already, broadcast the location
    public GameObject CreateOrUpdateObject(GameObject objectPrefab, string gameObjectName)
    {
        GameObject existing = GameObject.Find(gameObjectName);
        DebugWindow.DebugMessage(gameObjectName + " Existing:" + (existing != null));

        if (existing == null)
        {
            //instantiate it 0.5m in front of me (regardless of where the anchor is)
    DebugWindow.DebugMessage("preInst");
            GameObject objectInst = Instantiate(objectPrefab, initialObjectPosition + anchor.transform.position, anchor.transform.rotation);
            objectInst.name = gameObjectName;
            objectInst.GetComponent<MoveObject>().anchor = this.gameObject;
    DebugWindow.DebugMessage("postInst");
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
