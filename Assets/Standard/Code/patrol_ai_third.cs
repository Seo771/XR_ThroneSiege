using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityStandardAssets.Characters.ThirdPerson;


[RequireComponent(typeof(ThirdPersonCharacter))]

public class patrol_ai_third : MonoBehaviour {

    public Transform[] points;
    private int destPoint = 0;
    private NavMeshAgent agent;
    public ThirdPersonCharacter character { get; private set; }


    // Use this for initialization
    void Start () {
        agent = GetComponent<NavMeshAgent>();
        character = GetComponent<ThirdPersonCharacter>();
        agent.autoBraking = true;
         GotoNextPoint();
    }

    void GotoNextPoint()
    {
       if (points.Length == 0)
            return;

        agent.destination = points[destPoint].position;
        destPoint = (destPoint + 1) % points.Length;
      ;
    }

    // Update is called once per frame
    void Update () {
        character.Move(agent.desiredVelocity, false, false);

        if (!agent.pathPending && agent.remainingDistance < agent.stoppingDistance) { 
            GotoNextPoint();
    }
}
}
