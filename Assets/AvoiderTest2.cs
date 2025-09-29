using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Avoider;

[RequireComponent(typeof(NavMeshAgent))]
public class AvodierTest2 : MonoBehaviour
{
    public GameObject objectToAvoid;
    [Range(5f, 100f)] public float range;
    [Range(1f, 100f)] public float speed;
    public bool showGizmos = true;
    [Range(5f, 100f)] public float samplingRadius;
    [Range(2f, 10f)] public float pointRadius;

    private AvoiderCore avoiderCore;
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        avoiderCore = new AvoiderCore(gameObject, agent)
        {
            ObjectToAvoid = objectToAvoid,
            Range = range,
            Speed = speed,
            SamplingRadius = samplingRadius,
            PointRadius = pointRadius,
            ShowGizmos = showGizmos
        };
    }

    void Update()
    {
        if (avoiderCore != null)
        {
            avoiderCore.ObjectToAvoid = objectToAvoid;
            avoiderCore.Range = range;
            avoiderCore.Speed = speed;
            avoiderCore.SamplingRadius = samplingRadius;
            avoiderCore.PointRadius = pointRadius;
            avoiderCore.ShowGizmos = showGizmos;

            avoiderCore.Update();
        }
    }

    void OnDrawGizmos()
    {
        if (avoiderCore != null)
        {
            avoiderCore.DrawGizmos();
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(AvodierTest2))]
public class AvoiderComponentEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AvodierTest2 avoider = (AvodierTest2)target;

        if (avoider.GetComponent<NavMeshAgent>() == null)
            EditorGUILayout.HelpBox("GameObject needs a NavMeshAgent.", MessageType.Warning);
        if (avoider.objectToAvoid == null)
            EditorGUILayout.HelpBox("Assign a GameObject to avoid.", MessageType.Info);
        if (avoider.range <= 0)
            EditorGUILayout.HelpBox("Range must be greater than 0.", MessageType.Warning);
        if (avoider.speed <= 0)
            EditorGUILayout.HelpBox("Speed must be greater than 0.", MessageType.Warning);
    }
}
#endif


/*
 Plug-in Code

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Avoider
{
    public class AvoiderCore
    {
        public GameObject ObjectToAvoid { get; set; }

        public float Range { get; set; } = 5f;
        public float Speed { get; set; } = 3.5f;
        // Radius of the area around the owner to sample candidate hiding spots
        public float SamplingRadius { get; set; } = 10f;
        // Minimum distance between sampled points
        public float PointRadius { get; set; } = 2f;

        public bool ShowGizmos { get; set; } = true;

        private NavMeshAgent agent;
        private GameObject owner;

        // Exposed properties for drawing Gizmos
        public Vector3 CurrentTarget => currentTarget;
        public bool Moving => moving;
        public List<Vector3> VisiblePoints => visiblePoints;
        public List<Vector3> HiddenPoints => hiddenPoints;

        // Internal state
        private Vector3 currentTarget;
        private bool moving = false;
        private List<Vector3> candidates = new List<Vector3>();
        private List<Vector3> visiblePoints = new List<Vector3>();
        private List<Vector3> hiddenPoints = new List<Vector3>();

        public AvoiderCore(GameObject owner, NavMeshAgent agent)
        {
            this.owner = owner;
            this.agent = agent;

            if (agent == null)
            {
                Debug.LogError($"[AvoiderCore] Missing NavMeshAgent on {owner.name}!");
                return;
            }
        }

        public void Update()
        {
            if (ObjectToAvoid == null || agent == null) return;

            // Always look at the avoidee
            owner.transform.LookAt(ObjectToAvoid.transform.position);

            float distance = Vector3.Distance(owner.transform.position, ObjectToAvoid.transform.position);

            // Check if avoidee is in range and we're not already moving
            if (distance < Range && !moving && TheyCanSeeMe(owner.transform.position))
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

        /// Finds the best hiding spot based on Poisson disc sampling
        private void FindHidingSpot()
        {
            candidates.Clear();
            visiblePoints.Clear();
            hiddenPoints.Clear();

            // Create sampler around our current position
            var sampler = new PoissonDiscSampler(SamplingRadius, SamplingRadius, PointRadius);

            foreach (var point in sampler.Samples())
            {
                // Convert 2D point to 3D world space
                Vector3 worldPoint = owner.transform.position + new Vector3(point.x - SamplingRadius * 0.5f, 0, point.y - SamplingRadius * 0.5f);

                // Check if this point is not visible to the avoidee
                if (TheyCanSeeMe(worldPoint))
                {
                    visiblePoints.Add(worldPoint);
                }
                else
                {
                    // Check if the point is on NavMesh
                    if (PointInNavMesh(worldPoint))
                    {
                        hiddenPoints.Add(worldPoint);
                        candidates.Add(worldPoint);
                    }
                }
            }

            if (candidates.Count == 0) return;

            // Select the closest hiding spot as the best point
            Vector3 bestPoint = candidates[0];
            float bestDistance = Vector3.Distance(owner.transform.position, bestPoint);

            foreach (var point in candidates)
            {
                float dist = Vector3.Distance(owner.transform.position, point);
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
        private bool PointInNavMesh(Vector3 point, float maxDistance = 1f)
        {
            NavMeshHit hit;
            return NavMesh.SamplePosition(point, out hit, maxDistance, NavMesh.AllAreas);
        }

        // check if can be seen from said point 
        private bool TheyCanSeeMe(Vector3 point)
        {
            if (ObjectToAvoid == null) return false;

            Vector3 direction = point - ObjectToAvoid.transform.position;
            Ray ray = new Ray(ObjectToAvoid.transform.position, direction.normalized);
            RaycastHit hit;
            float maxDistance = direction.magnitude;

            // Use physics raycast to check for collision with other game objects 
            if (Physics.Raycast(ray, out hit, maxDistance))
            {
                // If the ray hits something other than the avoider or avoidee, the point is not visible
                return hit.collider.gameObject == owner || hit.collider.gameObject == ObjectToAvoid;
            }

            // If nothing was hit, the point is visible
            return true;
        }

        public void DrawGizmos()
        {
            if (!ShowGizmos || ObjectToAvoid == null) return;

            // Draw avoidance range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(owner.transform.position, Range);

            // Draw line to avoidee
            Gizmos.color = Color.red;
            Gizmos.DrawLine(owner.transform.position, ObjectToAvoid.transform.position);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(ObjectToAvoid.transform.position, 0.3f);


                // Draw current target
                if (Moving)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(owner.transform.position, currentTarget);
                    Gizmos.DrawSphere(currentTarget, 0.3f);
                }

                // Draw hidden points
                Gizmos.color = Color.green;
                foreach (var point in HiddenPoints)
                {
                    Gizmos.DrawWireSphere(point, 0.2f);
                    Gizmos.DrawLine(ObjectToAvoid.transform.position, point);
                }

                if (currentTarget != Vector3.zero)
                {
                    Gizmos.color = Color.purple;
                    Gizmos.DrawSphere(currentTarget, 0.25f);
                    Gizmos.DrawLine(owner.transform.position, currentTarget);
                }

                // Draw visible points
                Gizmos.color = Color.red;
                foreach (var point in VisiblePoints)
                {
                    Gizmos.DrawWireSphere(point, 0.15f);
                    Gizmos.DrawLine(ObjectToAvoid.transform.position, point);
                }

                // Draw sampling area
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(owner.transform.position, new Vector3(SamplingRadius, 0.1f, SamplingRadius));
     
        }
    }

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
}
 */