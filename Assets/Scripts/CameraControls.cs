using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniverseSimulation
{
    [RequireComponent(typeof(Camera))]
    public class CameraControls : MonoBehaviour
    {
        private Camera m_Camera;
        private float m_ZoomAmount = 0f;
        private Quaternion m_RotationAmount;
        private Quaternion m_AutoRotationAmount;

        [SerializeField] private bool m_AutoOrbit = true;
        [SerializeField] private float m_ZoomSpeed = 1f;
        [SerializeField] private float m_OrbitSpeed = 1f;
        [SerializeField] private float m_AutoOrbitSpeed = 1f;

        private void Start()
        {
            m_Camera = GetComponent<Camera>();

            if (m_AutoOrbit)
                m_AutoRotationAmount = Quaternion.Euler(0f, -m_AutoOrbitSpeed * Time.deltaTime, 0f);
        }

        private void Update()
        {
            Cursor.lockState = CursorLockMode.Locked;

            if (Input.GetKey(KeyCode.W))
                m_ZoomAmount += m_ZoomSpeed * Time.deltaTime;

            else if (Input.GetKey(KeyCode.S))
                m_ZoomAmount += -m_ZoomSpeed * Time.deltaTime;

            else
                m_ZoomAmount = 0f;

            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, transform.localPosition.z + m_ZoomAmount);

            if (Input.GetKey(KeyCode.A))
                m_RotationAmount = Quaternion.Euler(0f, m_OrbitSpeed * Time.deltaTime, 0f);

            else if (Input.GetKey(KeyCode.D))
                m_RotationAmount = Quaternion.Euler(0f, -m_OrbitSpeed * Time.deltaTime, 0f);

            else
                m_RotationAmount = Quaternion.Euler(0f, 0f, 0f);

            transform.parent.transform.rotation *= m_RotationAmount * m_AutoRotationAmount;
        }
    }
}