#nullable enable

using UnityEngine;

namespace TurningPage.Sample
{
    public class PinchPoint : MonoBehaviour
    {
        [SerializeField] private TurningPageSimulation turningPageSimulation = default!;
        [SerializeField] private float maxPinchDistance = 0.02f;

        private bool initialzied = false;

        private void Update()
        {
            if (!initialzied)
            {
                if (turningPageSimulation.TrySearchNearestVertex(transform.position, out var result)
                    && result.Distance < maxPinchDistance)
                {
                    turningPageSimulation.SetPinchPoint(result.VertexId);
                    initialzied = true;
                }
                else
                {
                    Debug.LogWarning("Failed to initialize PinchPoint: No nearby vertex found within the maximum pinch distance.");
                    enabled = false;
                }
            }

            turningPageSimulation.UpdatePinchData(transform.position, transform.right, transform.forward);            
        }

        private void OnDisable()
        {
            turningPageSimulation.Release();
            initialzied = false;
        }
    }
}
