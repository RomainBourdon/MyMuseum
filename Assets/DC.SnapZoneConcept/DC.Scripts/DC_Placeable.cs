﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// This is a demo script:
/// Object follows mouse pointer
/// </summary>
public class DC_Placeable : MonoBehaviour
{
    [Header("Snap zone settings")]
    [Tooltip("The valid layers for this object, choose wall layers for objects that should only be placed on walls")]
    public LayerMask _ValidLayers;

    // The bounds are used to calculate how far an object needs adjusting for directional snap zones
    // It is also used to position the Edit Object GUI
    private Bounds m_EncapsulatedBounds;
    // This boolean determines whether the object is rotated to 90 degrees in Y or not based on which bound size is thinnest
    private bool m_DefaultToX = false;
    private float m_PushOutAmount = 0;
    private float m_CurrentLocalAngle = 0.0f;

    // The snap zone this object is currently placed on (if any)
    private DC_SnapZone m_SnapZone = null;

    // Only allow placement when this is true
    private bool m_BeingPlaced = true;

    // RD EXT: Wall Layer for detecting walls and limiting movement of object
    private int m_WallLayerID;
    // Invalid locations are where the object could not possibly fit in an area
    private bool m_InvalidLocation = false;
    // Breakdown of the above for X and Z
    // 0 not too small in +X or -X, 1 too small in +X or -X, 2 too small in +X and -X ergo invalid placement
    private int m_SpaceTooSmallX = 0;
    private int m_SpaceTooSmallZ = 0;
    private float m_XClamp = 0.0f;
    private float m_ZClamp = 0.0f;

    //probalbly make this a private with a getters and setters
    public Asset asset = null; 
    private DC_EditorCamera cam;

    private void Awake()
    {
        // Encapsulate all renderers bounds to get the size of the object
        m_EncapsulatedBounds = GetComponentInChildren<Renderer>().bounds;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            m_EncapsulatedBounds.Encapsulate(renderer.bounds);

        // If the object is thinner in X than it is in Z (Forward direction for directional snap zones)
        // Then the object will be rotated 90 degrees in Y axis by default when snapped to a directional zone
        if (m_EncapsulatedBounds.extents.x <= m_EncapsulatedBounds.extents.z)
        {
            m_DefaultToX = true;
            m_CurrentLocalAngle = 90.0f;
        }

        // RD EXT: Get wall layer ID
        m_WallLayerID = LayerMask.NameToLayer("Wall");

        cam =GameObject.Find("Editor Camera").GetComponent<DC_EditorCamera>();
        cam.placingSomething = true;
    }

    // For optimisation
    Ray m_Ray;
    RaycastHit[] m_Hits;
    RaycastHit m_Hit;
    private List<float> distances;

    //the trigger function saves the snapzone that the object is colliding with 
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<DC_SnapZone>())
        {
            m_BeingPlaced = false;
            cam.placingSomething = false;

            m_SnapZone = other.gameObject.GetComponent<DC_SnapZone>();
            m_SnapZone.SetValidity(false);

            this.GetComponent<Collider>().isTrigger = false;

        }
    }
    public bool GetPlacing()
    {
        return m_BeingPlaced;
    }
    public void placing(bool place)
    {
        m_BeingPlaced = place;
    }

    public GameObject GetSnapZoneGO()
    {
        return m_SnapZone.gameObject;
    }

    private void Update()
    {
        // Place object
        // RD EXT: Only if a valid placement
        if (Input.GetMouseButtonDown(0) && !m_InvalidLocation)
        {
            m_BeingPlaced = false;

            // If snapped to a zone, make it invalid for any future placements (This is reset if the object is subsequently moved)
            if (m_SnapZone)
            {
                m_SnapZone.SetValidity(false);
                cam.placingSomething = false;
            }
        }

        if (m_BeingPlaced)
        {
            // A ray from your mouse pointer to the world
            m_Ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            cam.placingSomething = true;

            // Get all the hits, and order them by distance
            m_Hits = Physics.RaycastAll(m_Ray, 100, _ValidLayers, QueryTriggerInteraction.Collide);// EDIT: no need to order, instead using if it has a Sphere Collider to take precedence.OrderBy(x => x.distance).ToArray();
            
            // Check all hit points
            foreach (RaycastHit hit in m_Hits)
            {
                // If the hit point had a sphere collider then it takes priority
                if (hit.collider is SphereCollider)
                {
                    // This is probably a snap zone, save for later
                    m_SnapZone = hit.transform.GetComponent<DC_SnapZone>();
                    if(m_SnapZone == null)
                    {
                        m_SnapZone = hit.transform.GetComponentInChildren<DC_SnapZone>();
                    }
                    
                    // If it is a snap zone make sure it's still valid (Hasn't already got something placed there)
                    if (m_SnapZone && m_SnapZone._IsValid)
                    {
                        // different layers require different rotations and positions
                        //plinths need to be placed on the transform of the snap zone whioch is a child of the display object
                        if (hit.transform.name.Contains("Plinth"))
                        {
                            transform.position = hit.transform.GetComponentInChildren<DC_SnapZone>().gameObject.transform.position;
                            break;
                        }
                        //for walls, we make sure the object has the same rotation as the snap zone and pushes out the object to prevent clipping 
                        else if (hit.transform.name.Contains("Wall"))
                        {
                            transform.rotation = hit.transform.rotation;
                            // Set the position to the snap zone + If X smaller push out in the direction of the snap zone by X extents, otherwise Z is used
                            transform.position = hit.transform.position + hit.transform.forward * (m_DefaultToX ? m_EncapsulatedBounds.extents.x : m_EncapsulatedBounds.extents.z);

                            //Testing
                            m_PushOutAmount = (m_DefaultToX ? m_EncapsulatedBounds.extents.x : m_EncapsulatedBounds.extents.z);
                            break;
                        }
                        //smiliar to the wall layer except this time we rotate 90 
                        else if (hit.transform.name.Contains("Directional"))
                        {
                            // Set the rotation to the snap zone + If X smaller than Z, rotate 90 degrees
                            
                            transform.rotation = hit.transform.rotation * Quaternion.AngleAxis(m_DefaultToX ? 90.0f : 0.0f, Vector3.up);

                            // Set the position to the snap zone + If X smaller push out in the direction of the snap zone by X extents, otherwise Z is used
                            transform.position = hit.transform.position + hit.transform.forward * (m_DefaultToX ? m_EncapsulatedBounds.extents.x : m_EncapsulatedBounds.extents.z);

                            //Testing
                            m_PushOutAmount = (m_DefaultToX ? m_EncapsulatedBounds.extents.x : m_EncapsulatedBounds.extents.z);
                            break;
                        }
                        // Otherwise just set the objects position (Normal floor snap point where direction doesn't matter)
                        else
                        {
                            transform.position = hit.transform.position;

                            break;
                        }
                    }
                }
                // Otherwise just move to where you are pointing on a valid surface
                else
                {
                    transform.position = hit.point;
                }

                // Clamp positioning using 4 rays from the objects current position, and off setting accordingly
                ClampPositioning4Rays();
            }
        }



    }

    /// <summary>
    /// RD EXT: Cast 4 more rays from the object in +/- X and Z
    /// These are used to detect Wall surfaces
    /// If the distance to any wall is smaller than the size of the object in X Z respecfully,
    /// then clamp the movment of the object in that direction
    /// </summary>
    private void ClampPositioning4Rays()
    {
        m_SpaceTooSmallX = 0;
        m_SpaceTooSmallZ = 0;
        m_InvalidLocation = false;

        // +X
        m_Ray = new Ray(transform.position, Vector3.right);
        if (Physics.Raycast(m_Ray, out m_Hit, m_DefaultToX ? m_EncapsulatedBounds.extents.x : m_EncapsulatedBounds.extents.z, 1 << m_WallLayerID, QueryTriggerInteraction.Collide))
        {
            // Because the ray is the exact length of the encapsulated renderers
            // If it got in here then it must have hit a wall, therefore limit the object motion in this direction
            m_SpaceTooSmallX++;

            // The new X position is where it hit the wall in X - half the size of the object in it's current orientation
            m_XClamp = m_Hit.point.x - (m_DefaultToX ? m_EncapsulatedBounds.extents.x : m_EncapsulatedBounds.extents.z);

            // Rotate the object to be as flat as possible against the surface
            transform.rotation = Quaternion.FromToRotation(Vector3.forward, m_Hit.normal) * Quaternion.AngleAxis(m_DefaultToX ? 90.0f : 0.0f, Vector3.up);
        }
        // -X
        m_Ray = new Ray(transform.position, Vector3.left);
        if (Physics.Raycast(m_Ray, out m_Hit, m_DefaultToX ? m_EncapsulatedBounds.extents.x : m_EncapsulatedBounds.extents.z, 1 << m_WallLayerID, QueryTriggerInteraction.Collide))
        {
            m_SpaceTooSmallX++;

            // The new X position is where it hit the wall in X + half the size of the object in it's current orientation
            m_XClamp = m_Hit.point.x + (m_DefaultToX ? m_EncapsulatedBounds.extents.x : m_EncapsulatedBounds.extents.z);

            // Rotate the object to be as flat as possible against the surface
            transform.rotation = Quaternion.FromToRotation(Vector3.forward, m_Hit.normal) * Quaternion.AngleAxis(m_DefaultToX ? 90.0f : 0.0f, Vector3.up);
        }
        // +Z
        m_Ray = new Ray(transform.position, Vector3.forward);
        if (Physics.Raycast(m_Ray, out m_Hit, m_DefaultToX ? m_EncapsulatedBounds.extents.z : m_EncapsulatedBounds.extents.x, 1 << m_WallLayerID, QueryTriggerInteraction.Collide))
        {
            m_SpaceTooSmallZ++;

            // The new Z position is where it hit the wall in Z - half the size of the object in it's current orientation
            m_ZClamp = m_Hit.point.z - (m_DefaultToX ? m_EncapsulatedBounds.extents.x : m_EncapsulatedBounds.extents.z);

            // Rotate the object to be as flat as possible against the surface
            // Edit: Using Back to avoid gimbol lock, then -90 : 180 instead of 90 : 0
            transform.rotation = Quaternion.FromToRotation(Vector3.back, m_Hit.normal) * Quaternion.AngleAxis(m_DefaultToX ? -90.0f : 180.0f, Vector3.up);
            transform.rotation.eulerAngles.Set(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, 0.0f);
        }
        // -Z
        m_Ray = new Ray(transform.position, Vector3.back);
        if (Physics.Raycast(m_Ray, out m_Hit, m_DefaultToX ? m_EncapsulatedBounds.extents.z : m_EncapsulatedBounds.extents.x, 1 << m_WallLayerID, QueryTriggerInteraction.Collide))
        {
            m_SpaceTooSmallZ++;

            // The new Z position is where it hit the wall in Z + half the size of the object in it's current orientation
            m_ZClamp = m_Hit.point.z + (m_DefaultToX ? m_EncapsulatedBounds.extents.x : m_EncapsulatedBounds.extents.z);

            // Rotate the object to be as flat as possible against the surface
            transform.rotation = Quaternion.FromToRotation(Vector3.forward, m_Hit.normal) * Quaternion.AngleAxis(m_DefaultToX ? 90.0f : 0.0f, Vector3.up);
        }

        // If the space is too small then we cannot place the object
        if (m_SpaceTooSmallX > 1 || m_SpaceTooSmallZ > 1)
            m_InvalidLocation = true;
        // Clamp the positioning if the gap between a wall and the obejct is smaller than the size of the object in that direction
        else
        {
            if (m_SpaceTooSmallX == 1)
            {
                transform.position = new Vector3(m_XClamp, transform.position.y, transform.position.z);

            }
            if (m_SpaceTooSmallZ == 1)
            {
                transform.position = new Vector3(transform.position.x, transform.position.y, m_ZClamp);
            }
        }
    }

    private void OnMouseDown()
    {
        if (cam.m_CurrentMode == DC_EditorCamera.CurrentMode.EDIT)
        {
            if (!m_BeingPlaced)
            {
                // Create a temporary encapsulated bounds (Not to be confused with the one created at the start)
                // This one is used to calculate it's current position and size on the screen for the GUI placement
                // Updating the old one would mean the encapsulated bound would no longer be local to the object
                // Update the encapsulated bounds to where the object is now

                //when the object is a plinth, we make sure there is no object on its snap zone before we allow the edit object to appear
                if (this.GetComponentInChildren<DC_SnapZone>())
                {
                    if (this.GetComponentInChildren<DC_SnapZone>()._IsValid == true)
                    { 
                        if (cam.placingSomething == false)
                        {
                            createEditUI();
                        }
                    }
                }
                else
                {
                    createEditUI();
                }
            }
        }
    }

    private void createEditUI()
    {
        Bounds tempBound = GetComponentInChildren<Renderer>().bounds;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            tempBound.Encapsulate(renderer.bounds);

        // Set the position and scale for the Edit Object toolbox
        DC_EditObject.Instance.Init(tempBound, this.gameObject);
    }

    /// <summary>
    /// Rotates object and if snapped to a directional point, changes how far away from the snap zone origin
    /// using the encapsulated bounds
    /// </summary>
    /// <param name="angle"></param>
    public void Rotate(float angle)
    {
        transform.rotation *= Quaternion.AngleAxis(angle, transform.up);

        m_CurrentLocalAngle += angle;

        // If currently snapped to a directional snap zone, switch which bounding box size to use for moving the object away from the snap point
        if (m_SnapZone && m_SnapZone._Directional)
        {
            // Because some objects are thinner in X than they are in Z, when the object was created we worked out which is thinnest
            // So here we know if 90 and 270 is classed as X or Z
            // EDIT: The push out amount is now caculated using the current angle and is a percentage of X and Z so angles don't have to be in 90 degree increments :)
            if (m_SnapZone.name != "Snap_Zone_Plinth")
            {
                transform.position = m_SnapZone.transform.position + m_SnapZone.transform.forward * CalculatePushOutAmount(m_CurrentLocalAngle);
            }
        }
    }

    public void Reposition()
    {
        m_BeingPlaced = true;

        // If was attached to a snap zone then make it a valid option again
        if (m_SnapZone)
        {
            m_SnapZone.SetValidity(true);
            m_SnapZone = null;
        }
    }

    public void DestroyObject()
    {
        // If was attached to a snap zone then make it a valid option again
        if (m_SnapZone)
        {
            m_SnapZone.SetValidity(true);
            m_SnapZone = null;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Based on the inputted angle, calculate the push out amount for directional snap zones
    /// Fully accurate for 0, 90, 180 and 270 etc
    /// Semi accurate for and angle in between, rather than doing expensive ellipsoid calculations)
    /// </summary>
    /// <param name="angle"></param>
    /// <returns></returns>
    private float CalculatePushOutAmount(float angle)
    {
        // If Defaulted to X then we know that 90 degrees is X extents (Use Sin())
        if (m_DefaultToX)
        {
            float percentage = Mathf.Sin(angle * Mathf.Deg2Rad);

            m_PushOutAmount = Mathf.Lerp(m_EncapsulatedBounds.extents.z, m_EncapsulatedBounds.extents.x, Mathf.Abs(percentage));

            // If the angle isn't divisible by 90, then add a little extra (To avoid clipping as much as possible without calculating expensive ellipsoids)
            if (angle % 90 != 0)
                m_PushOutAmount += new Vector2(m_EncapsulatedBounds.extents.x, m_EncapsulatedBounds.extents.z).magnitude * 0.5f;

            return m_PushOutAmount;
        }
        // Otherwise 0 degrees is X extents (Use Cos())
        else
        {
            float percentage = Mathf.Cos(angle * Mathf.Deg2Rad);

            m_PushOutAmount = Mathf.Lerp(m_EncapsulatedBounds.extents.x, m_EncapsulatedBounds.extents.z, Mathf.Abs(percentage));

            // If the angle isn't divisible by 90, then add a little extra
            // To avoid clipping as much as possible without calculating expensive ellipsoids or a load of pythagoras
            // If wanting it completely accurate: Maybe this will work...
            // 1. Use collision boxes (As these are rotated with the object, Bounds are not)
            // 2. Find the closest point (locally with regard to the snap point) and push out by any negative distance in Z
            if (angle % 90 != 0)
                m_PushOutAmount += new Vector2(m_EncapsulatedBounds.extents.x, m_EncapsulatedBounds.extents.z).magnitude * 0.5f;

            return m_PushOutAmount;
        }
    }
}
