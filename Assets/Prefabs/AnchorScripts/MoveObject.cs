using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveObject : MonoBehaviour
{
    public bool grabbing;
    public Material selectedColor;
    public Material originalColor;
    public Material lostColor;

    public GameObject anchor;

    private NetworkDiscoveryManager networkDiscoveryManager;

    // Start is called before the first frame update
    // load the sphere's location from player prefs, broadcast the position
    void Start()
    {
DebugWindow.DebugMessage("Start start: " + gameObject.name);
        networkDiscoveryManager = GameObject.Find("NetworkDiscoveryManager").GetComponent<NetworkDiscoveryManager>();

        //read position
        LoadObject();
DebugWindow.DebugMessage("Loading end");
        //transmit position
        networkDiscoveryManager.BroadcastPosOnce(gameObject);
DebugWindow.DebugMessage("Start end");

    }

    // Update is called once per frame
    void Update()
    {
        if (grabbing)
        {
            // networkDiscoveryManager.BroadcastPosOnce(gameObject);
        }
    }

    //grab the object, change the color
    public void Grab()
    {
        grabbing = true;
        gameObject.GetComponent<Renderer>().material = selectedColor;

    }

    //release the object, change the color, broadcast the new position, write the position to the playerprefs..
    public void Release()
    {
        grabbing = false;
        DebugWindow.DebugMessage("release object stat: " + (gameObject != null));

        gameObject.GetComponent<Renderer>().material = originalColor;

        //transmit position
        networkDiscoveryManager.BroadcastPosOnce(gameObject);

        //save position
        SaveObject();
        DebugWindow.DebugMessage("release object end");

    }

    //in theory, if another player moves the sphere, it should tell us
    public void Moved(Vector3 newPosition, Quaternion newRotation)
    {
        SetPositionRelativeToAnchor(newPosition, newRotation);

        //transmit position
        networkDiscoveryManager.BroadcastPosOnce(gameObject);

        SaveObject();
    }

    //set position based on the anchor position and orientation
    private void SetPositionRelativeToAnchor(Vector3 devicePosRelativeToAsa, Quaternion deviceRotRelativeToAsa)
    {
        Quaternion asaRotRelativeToWorld = anchor.transform.rotation;
        Quaternion deviceRotRelativeToWorld = deviceRotRelativeToAsa * asaRotRelativeToWorld;

        Vector3 devicePosRelativeToWorld = anchor.transform.position +
                                           asaRotRelativeToWorld * devicePosRelativeToAsa;

        gameObject.transform.rotation = deviceRotRelativeToWorld;
        gameObject.transform.position = devicePosRelativeToWorld;

        DebugWindow.DebugMessage(anchor.transform.position + "");
        DebugWindow.DebugMessage(gameObject.transform.position + " : " + devicePosRelativeToAsa);
    }

    //get position based on anchor position and orientation
    private Vector3 GetPositionRelativeToAnchor()
    {
        Vector3 devicePosRelativeToAsa = gameObject.transform.position - anchor.transform.position;
        return devicePosRelativeToAsa;
    }

    //get rotation based on anchor rotation  (it's a sphere... so kind of silly)
    private Quaternion GetRotationRelativeToAnchor()
    {
        Quaternion asaRotRelativeToWorld = anchor.transform.rotation;
        Quaternion deviceRotRelativeToWorld = gameObject.transform.rotation;

        Quaternion deviceRotRelativeToAsa = deviceRotRelativeToWorld * Quaternion.Inverse(asaRotRelativeToWorld);

        return deviceRotRelativeToAsa;        
    }

    //read sphere position and rotation from playerprefs, update the game object
    private void LoadObject()
    {
        DebugWindow.DebugMessage("Loading Prefs: " + anchor.name + ": " + PlayerPrefs.HasKey(anchor.name + ".px"));

        if (PlayerPrefs.HasKey(anchor.name + ".px"))
        {
            DebugWindow.DebugMessage("Loaded Prefs");

            Color color = new Color();
            color.r = float.Parse(PlayerPrefs.GetString(anchor.name + ".cr"));
            color.g = float.Parse(PlayerPrefs.GetString(anchor.name + ".cg"));
            color.b = float.Parse(PlayerPrefs.GetString(anchor.name + ".cb"));

            gameObject.GetComponent<Renderer>().material.color = color;


            Vector3 position;
            position.x = float.Parse( PlayerPrefs.GetString(anchor.name + ".px") );
            position.y = float.Parse( PlayerPrefs.GetString(anchor.name + ".py") );
            position.z = float.Parse( PlayerPrefs.GetString(anchor.name + ".pz") );

            Quaternion rotation;
            rotation.w = float.Parse( PlayerPrefs.GetString(anchor.name + ".ow") );
            rotation.x = float.Parse( PlayerPrefs.GetString(anchor.name + ".ox") );
            rotation.y = float.Parse( PlayerPrefs.GetString(anchor.name + ".oy") );
            rotation.z = float.Parse( PlayerPrefs.GetString(anchor.name + ".oz") );

            SetPositionRelativeToAnchor(position, rotation);

        }
        else
        {
            DebugWindow.DebugMessage("No Prefs");
            SaveObject();
        }
    }

    //get position and rotation relative to the anchor, write to player prefs
    private void SaveObject()
    {
        DebugWindow.DebugMessage("Saving Prefs: " + anchor.name + ": " + PlayerPrefs.HasKey(anchor.name + ".px"));

        Color color = gameObject.GetComponent<Renderer>().material.color;

        PlayerPrefs.SetString(anchor.name + ".cx", color.r.ToString());
        PlayerPrefs.SetString(anchor.name + ".cy", color.g.ToString());
        PlayerPrefs.SetString(anchor.name + ".cz", color.b.ToString());

        Vector3 position = GetPositionRelativeToAnchor();

        PlayerPrefs.SetString(anchor.name + ".px", position.x.ToString());
        PlayerPrefs.SetString(anchor.name + ".py", position.y.ToString());
        PlayerPrefs.SetString(anchor.name + ".pz", position.z.ToString());

        Quaternion rotation = GetRotationRelativeToAnchor();

        PlayerPrefs.SetString(anchor.name + ".ow", rotation.w.ToString());
        PlayerPrefs.SetString(anchor.name + ".ox", rotation.x.ToString());
        PlayerPrefs.SetString(anchor.name + ".oy", rotation.y.ToString());
        PlayerPrefs.SetString(anchor.name + ".oz", rotation.z.ToString());

        DebugWindow.DebugMessage(anchor.transform.position + "");
        DebugWindow.DebugMessage(transform.position + ":" + position);
    }
}
