using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace UniverseSimulation
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        #region PUBLIC VARIABLES
        public float OrbitSpeed = 1f;
        public float ZoomSpeed = 5f;
        public float MoveSpeed = 1f;
        public float LookAtSpeed = 0.2f;
        public float CameraShake = 0.2f;
        #endregion

        #region PRIVATE VARIABLES
        private const int k_QueueLimit = 32;

        private GameObject m_Pivot;
        private float m_InitialZoom;
        private Vector3 m_PrevShake = Vector3.zero;

        private static Vector3 s_LookAtPosition = Vector3.zero;
        private static float s_Area = 0f;

        // A queue containing the point at the centre of all particles
        private static Queue<Vector3> s_LookAtQueue = new Queue<Vector3>();

        // A queue containing the average distance of particles from camera centre
        private static Queue<float> s_AreaQueue = new Queue<float>();
        #endregion
        
        #region MONOBEHAVIOUR
        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;

            m_Pivot = new GameObject();
            m_Pivot.name = "Camera Pivot";
            m_Pivot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            transform.SetParent(m_Pivot.transform);
            m_InitialZoom = transform.localPosition.z;
        }

        private void Update()
        {
            UpdateCameraShake();

            UpdateLookAt();
            UpdatePositionAndZoom();
            UpdateOrbit();
        }
        #endregion

        #region GENERAL
        public static void Push(Vector3 lookAtPosition, float area)
        {
            s_LookAtPosition = Common.UpdateFixedQueue<Vector3>(k_QueueLimit, lookAtPosition, ref s_LookAtQueue);
            s_Area = Common.UpdateFixedQueue<float>(k_QueueLimit, area, ref s_AreaQueue);
        }

        private void UpdatePositionAndZoom()
        {
            // Set pivot position
            var toPos = Vector3.Normalize(s_LookAtPosition - transform.position);
            toPos *= MoveSpeed * Time.deltaTime;
            m_Pivot.transform.position += toPos;

            // Deduct an amount to invert behaviour for small particle clouds
            var zoom = (s_Area - 0.05f) <= 0f ? -1f : 1f;

            zoom *= ZoomSpeed * Time.deltaTime;
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
            lookAtRotation = Quaternion.Lerp(Quaternion.identity, lookAtRotation, LookAtSpeed * Time.deltaTime);
            transform.rotation *= lookAtRotation;
            
            // Force no Z-axis rotation
            transform.rotation = Quaternion.Euler(transform.eulerAngles.x, transform.eulerAngles.y, 0f);
        }

        private void UpdateCameraShake()
        {
            if (CameraShake.Equals(0f))
                return;

            var shake = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(0f, CameraShake);
            shake = shake * 0.1f + m_PrevShake * 0.9f;

            s_LookAtPosition += shake;
            m_PrevShake = shake;
        }

        private void UpdateOrbit()
        {
            m_Pivot.transform.rotation *= Quaternion.Euler(0f, OrbitSpeed * Time.deltaTime, 0f);
        }
        #endregion
    }
}