using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public class Avoider : MonoBehaviour
{
    private  NavMeshAgent agent;
    public GameObject objectToAvoid;
    public float range;
    public float speed;
    public bool showGizmos;
    private Vector3 currentTarget;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        
    }

    // Update is called once per frame
    void Update()
    {
        if(objectToAvoid == null)
        {
            return;
        }
        transform.LookAt(objectToAvoid.transform.position);

        float distance = Vector3.Distance(transform.position, objectToAvoid.transform.position);

        if (distance < range)
        {
            FindASpot();
        }
    }

    // this does not work 
    void FindASpot()
    {
        List<Vector3> candidates = new List<Vector3>();
        var sampler = new PoissonDiscSampler(range, range, range / 2f);

        foreach (var point in sampler.Samples())
        {
            // Convert 2D sampler to 3D point in world space (XZ plane)
            Vector3 worldPoint = transform.position + new Vector3(point.x, 0, point.y);

            // Skip points too far from the avoider
            if (Vector3.Distance(transform.position, worldPoint) > range)
                continue;

            if (NavMesh.SamplePosition(worldPoint, out NavMeshHit hit, 1f, NavMesh.AllAreas)) 
            {
                worldPoint = hit.position;

                // Skip points too far
                if (Vector3.Distance(transform.position, worldPoint) > range) continue;

                // Check if the avoidee (player) has line of sight to this point
                if (!IsVisible(worldPoint, objectToAvoid.transform.position))
                {
                    candidates.Add(worldPoint);
                }
            }
           
        }

        if (candidates.Count == 0) return;

        // Pick closest valid hiding spot
        Vector3 bestPoint = candidates[0];
        float bestDist = Vector3.Distance(transform.position, bestPoint);

        foreach (var c in candidates)
        {
            float d = Vector3.Distance(transform.position, c);
            if (d < bestDist)
            {
                bestDist = d;
                bestPoint = c;
            }
        }

        currentTarget = bestPoint;
        agent.SetDestination(currentTarget);
    }

    bool IsVisible(Vector3 point, Vector3 origin)
    {
        Vector3 dir = point - origin;
        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dir.magnitude))
        {
            // If ray hits the avoider, point is visible
            if (hit.transform == transform) return true;
            // If ray hits something else before avoider, not visible
            return false;
        }
        return true;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Draw chosen target
        if (currentTarget != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(currentTarget, 0.3f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentTarget);

            if (objectToAvoid != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(objectToAvoid.transform.position, currentTarget);
            }
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
