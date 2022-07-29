using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using TMPro;

public class AnchorDisplay : MonoBehaviour
{
    public GameObject placard = null;

    public TextMeshPro txtName = null;

    // Start is called before the first frame update
    void Start()
    {
        txtName.text = gameObject.name;
    }

    // Update is called once per frame
    void Update()
    {
        placard.transform.LookAt(Camera.main.transform.position);
    }
}
