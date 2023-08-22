using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveObject : MonoBehaviour
{
    public Material selectedColor;
    public Material originalColor;
    public Material lostColor;

    public GameObject anchor;

    private NetworkDiscoveryManager networkDiscoveryManager;

    // Start is called before the first frame update
    void Start()
    {
        networkDiscoveryManager = GameObject.Find("NetworkDiscoverManager").GetComponent<NetworkDiscoveryManager>();

        //read position
        LoadObject();

        //transmit position
        networkDiscoveryManager.BroadcastPosOnce(gameObject);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Grab()
    {
        gameObject.GetComponent<Renderer>().material = selectedColor;

    }

    public void Release()
    {
        gameObject.GetComponent<Renderer>().material = originalColor;

        //transmit position
        networkDiscoveryManager.BroadcastPosOnce(gameObject);

        //save position
        SaveObject();
    }

    public void Moved(Vector3 newPosition, Quaternion newRotation)
    {
        SetPositionRelativeToAnchor(newPosition, newRotation);

        //transmit position
        networkDiscoveryManager.BroadcastPosOnce(gameObject);

        SaveObject();
    }

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

    private Vector3 GetPositionRelativeToAnchor()
    {
        Vector3 devicePosRelativeToAsa = gameObject.transform.position - anchor.transform.position;
        return devicePosRelativeToAsa;
    }

    private Quaternion GetRotationRelativeToAnchor()
    {
        Quaternion asaRotRelativeToWorld = anchor.transform.rotation;
        Quaternion deviceRotRelativeToWorld = gameObject.transform.rotation;

        Quaternion deviceRotRelativeToAsa = deviceRotRelativeToWorld * Quaternion.Inverse(asaRotRelativeToWorld);

        return deviceRotRelativeToAsa;        
    }

    private void LoadObject()
    {
        DebugWindow.DebugMessage("Loading Prefs: " + anchor.name + ": " + PlayerPrefs.HasKey(anchor.name + ".x"));

        if (PlayerPrefs.HasKey(anchor.name + ".x"))
        {
            DebugWindow.DebugMessage("Loaded Prefs");

            Vector3 position;
            position.x = float.Parse( PlayerPrefs.GetString(anchor.name + ".x") );
            position.y = float.Parse( PlayerPrefs.GetString(anchor.name + ".y") );
            position.z = float.Parse( PlayerPrefs.GetString(anchor.name + ".z") );

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

    private void SaveObject()
    {
        DebugWindow.DebugMessage("Saving Prefs: " + anchor.name + ": " + PlayerPrefs.HasKey(anchor.name + ".x"));

        Vector3 position = GetPositionRelativeToAnchor();

        PlayerPrefs.SetString(anchor.name + ".x", position.x.ToString());
        PlayerPrefs.SetString(anchor.name + ".y", position.y.ToString());
        PlayerPrefs.SetString(anchor.name + ".z", position.z.ToString());

        Quaternion rotation = GetRotationRelativeToAnchor();

        PlayerPrefs.SetString(anchor.name + ".ow", rotation.w.ToString());
        PlayerPrefs.SetString(anchor.name + ".ox", rotation.x.ToString());
        PlayerPrefs.SetString(anchor.name + ".oy", rotation.y.ToString());
        PlayerPrefs.SetString(anchor.name + ".oz", rotation.z.ToString());

        DebugWindow.DebugMessage(anchor.transform.position + "");
        DebugWindow.DebugMessage(transform.position + ":" + position);
    }
}
