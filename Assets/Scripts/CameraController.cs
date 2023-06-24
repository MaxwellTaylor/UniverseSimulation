using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniverseSimulation
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        private const int k_QueueLimit = 32;


        [SerializeField] private bool m_ShouldAutoOrbit = true;
        [SerializeField] private float m_AutoOrbitSpeed = 10f;
        [SerializeField] private float m_ZoomSpeed = 10f;
        [SerializeField] private Transform m_AutoOrbitTransform;
        [SerializeField][Range(0.9f, 1f)] private float m_LookAtSmoothing = 0.995f;


        private Quaternion m_AutoRotationAmount = Quaternion.identity;

        // A queue containing the point at the centre of all particles
        private static Queue<Vector3> s_LookAtQueue = new Queue<Vector3>();

        // A queue containing the average distance of particles from camera centre
        private static Queue<float> s_DistToCameraCentreQueue = new Queue<float>();

        private static Vector3 s_WorkingLookAtPosition;
        private static float s_WorkingZoomAmount;
        private static Vector3 s_WorkingPosition;
        private static Quaternion s_WorkingLookAtRotation;
        private static float s_PushTime = 0f;
        private static float s_PushTimePrev = 0f;
        private static float s_PushTimeDelta = 0f;


        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;

            if (m_ShouldAutoOrbit)
                m_AutoRotationAmount = Quaternion.Euler(0f, -m_AutoOrbitSpeed * Time.deltaTime, 0f);
        }

        private void UpdateZoom()
        {
            if (s_DistToCameraCentreQueue.Count == 0)
                return;

            // Average queue
            s_WorkingZoomAmount = 0f;
            foreach (var dist in s_DistToCameraCentreQueue)
                s_WorkingZoomAmount += dist;

            s_WorkingZoomAmount /= s_DistToCameraCentreQueue.Count;

            // Deduct an amount such that zoom behaviour is inverted when particles are too close to the centre
            s_WorkingZoomAmount -= 0.75f;
            s_WorkingZoomAmount = Mathf.SmoothStep(-5, 5, s_WorkingZoomAmount);

            s_WorkingZoomAmount *= m_ZoomSpeed * Time.deltaTime;
            s_WorkingPosition = transform.localPosition;
            s_WorkingPosition.z -= s_WorkingZoomAmount;
            transform.localPosition = s_WorkingPosition;
        }

        private void UpdateOrbit()
        {
            m_AutoOrbitTransform.rotation *= m_AutoRotationAmount;
        }

        private void UpdateLookAt()
        {
            if (s_LookAtQueue.Count == 0)
                return;

            // Average queue
            s_WorkingLookAtPosition = Vector3.zero;
            foreach (var position in s_LookAtQueue)
                s_WorkingLookAtPosition += position;

            s_WorkingLookAtPosition /= s_LookAtQueue.Count;
            s_WorkingLookAtRotation = Quaternion.LookRotation(s_WorkingLookAtPosition, Vector3.up);

            var alpha = (Time.time - s_PushTime) / Mathf.Max(s_PushTimeDelta, float.MinValue);
            transform.rotation = Quaternion.Lerp(transform.rotation, s_WorkingLookAtRotation, alpha * (1f - m_LookAtSmoothing));
        }

        private void Update()
        {
            UpdateZoom();
            UpdateOrbit();
            UpdateLookAt();
        }

        public static void Push(Vector3 lookAtPosition, float distToCameraCentre)
        {
            s_LookAtQueue.Enqueue(lookAtPosition);
            s_DistToCameraCentreQueue.Enqueue(distToCameraCentre);

            if (s_LookAtQueue.Count > k_QueueLimit)
                s_LookAtQueue.Dequeue();

            if (s_DistToCameraCentreQueue.Count > k_QueueLimit)
                s_DistToCameraCentreQueue.Dequeue();

            s_PushTimePrev = s_PushTime;
            s_PushTime = Time.time;
            s_PushTimeDelta = s_PushTime - s_PushTimePrev;
        }
    }
}