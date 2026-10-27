using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class scene_portal : MonoBehaviour {

	public string scene_text;
    
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	void OnTriggerEnter(Collider col) {

        if (col.gameObject.tag == "Player")
        {

            SceneManager.LoadScene(scene_text);

        }
		
	}

}
