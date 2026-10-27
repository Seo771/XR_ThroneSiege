using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Trigger_Teleport : MonoBehaviour {

    public Transform m_Destination;
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
        {
            other.transform.position = m_Destination.position;
        }
    }
}
