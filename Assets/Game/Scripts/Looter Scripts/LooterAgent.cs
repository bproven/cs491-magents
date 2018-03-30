﻿using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Assets.Game.Scripts.Pickups;

public class LooterAgent : Agent
{
    // PLAYER SETTINGS, used for default values on agent reset
    public static float HP = 5, TIME = 30;    // Max health, level timer
    public static float DEX = 2;            // Movement speed
    public static float DEFLECTION = 0;       // without armor

    // Base stats
    public float mySpeed;               // Local Speed Stat
    public float myHealth;                // Local Health Stat
    public float myDamageDeflection;

    private static string[] thingsICanSee =
    {
        "Enemy",
        "Wall",
        "Gold"
    };  // What items is this agent allowed to recognize by tag?

    /// <summary>
    /// The Agent's Speed as modified by Items
    /// </summary>
    private float Speed { get; set; }

    /// <summary>
    /// The Agent's Health as modified by Items
    /// </summary>
    private float Health { get; set; }

    /// <summary>
    /// The Agent's damage deflection as modified by Items
    /// </summary>
    public float DamageDeflection { get; set; }

    // Set in scene
    public GameObject shooter;      // GameObject that the ArcherAgent script is attached to
    public GameObject levelManager; // GameObject that the LevelSpawner script is attached to

    public ArcherAgent Archer
    {
        get
        {
            return shooter.GetComponent<ArcherAgent>();
        }
    }

    // Vision specific variables
    public float sightDistance;
    public uint numRays;

    // Output debug
    public bool debug;
    public float myReward, x, y;    // Reward, output velocity components

    // Local timers
    private float roundStart;
    private float lastDamage;

    // list of items
    private List<Item> Items { get; } = new List<Item>();

    public Gold Gold
    {
        get
        {
            return Items.FirstOrDefault(i => i.GetType() == typeof(Gold)) as Gold;
        }
    }

    public int GoldValue
    {
        get
        {
            return Gold?.value ?? 0;
        }
        set
        {
            Gold gold = Gold;
            if (gold == null)
            {
                if (value > 0)
                {
                    gold = new Gold();
                    Items.Add(gold);
                }
            }
            else
            {
                if (value == 0)
                {
                    Items.Remove(gold);
                }
                else
                {
                    gold.value = value;
                }
            }
        }
    }

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
        state.Add(Speed != 0 ? dir.x / Speed : 0.0f);
        state.Add(Speed != 0 ? dir.y / Speed : 0.0f);

        // New loop
        for (int i = 0; i < numRays; i++)
        {
            RaycastHit2D hit = Physics2D.Raycast(gameObject.transform.position, Quaternion.AngleAxis((360f / numRays) * i, Vector3.forward) * transform.up, sightDistance);
            if (debug)
            {
                Color collisionColor = Color.white; // Default
                if (hit)
                {
                    if (hit.collider.tag == "Enemy") collisionColor = Color.red;
                    else if (hit.collider.tag == "Gold") collisionColor = Color.yellow;
                    else if (hit.collider.tag == "Wall") collisionColor = Color.blue;
                    else collisionColor = Color.magenta;
                    Debug.DrawLine(gameObject.transform.position, hit.point, collisionColor);
                }
                else
                    Debug.DrawLine(gameObject.transform.position, (gameObject.transform.position + (Quaternion.AngleAxis((360f / numRays) * i, Vector3.forward) * transform.up).normalized * sightDistance), collisionColor);
            }
            state.Add(sightDistance != 0 ? hit.distance / sightDistance : 0.0f);
            // Add information about what was hit
            foreach (string tag in thingsICanSee)
                state.Add(hit && hit.collider.tag == tag ? 1.0f : 0.0f);
        }
        // Could we attack if we needed to?
        float cd = (Time.time - shooter.GetComponent<ArcherAgent>().lastShot) / shooter.GetComponent<ArcherAgent>().myFireRate;
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
        // Move
        else if (brain.brainParameters.actionSpaceType == StateType.continuous)
            gameObject.transform.parent.GetComponent<Rigidbody2D>().velocity = new Vector2(Mathf.Clamp(act[0], -1, 1), Mathf.Clamp(act[1], -1, 1)) * Speed;

        // Proximity Rewards, make the earlier runs more distinct in score before the late game
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
                    reward -= 0.05f * (Mathf.Pow(1 - (rays[i].distance / sightDistance), 2)) / numRays;
            }
        }
        if (Time.time - roundStart > TIME)
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
        GoldValue = 0;
        transform.up = Vector2.up;
        levelManager.GetComponent<LevelSpawner>().resetLevel();
        lastDamage = Time.time - 0.5f;

        // PLAYER SETTINGS
        myHealth = HP;
        mySpeed = DEX;
        myDamageDeflection = DEFLECTION;

        Health = myHealth;
        Speed = mySpeed;

        Items.Clear();  // TODO: what about starting items?

        Archer.Reset();

        transform.parent.GetComponent<SpriteRenderer>().color = Color.green;

        transform.parent.position = levelManager.transform.GetChild(0).position;

    }

    /// <summary>
    /// Called to have the looter take some damage
    /// </summary>
    /// <param name="damage"></param>
    public void looterTakeDamage(float damage)
    {
        if (Time.time - lastDamage > 0.5f)
        {
            shooter.GetComponent<ArcherAgent>().reward -= 3;
            Debug.Log("Taking Damage!");
            Health -= Mathf.Max(0, damage - DamageDeflection);
            if (debug)
                transform.parent.GetComponent<SpriteRenderer>().color = Color.red;
            if (Health <= 0)
            {
                done = true;
                shooter.GetComponent<ArcherAgent>().done = true;
            }
            lastDamage = Time.time;
        }
    }

    /// <summary>
    /// Agent picks up an item and adds it to inventory
    /// </summary>
    /// <param name="item"></param>
    public void Pickup( Item item )
    {
        string name = item.name;
        if ( string.IsNullOrEmpty( name ) )
        {
            name = item.tag;
        }
        Debug.Log("Picked up " + name);
        reward += item.value;
        if ( item.IsCountable )
        {
            Item existingItem = Items.FirstOrDefault(i => i.GetType() == item.GetType());
            if ( existingItem == null )
            {
                existingItem = item;
                existingItem.Count = 1;
                Items.Add(existingItem);
            }
            else
            {
                existingItem.Count++;
            }
        }
        else
        {
            Items.Add(item);
            UpdateStats();
        }
    }

    /// <summary>
    /// Recalcs stats when an item is added or removed
    /// </summary>
    private void UpdateStats()
    {
        float speed = mySpeed;
        float health = myHealth;
        float damageDeflection = myDamageDeflection;
        float strength = Archer.myStrength;
        float range = Archer.myRange;
        foreach ( Item item in Items )
        {
            // add bonuses
            speed += (item.speedFactor - 1.0f) * mySpeed;            // factor is not cumulative, 2 20% items (1.2) adds 40%
            health += item.healthBonus;
            strength += item.damageBonus;
            damageDeflection = item.damageDeflection;
            range = item.rangeBonus;
        }
        Speed = speed;
        Health = health;
        DamageDeflection = damageDeflection;
        Archer.Strength = strength;
        Archer.Range = range;
    }

    /// <summary>
    /// If Reset On Done is not checked, this function will be called when the agent is done. 
    /// Reset() will only be called when the Academy resets.
    /// </summary>
    public override void AgentOnDone()
    {
    }

}