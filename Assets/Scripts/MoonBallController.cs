﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class MoonBallController : MonoBehaviour
{
    public float power;

    // Gravity well business
    public float gravCoeff;
    public Dictionary<string, Collider2D> planetArray;

    public Rigidbody2D rigidBody2D;
    public LineRenderer lineRenderer;
    public LineRenderer previewRenderer;
    public Vector3 startMousePosition;
    public bool captureMouseMovement;
    public bool canLaunch;
    public OrbitObject orbiting;
    public bool canGravity;
    public GameObject levelControllerObject;
    public GameObject scoreText;
    public string[] planetNames;
    private LevelController levelController;
    private bool launch_ball;
    public float velocity_coeff;

    // Preview Shot Code
    public Vector3[] previewLoci;
    private GameObject[] planets;
    private GameObject[] orbits;
    private Vector3 gravForce;
    private Vector3 launchForce;
    private bool resolve_gravity;

    // Start is called before the first frame update
    void Start()
    {
        // Check gravity well status
        planets = GameObject.FindGameObjectsWithTag("planet");
        orbits = GameObject.FindGameObjectsWithTag("orbitRing");
        planetArray = checkGravity(transform.position);
        levelController = levelControllerObject.GetComponent<LevelController>();
        rigidBody2D = GetComponent<Rigidbody2D>();
        lineRenderer = GetComponent<LineRenderer>();
        Material whiteDiffuseMat = new Material(Shader.Find("Unlit/Texture"));
        lineRenderer.material = whiteDiffuseMat;
        lineRenderer.material.color = Color.blue;
        previewRenderer = transform.GetChild(0).gameObject.GetComponent<LineRenderer>();
        previewRenderer.material = whiteDiffuseMat;
        previewRenderer.material.color = Color.white;
        orbiting = GetComponent<OrbitObject>();
        canLaunch = true;
        captureMouseMovement = false;
        canGravity = false;
        launch_ball = false;
        resolve_gravity = false;
        scoreText = GameObject.Find("GolfScore");
    }

    private void FixedUpdate()
    {
        // Add Gravity Force
        if (resolve_gravity)
        {
            rigidBody2D.AddForce(gravForce);
            resolve_gravity = false;
        }
        // Add Launch Force
        if (launch_ball)
        {
            rigidBody2D.velocity = (launchForce);
            launch_ball = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Handle panning camera with mouse click and drag.
        if (Input.GetMouseButtonDown(0) && canLaunch) {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mousePos2D = new Vector2(mousePos.x, mousePos.y);
            RaycastHit2D hit = Physics2D.Raycast(mousePos2D, Vector2.zero);
            if (hit.collider != null) {
                if (hit.collider.gameObject == gameObject) {
                    UnityEngine.Debug.Log("Player clicked on " + gameObject.name);
                    startMousePosition = transform.position;
                    captureMouseMovement = true;
                    // Update stroke count
                    levelController.UpdateLevelScore();

                    // Save current state so we can reload
                    print("TODO: Game state should save here.");
                    levelController.SaveState();
                }
            }
        }
        // Freeze while holding down mouseclick
        if (captureMouseMovement)
        {
            // Freeze all movements
            FreezeGame();

            // Free the moon from its current planet
            orbiting.enabled = false;
            canGravity = true;
            canLaunch = false; // Turning this off right now because its fun

            // Make current planet non-interactable
        }
        else
        {
            // Resume movement
            UnfreezeGame();
        }
        // Handle Pre-Shot
        if (Input.GetMouseButton(0) && captureMouseMovement) {
            Vector3 currentMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3 delta = startMousePosition - currentMousePos;
            launchForce = new Vector3((delta.x * power), (delta.y * power), 0);
            Vector3 velocity_shot = launchForce;
            
            // Back Line
            lineRenderer.SetPosition(0, new Vector3(startMousePosition.x, startMousePosition.y, 0.0f));
            lineRenderer.SetPosition(1, new Vector3(startMousePosition.x - delta.x, startMousePosition.y - delta.y, 0.0f));

            // Preview Line
            calculatePreviewLoci(velocity_shot);
            drawPreviewLine();
        }
        // Handle Shot
        if (Input.GetMouseButtonUp(0) && captureMouseMovement) {
            // Launch Moon Ball in FixedUpdate
            Vector3 currentMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector3 delta = startMousePosition - currentMousePos;
            launchForce = new Vector3((delta.x * power), (delta.y * power), 0);

            launch_ball = true;

            // Back Line
            lineRenderer.SetPosition(0, Vector3.zero);
            lineRenderer.SetPosition(1, Vector3.zero);

            // Preview Line
            erasePreviewLine();
            captureMouseMovement = false;

            // Update score
            uint score = System.Convert.ToUInt32(scoreText.GetComponent<UnityEngine.UI.Text>().text);
            score++;
            scoreText.GetComponent<UnityEngine.UI.Text>().text = score.ToString();
        }

        // Gravity Well Handling
        // Check if planet array is empty
        if (planetArray.Count == 0)
        {
            // Do nothing
        }
        // Else
        else if (canGravity == true)
        {
            Vector3 moon_pos = transform.position;
            // Set up total force vector
            gravForce = calculateGravForceTotal(moon_pos, planetArray);
            // Add total force
            resolve_gravity = true;
        }
        else
        {
            // do nothing
        }

    }

    void FreezeGame()
    {
        Time.timeScale = 0;
    }

    void UnfreezeGame()
    {
        Time.timeScale = 1;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.tag == "gravityWellEnd")
        {
            if (planetArray.ContainsKey(other.transform.parent.gameObject.name))
            {
                planetArray[other.transform.parent.gameObject.name] = other;
            }
            else
            {
                planetArray.Add(other.transform.parent.gameObject.name, other);
            }
        }
        else if(other.tag == "orbitRing")
        {
            // Stop moving
            rigidBody2D.velocity = new Vector2(0, 0);
            canGravity = false;
            canLaunch = true;

            // Orbit around new object
            orbiting.enabled = true;

            // TODO: Make based on collision position
            float radius = Vector3.Distance(other.transform.position, transform.position);
            float rotation = Vector3.Angle(other.transform.position - transform.position, rigidBody2D.velocity);
            print(rotation);
            orbiting.changeTargetBody(other.transform.parent.gameObject, rotation, radius);

            // Remove from gravity list
            planetArray.Remove(other.transform.parent.gameObject.name);
        }
        else
        {
            // do nothing
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        //print(other.transform.parent.gameObject.name);

        if (other.tag == "gravityWellStart")
        {
            // If planet already not in orbit
            if (!orbiting.enabled)
            {
                if (planetArray.ContainsKey(other.transform.parent.gameObject.name))
                {
                    planetArray[other.transform.parent.gameObject.name] = other;
                }
                else
                {
                    planetArray.Add(other.transform.parent.gameObject.name, other);
                }
                //print("Leaving " + other.transform.parent.gameObject.name + " well.");
            }
            // if in orbit
            else
            {
                // do nothing
            }
        }
        else if (other.tag == "gravityWellEnd")
        {
            planetArray.Remove(other.transform.parent.gameObject.name);
            //print("Leaving " + other.transform.parent.gameObject.name + " well.");
        }
        else if (other.tag == "orbitRing")
        {
            // do nothing
        }
        else if (other.tag == "boundary")
        {
            print("TODO: Implement out of bounds logic.");
            levelController.OutOfBounds();
            Start();
        }
        else
        {
            // do nothing

        }
    }

    public Dictionary<string, Collider2D> checkGravity(Vector3 moon_pos)
    {
        Dictionary<string, Collider2D> gravArray = new Dictionary<string, Collider2D>();
        string orbitingBodyName = orbiting.targetBody.name;

        foreach (GameObject planet in planets)
        {
            // TODO: Hardcoded here because it's faster than search for tag twice
            GameObject gravityWellStart = planet.transform.GetChild(0).gameObject;
            GameObject gravityWellEnd = planet.transform.GetChild(1).gameObject;
            Collider2D colliderStart = gravityWellStart.GetComponent<Collider2D>();
            Collider2D colliderEnd = gravityWellEnd.GetComponent<Collider2D>();
            string bodyName = gravityWellEnd.transform.parent.gameObject.name;
            // Bool to check if planet is in orbit
            bool is_orbiting_planet = bodyName == orbitingBodyName;
            // Bool to check if planet is in gravityWellStart
            bool in_gravityWellStart = colliderStart.bounds.Contains(moon_pos);
            // Bool to check if planet is in gravityWellEnd
            bool in_gravityWellEnd = colliderEnd.bounds.Contains(moon_pos);
            // Add if:
            // It's in gravityWellEnd
            // AND
            // If it's the planet we're orbiting -> only add if it's outside the gravityWellStart
            // Boolean Logic is: 
            // (is_orbiting_planet NAND in_gravityWellStart) AND in_gravityWellEnd
            if ((!is_orbiting_planet || !in_gravityWellStart) && in_gravityWellEnd)             {
                if (gravArray.ContainsKey(bodyName))
                {
                    gravArray[bodyName] = colliderEnd;
                }
                else
                {
                    gravArray.Add(bodyName, colliderEnd);
                }
            }
            // Else
            else
            {
                // do nothing
            }
        }
        return gravArray;
    }

    public void drawPreviewLine()
    {
        previewRenderer.loop = false;
        previewRenderer.positionCount = previewLoci.Length;

        for (int i = 0; i < previewLoci.Length; i++)
        {
            previewRenderer.SetPosition(i, previewLoci[i]);
        }
    }

    public void erasePreviewLine()
    {
        previewRenderer.loop = false;
        previewRenderer.positionCount = previewLoci.Length;

        for (int i = 0; i < previewLoci.Length; i++)
        {
            previewRenderer.SetPosition(i, Vector3.zero);
        }
    }

    // This function calculates the positions the preview line should have
    public void calculatePreviewLoci(Vector3 velocity_shot)
    {
        // 1. Set up time step variables for accuracy
        float range = 0.1f;
        int steps = 500;
        float step_size = range / steps;
        previewLoci = new Vector3[steps];
        Vector3 velocity_shot_real = velocity_shot * velocity_coeff; // TODO: Find out where I screwed up and missed a 10 multiplier
        //print(velocity_shot);
        Dictionary<string, Collider2D> previewPlanetArray;
        // 2. For each time_step in time_array
        // Variables and initial conditions
        Vector3 velocity_0 = velocity_shot_real;
        Vector3 velocity_k = velocity_0;
        Vector3 velocity_k1;
        Vector3 position_0 = transform.position;
        Vector3 position_k = position_0;
        Vector3 position_k1;
        previewLoci[0] = position_k;
        bool in_orbit = false;

        for (int i = 1; i < steps; i++)
        {
            previewPlanetArray = checkGravity(position_k);
            // i.   Calculate velocity based on differential equation and Euler's approximation            
            velocity_k1 = velocity_k + step_size * calculateGravForceTotal(position_k, previewPlanetArray);
            // ii.  Calculate position based on differential equation and Euler's approximation
            position_k1 = position_k + step_size * velocity_k;
            // If touch an orbit ring, call it
            foreach (GameObject orbit in orbits)
            {
                Collider2D orbit_collider = orbit.GetComponent<Collider2D>();
                if (orbit_collider.bounds.Contains(position_k))
                {
                    in_orbit = true;
                    break;
                }
            }
            if (in_orbit)
            {
                previewLoci[i] = position_k;
                continue;
            }
            // iii. Store result in previewLoci array
            previewLoci[i] = position_k1;
            // iv.  Save next iterations
            velocity_k = velocity_k1;
            position_k = position_k1;
            //            print(position_k);
        }
    }

    // This function calculates gravity between moon and something else
    public Vector3 calculateGravForcePlanet(Vector3 moon_pos, Vector3 planet_pos, float planet_mass)
    {
        // Calculate radius
        float forceRadius = Vector3.Distance(planet_pos, moon_pos);

        // Calculate direction between moon and current object
        Vector3 forceVector = planet_pos - moon_pos;
        Vector3 forceDirection = forceVector / forceRadius;

        //print(mass);

        return forceDirection* gravCoeff * planet_mass / Mathf.Pow(forceRadius, 2);
    }

    public Vector3 calculateGravForceTotal(Vector3 moon_pos, Dictionary<string, Collider2D> gravArray)
    {
        // Set up total force vector
        Vector3 finalForce = Vector3.zero;
        // for each planet
        foreach (KeyValuePair<string, Collider2D> entry in gravArray)
        {
            // Variables
            Vector3 planet_pos = entry.Value.transform.position;
            // Get mass from Astral Body Controller
            float planet_mass = entry.Value.transform.parent.gameObject.GetComponent<AstralBodyController>().mass;

            // Calculate Gravity from Planet
            Vector3 planetForce = calculateGravForcePlanet(moon_pos, planet_pos, planet_mass);

            // Calculate force to apply
            finalForce += planetForce;
        }
        return finalForce;
    }
}
