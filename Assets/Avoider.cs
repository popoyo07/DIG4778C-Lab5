using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Avoider : MonoBehaviour
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
    [CustomEditor(typeof(Avoider))]
public class AvoiderEditor : Editor
{

    public override void OnInspectorGUI()
    {
       
        Avoider avoider = (Avoider)target;

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

public class PoissonDiscSampler
{
    private const int k = 30;  // Maximum number of attempts before marking a sample as inactive.

    private readonly Rect rect;
    private readonly float radius2;  // radius squared
    private readonly float cellSize;
    private Vector2[,] grid;
    private List<Vector2> activeSamples = new List<Vector2>();

    /// Create a sampler with the following parameters:
    ///
    /// width:  each sample's x coordinate will be between [0, width]
    /// height: each sample's y coordinate will be between [0, height]
    /// radius: each sample will be at least `radius` units away from any other sample, and at most 2 * `radius`.
    public PoissonDiscSampler(float width, float height, float radius)
    {
        rect = new Rect(0, 0, width, height);
        radius2 = radius * radius;
        cellSize = radius / Mathf.Sqrt(2);
        grid = new Vector2[Mathf.CeilToInt(width / cellSize),
                           Mathf.CeilToInt(height / cellSize)];
    }

    /// Return a lazy sequence of samples. You typically want to call this in a foreach loop, like so:
    ///   foreach (Vector2 sample in sampler.Samples()) { ... }
    public IEnumerable<Vector2> Samples()
    {
        // First sample is choosen randomly
        yield return AddSample(new Vector2(Random.value * rect.width, Random.value * rect.height));

        while (activeSamples.Count > 0)
        {

            // Pick a random active sample
            int i = (int)Random.value * activeSamples.Count;
            Vector2 sample = activeSamples[i];

            // Try `k` random candidates between [radius, 2 * radius] from that sample.
            bool found = false;
            for (int j = 0; j < k; ++j)
            {

                float angle = 2 * Mathf.PI * Random.value;
                float r = Mathf.Sqrt(Random.value * 3 * radius2 + radius2); // See: http://stackoverflow.com/questions/9048095/create-random-number-within-an-annulus/9048443#9048443
                Vector2 candidate = sample + r * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                // Accept candidates if it's inside the rect and farther than 2 * radius to any existing sample.
                if (rect.Contains(candidate) && IsFarEnough(candidate))
                {
                    found = true;
                    yield return AddSample(candidate);
                    break;
                }
            }

            // If we couldn't find a valid candidate after k attempts, remove this sample from the active samples queue
            if (!found)
            {
                activeSamples[i] = activeSamples[activeSamples.Count - 1];
                activeSamples.RemoveAt(activeSamples.Count - 1);
            }
        }
    }

    private bool IsFarEnough(Vector2 sample)
    {
        GridPos pos = new GridPos(sample, cellSize);

        int xmin = Mathf.Max(pos.x - 2, 0);
        int ymin = Mathf.Max(pos.y - 2, 0);
        int xmax = Mathf.Min(pos.x + 2, grid.GetLength(0) - 1);
        int ymax = Mathf.Min(pos.y + 2, grid.GetLength(1) - 1);

        for (int y = ymin; y <= ymax; y++)
        {
            for (int x = xmin; x <= xmax; x++)
            {
                Vector2 s = grid[x, y];
                if (s != Vector2.zero)
                {
                    Vector2 d = s - sample;
                    if (d.x * d.x + d.y * d.y < radius2) return false;
                }
            }
        }

        return true;

        // Note: we use the zero vector to denote an unfilled cell in the grid. This means that if we were
        // to randomly pick (0, 0) as a sample, it would be ignored for the purposes of proximity-testing
        // and we might end up with another sample too close from (0, 0). This is a very minor issue.
    }

    /// Adds the sample to the active samples queue and the grid before returning it
    private Vector2 AddSample(Vector2 sample)
    {
        activeSamples.Add(sample);
        GridPos pos = new GridPos(sample, cellSize);
        grid[pos.x, pos.y] = sample;
        return sample;
    }

    /// Helper struct to calculate the x and y indices of a sample in the grid
    private struct GridPos
    {
        public int x;
        public int y;

        public GridPos(Vector2 sample, float cellSize)
        {
            x = (int)(sample.x / cellSize);
            y = (int)(sample.y / cellSize);
        }
    }
}
