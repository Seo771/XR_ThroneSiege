using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class deactive_objects : MonoBehaviour {

    public GameObject[] obejcts;
    public float Delay_time;

	
	void OnTriggerEnter(Collider col) {
		if (col.tag == "Player") {

            StartCoroutine(Wait());

        }
	}

    IEnumerator Wait()
    {
        yield return new WaitForSeconds(Delay_time);
        foreach (GameObject obj in obejcts)
        {
            obj.SetActive(false);
        }
    }
}
