using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Avoider;

[RequireComponent(typeof(NavMeshAgent))]
public class AvoiderTest : MonoBehaviour
{
    private NavMeshAgent agent;
    public GameObject objectToAvoid;
    [Range(5f,100f)] public float range = 5f;
    [Range(1f, 100f)] public float speed = 3.5f;
    public bool showGizmos = true;
    [Range(5f, 100f)] public float samplingRadius = 10f;
    [Range(2f, 10f)] public float pointRadius = 2f;

    private Vector3 currentTarget;
    bool moving = false;
    private List<Vector3> candiadates = new List<Vector3>();
    private List<Vector3> visiblePoints = new List<Vector3>();
    private List<Vector3> hiddenPoints = new List<Vector3>();
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("Missing the NavMeshAgent "); 
        }
    }


    void Update()
    {
        if (objectToAvoid == null || agent == null) return;

        // Always look at player 
        transform.LookAt(objectToAvoid.transform.position);

        float distance = Vector3.Distance(transform.position, objectToAvoid.transform.position);

        // Check if avoidee is in range and we're not already moving
        if (distance < range && !moving && theyCanSeeMe(transform.position))
        {
            moving = true;
            FindHidingSpot();
        }

        // Check if we've reached our destination
        if (moving && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            moving = false;
        }
    }

    void FindHidingSpot()
    {
        candiadates.Clear();

        // Create sampler around our current position
        var sampler = new PoissonDiscSampler(samplingRadius, samplingRadius, pointRadius);

        foreach (var point in sampler.Samples())
        {
            // Convert 2D point to 3D world space 
            Vector3 worldPoint = transform.position + new Vector3(point.x - samplingRadius * 0.5f, 0, point.y - samplingRadius * 0.5f);

            // Check if this point is not visible to the avoidee
            if (!theyCanSeeMe(worldPoint))
            {
                // Check if the point is on NavMesh
                if (pointInNavMesh(worldPoint))
                {
                    candiadates.Add(worldPoint);
                }
            }

        }
        Vector3 bestPoint = candiadates[0];
        float bestDistance = Vector3.Distance(transform.position, bestPoint);
        foreach(var point in candiadates)
        {
            float dist = Vector3.Distance(transform.position, point);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestPoint = point;
            }
        }

        currentTarget = bestPoint;
        agent.SetDestination(currentTarget);

    }

    // set to true if point is in NavMesh
    bool pointInNavMesh(Vector3 point, float maxDistance = 1f)
    {
        NavMeshHit hit;
        return NavMesh.SamplePosition(point, out hit, maxDistance, NavMesh.AllAreas);
    }

    // check if can be seen from said point 
    bool theyCanSeeMe(Vector3 point)
    {
        if (objectToAvoid == null) return false;

        Vector3 directionToPoint = point - objectToAvoid.transform.position;
        Ray ray = new Ray(objectToAvoid.transform.position, directionToPoint.normalized);
        RaycastHit hit;

        float maxDistance = directionToPoint.magnitude;

        // Use physics raycast to check for collision with other game objects 
        if (Physics.Raycast(ray, out hit, maxDistance))
        {
            // If the ray hits something other than the avoider or avoidee, the point is not visible
            return hit.collider.gameObject == gameObject || hit.collider.gameObject == objectToAvoid;
        }

        // If nothing was hit, the point is visible
        return true;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Draw avoidance range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);

        // Draw line of sight to avoidee
        if (objectToAvoid != null)
        {
            bool isVisible = theyCanSeeMe(transform.position);
            // change set color depending if it is visible or not 
            Gizmos.color = isVisible ? Color.red : Color.green;
            Gizmos.DrawLine(transform.position, objectToAvoid.transform.position);


            // Draw small sphere at avoidee position for clarity
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(objectToAvoid.transform.position, 0.3f);
        }
        // Draw current target if moving
        if (moving)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, currentTarget);
            Gizmos.DrawSphere(currentTarget, 0.3f);
        }

        // Draw visible points (points that can be seen by avoidee)
        Gizmos.color = Color.red;
        foreach (var point in visiblePoints)
        {
            Gizmos.DrawWireSphere(point, 0.15f);
            if (objectToAvoid != null)
            {
                Gizmos.DrawLine(objectToAvoid.transform.position, point);
            }
        }

        // Draw hidden points (valid hiding spots)
        Gizmos.color = Color.green;
        foreach (var point in hiddenPoints)
        {
            Gizmos.DrawWireSphere(point, 0.2f);
            if (objectToAvoid != null)
            {
                // Draw dashed line to show these are hidden
                DrawDashedLine(objectToAvoid.transform.position, point, 0.5f);
            }
        }

        // Draw sampling area
        Gizmos.color = new Color(1, 1, 0, 0.1f);
        Gizmos.DrawWireCube(transform.position, new Vector3(samplingRadius, 0.1f, samplingRadius));
    }

    void DrawDashedLine(Vector3 start, Vector3 end, float dashLength)
    {
        Vector3 direction = (end - start).normalized;
        float distance = Vector3.Distance(start, end);
        int segments = Mathf.RoundToInt(distance / dashLength);

        for (int i = 0; i < segments; i += 2)
        {
            Vector3 segmentStart = start + direction * (i * dashLength);
            Vector3 segmentEnd = start + direction * ((i + 1) * dashLength);

            // Clamp the end point to not exceed the total distance
            if (Vector3.Distance(start, segmentEnd) > distance)
                segmentEnd = end;

            Gizmos.DrawLine(segmentStart, segmentEnd);
        }
    }
}

#if UNITY_EDITOR
    [CustomEditor(typeof(AvoiderTest))]
public class AvoiderEditor : Editor
{

    public override void OnInspectorGUI()
    {

        AvoiderTest avoider = (AvoiderTest)target;

        DrawDefaultInspector();

        if (avoider.GetComponent<NavMeshAgent>() == null) // will warn you if your game object does not have a navmesh
        {
            EditorGUILayout.HelpBox("GameObject needs to have a  ", MessageType.Warning);
        }
        if (avoider.objectToAvoid == null) 
        {
            EditorGUILayout.HelpBox("Assign a game object to avoid ", MessageType.Info);
        }
        if (avoider.range <= 0) 
        { 
            EditorGUILayout.HelpBox("Range cannot be less than or euqal to 0 ", MessageType.Warning);
        }
        if (avoider.speed <= 0) 
        { 
            EditorGUILayout.HelpBox("Speed cannot be less than or euqal to 0 ", MessageType.Warning);
        }

    }
}
#endif