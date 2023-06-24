using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniverseSimulation
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        private const int k_QueueLimit = 8;


        [SerializeField] private float m_ZoomSpeed = 10f;
        [SerializeField] private float m_LookAtSpeed = 1f;
        [SerializeField] private float m_MoveSpeed = 1f;

        
        private GameObject m_Pivot;
        private float m_InitialZoom;


        // A queue containing the point at the centre of all particles
        private static Queue<Vector3> s_LookAtQueue = new Queue<Vector3>();

        // A queue containing the average distance of particles from camera centre
        private static Queue<float> s_AreaQueue = new Queue<float>();


        private static Vector3 s_LookAtPosition = Vector3.zero;
        private static float s_Area = 0f;
        

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;

            m_Pivot = new GameObject();
            m_Pivot.name = "Camera Pivot";
            m_Pivot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            transform.SetParent(m_Pivot.transform);
            m_InitialZoom = transform.localPosition.z;
        }

        private void UpdatePositionAndZoom()
        {
            // Set pivot position
            var toPos = Vector3.Normalize(s_LookAtPosition - transform.position);
            toPos *= m_MoveSpeed * Time.deltaTime;
            //m_Pivot.transform.position += toPos;

            // Deduct an amount to invert behaviour for small particle clouds
            var zoom = (s_Area - 0.05f) <= 0f ? -1f : 1f;

            zoom *= m_ZoomSpeed * Time.deltaTime;
            var position = transform.localPosition;

            position.z = position.z - zoom;
            transform.localPosition = position;
        }

        private void UpdateLookAt()
        {
            var toParticles = Vector3.Normalize(s_LookAtPosition - transform.position);
            var lookAtRotation = Quaternion.identity;

            if (!s_LookAtPosition.Equals(Vector3.zero))
                lookAtRotation = Quaternion.FromToRotation(transform.forward, toParticles);

            // Normalise to unit value
            lookAtRotation = Quaternion.Normalize(lookAtRotation);

            // Linearly map Quaternion magnitude to m_LookAtSpeed and deltaTime
            // This can be thought of as scaling
            lookAtRotation = Quaternion.Lerp(Quaternion.identity, lookAtRotation, m_LookAtSpeed * Time.deltaTime);
            transform.rotation *= lookAtRotation;
        }

        private void Update()
        {
            UpdatePositionAndZoom();
            //UpdateLookAt();
        }

        public static void Push(Vector3 lookAtPosition, float area)
        {
            s_LookAtQueue.Enqueue(lookAtPosition);
            s_AreaQueue.Enqueue(area);

            if (s_LookAtQueue.Count > k_QueueLimit)
                s_LookAtQueue.Dequeue();

            if (s_AreaQueue.Count > k_QueueLimit)
                s_AreaQueue.Dequeue();

            if (s_LookAtQueue.Count > 0)
            {
                // Average queue
                s_LookAtPosition = Vector3.zero;
                foreach (var position in s_LookAtQueue)
                    s_LookAtPosition += position;

                s_LookAtPosition /= s_LookAtQueue.Count;
            }

            if (s_AreaQueue.Count > 0)
            {
                // Average queue
                s_Area = 0f;
                foreach (var dist in s_AreaQueue)
                    s_Area += dist;

                s_Area /= s_AreaQueue.Count;
            }
        }
    }
}