﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LooterAgent : Agent
{
    public GameObject shooter;
    public GameObject OGLevel;
    public int roundTime;
    private float roundStart;
    public float maxSpeed;

    private Vector3[] transforms;

    public float sightDistance;

    public uint numRays;
    public bool debug;

    public float x, y;
    public float myReward;
    public float turnSpeed;

    public GameObject levelManager;
    /// <summary>
    /// Use this method to initialize your agent. This method is called when the agent is created. 
    /// Do not use Awake(), Start() or OnEnable().
    /// </summary>
    public override void InitializeAgent()
    {
        base.InitializeAgent();
    }

    /// <summary>
    /// Must return a list of floats corresponding to the state the agent is in. If the state space type is discrete, 
    /// return a list of length 1 containing the float equivalent of your state.
    /// </summary>
    /// <returns></returns>
    public override List<float> CollectState()
    {
        List<float> state = new List<float>();
        // New input, trying to add some kind of memory for confusing situations
        // Hey, past me, where did you want to go again?
        Vector2 dir = transform.parent.GetComponent<Rigidbody2D>().velocity;
        state.Add(dir.x / maxSpeed);
        state.Add(dir.y / maxSpeed);

        // What can I see?
        RaycastHit2D[] rays = new RaycastHit2D[numRays];
        // New loop
        for (int i = 0; i < rays.Length; i++)
        {
            rays[i] = Physics2D.Raycast(gameObject.transform.position, Quaternion.AngleAxis((360f / rays.Length) * i, Vector3.forward) * transform.up, sightDistance);
            // Debug
            if (debug)
                if (rays[i])
                    Debug.DrawLine(gameObject.transform.position, rays[i].point, rays[i].collider.tag == "Gold" ? Color.green : Color.blue);
                else
                    Debug.DrawLine(gameObject.transform.position, (gameObject.transform.position + (Quaternion.AngleAxis((360f / rays.Length) * i, Vector3.forward) * transform.up).normalized * sightDistance));
        }
        for (int i = 0; i < rays.Length; i++)
        {
            state.Add(rays[i].distance / sightDistance);
            // Add information about what was hit
            if (rays[i])
            {
                // Is it a Wall? Gold? Enemy?
                state.Add(rays[i].collider.tag == "Enemy" ? 1.0f : 0.0f);
                state.Add(rays[i].collider.tag == "Wall" ? 1.0f : 0.0f);
                state.Add(rays[i].collider.tag == "Gold" ? 1.0f : 0.0f);
            }
            else
            {
                state.Add(0.0f);
                state.Add(0.0f);
                state.Add(0.0f);
            }
        }
        // Could we attack if we needed to?
        float cd = (Time.time - shooter.GetComponent<ArcherAgent>().lastShot) / shooter.GetComponent<ArcherAgent>().shotDelay;
        state.Add(cd >= 1 ? 1 : 0);
        return state;
    }
    
    /// <summary>
    /// This function will be called every frame, you must define what your agent will do given the input actions. 
    /// You must also specify the rewards and whether or not the agent is done. To do so, modify the public fields of the agent reward and done.
    /// </summary>
    /// <param name="act"></param>
    public override void AgentStep(float[] act)
    {
        if (brain.brainParameters.actionSpaceType == StateType.discrete)
        {

        }
        else if (brain.brainParameters.actionSpaceType == StateType.continuous)
        {
            // Turn-based controls
            //transform.Rotate(new Vector3(0, 0, act[0] * turnSpeed));
            //gameObject.transform.parent.GetComponent<Rigidbody2D>().velocity = transform.up * maxSpeed;
            // WASD movement
            gameObject.transform.parent.GetComponent<Rigidbody2D>().velocity = new Vector2(Mathf.Clamp(act[0], -1, 1), Mathf.Clamp(act[1], -1, 1)) * maxSpeed;
        }
        // Proximity Rewards
        RaycastHit2D[] rays = new RaycastHit2D[numRays];
        for (int i = 0; i < rays.Length; i++)
            rays[i] = Physics2D.Raycast(gameObject.transform.position, Quaternion.AngleAxis((360f / rays.Length) * i, Vector3.forward) * transform.up, sightDistance);
        for (int i = 0; i < rays.Length; i++)
        {
            // Add information about what was hit
            if (rays[i])
            {
                // Reward greed
                if (rays[i].collider.tag == "Gold")
                    reward += 0.1f * (1 - (rays[i].distance / sightDistance)) / numRays;
                // Reward risk
                if (rays[i].collider.tag == "Enemy")
                    reward += 0.1f * (1 - (rays[i].distance / sightDistance)) / numRays;
                // Quit it with the wall hugging
                if (rays[i].collider.tag == "Wall")
                    reward -= 0.5f * (Mathf.Pow(1 - (rays[i].distance / sightDistance), 2)) / numRays;
            }
        }
        if (Time.time - roundStart > roundTime)
        {
            Debug.Log("Looter: Out of time.");
            done = true;
        }

        // Debug
        myReward = CumulativeReward;
    }

    /// <summary>
    /// This function is called at start, when the Academy resets and when the agent is done (if Reset On Done is checked).
    /// </summary>
    public override void AgentReset()
    {
        roundStart = Time.time;
        gameObject.transform.parent.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        transform.parent.GetComponent<Gold>().value = 0;
        transform.up = Vector2.up;
        levelManager.GetComponent<LevelSpawner>().resetLevel();

        transform.parent.position = levelManager.transform.GetChild(0).position;
    }

    /// <summary>
    /// If Reset On Done is not checked, this function will be called when the agent is done. 
    /// Reset() will only be called when the Academy resets.
    /// </summary>
    public override void AgentOnDone()
    {

    }

}