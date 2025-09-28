using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Avoider;

[RequireComponent(typeof(NavMeshAgent))]
public class AvodierTest2 : MonoBehaviour
{
    public GameObject objectToAvoid;
    [Range(5f, 100f)] public float range = 5f;
    [Range(1f, 100f)] public float speed = 3.5f;
    public bool showGizmos = true;
    [Range(5f, 100f)] public float samplingRadius = 10f;
    [Range(2f, 10f)] public float pointRadius = 2f;

    private AvoiderCore avoiderCore;
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.LogError("Missing NavMeshAgent!");
            return;
        }

        avoiderCore = new AvoiderCore(gameObject, agent)
        {
            ObjectToAvoid = objectToAvoid,
            Range = range,
            Speed = speed,
            SamplingRadius = samplingRadius,
            PointRadius = pointRadius
        };
    }

    void Update()
    {
        if (avoiderCore != null)
        {
            avoiderCore.ObjectToAvoid = objectToAvoid;
            avoiderCore.Update();
        }
    }

    void OnDrawGizmos()
    {
        if (!showGizmos || objectToAvoid == null) return;

        // Draw avoidance range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);

        // Draw line to avoidee
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, objectToAvoid.transform.position);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(objectToAvoid.transform.position, 0.3f);

        if (avoiderCore != null)
        {
            // Draw current target
            if (avoiderCore.Moving)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, avoiderCore.CurrentTarget);
                Gizmos.DrawSphere(avoiderCore.CurrentTarget, 0.3f);
            }

            // Draw hidden points
            Gizmos.color = Color.green;
            foreach (var point in avoiderCore.HiddenPoints)
            {
                Gizmos.DrawWireSphere(point, 0.2f);
                Gizmos.DrawLine(objectToAvoid.transform.position, point);
            }

            if (avoiderCore.CurrentTarget != Vector3.zero)
            {
                Gizmos.color = Color.purple;
                Gizmos.DrawSphere(avoiderCore.CurrentTarget, 0.25f);
                Gizmos.DrawLine(transform.position, avoiderCore.CurrentTarget);
            }

            // Draw visible points
            Gizmos.color = Color.red;
            foreach (var point in avoiderCore.VisiblePoints)
            {
                Gizmos.DrawWireSphere(point, 0.15f);
                Gizmos.DrawLine(objectToAvoid.transform.position, point);
            }

            // Draw sampling area
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, new Vector3(samplingRadius, 0.1f, samplingRadius));
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
