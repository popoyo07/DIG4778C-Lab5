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
