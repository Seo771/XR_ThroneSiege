using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityStandardAssets.Characters.ThirdPerson;

[RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
[RequireComponent(typeof(ThirdPersonCharacter))]

public class patrol_box_third : MonoBehaviour {

	public Transform[] points;
    public float searcharea;
	private bool flag = true ;
	private int destPoint = 0;
	private NavMeshAgent agent;
    public float searchtime;
    private float timer;
    private Transform target;
    public ThirdPersonCharacter character { get; private set; }



    void Start () {
		agent = GetComponent<NavMeshAgent>();
        agent.autoBraking = true;
        character = GetComponent<ThirdPersonCharacter>();
        GotoNextPoint();
	}


	void GotoNextPoint() {
		if (points.Length == 0)
			return;

		agent.destination = points[destPoint].position;
        destPoint = (destPoint + 1) % points.Length;
        //character.Move(agent.desiredVelocity, false, false);
    }


	void FixedUpdate () {

        character.Move(agent.desiredVelocity, false, false);

        if (!flag)
        {
            timer += Time.deltaTime;
            agent.SetDestination(target.position);
           

        }

        if (timer > searchtime)
        {
            flag = true;
            timer = 0.0f;
            
        }

        Vector3 forward = transform.TransformDirection(Vector3.forward) ;
        RaycastHit hit;
       if (Physics.BoxCast(transform.position, transform.lossyScale /2, transform.forward, out hit, transform.rotation, searcharea)) {
            Debug.DrawLine(transform.position, forward*searcharea, Color.red, 5.0f);
            if (hit.collider.tag == "Player") {
				flag = false;
                target = hit.collider.gameObject.transform;
				agent.SetDestination (target.position);
                
			} 

            
		} 

		if (agent.remainingDistance < agent.stoppingDistance && flag) {		
			GotoNextPoint ();
            
		}
	
			
	}
         	
}