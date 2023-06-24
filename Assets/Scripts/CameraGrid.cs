using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UniverseSimulation
{
    [RequireComponent(typeof(Camera))]
    public class CameraGrid : MonoBehaviour
    {
        [SerializeField] private int m_Divisions = 128;
        [SerializeField] private float m_Scale = 2048f;
        [SerializeField] private Material m_Material;
        [SerializeField] private float m_Height = 100f;


        private List<Vector3> m_VertexList = new List<Vector3>();
        private List<int> m_IndexList = new List<int>();
        private Mesh m_Mesh;
        private CommandBuffer m_CommandBuffer;
        private CameraEvent m_CommandBufferEvent;
        private Camera m_Camera;


        private void Start()
        {
            m_Camera = GetComponent<Camera>();
            BuildVertices();

            m_CommandBuffer = new CommandBuffer()
            {
                name = "Grid",
            };

            m_Mesh = new Mesh()
            {
                name = "Grid",
                vertices = m_VertexList.ToArray(),
            };

            m_Mesh.SetIndices(m_IndexList.ToArray(), MeshTopology.Lines, 0, true);

            var propertyBlock = new MaterialPropertyBlock();

            var positionLower = new Vector3(0f, -m_Height, 0f);
            var matrixLower = Matrix4x4.TRS(positionLower, Quaternion.identity, Vector3.one);
            m_CommandBuffer.DrawMesh(m_Mesh, matrixLower, m_Material, 0, 0, propertyBlock);

            var positionUpper = new Vector3(0f, m_Height, 0f);
            var matrixUpper = Matrix4x4.TRS(positionUpper, Quaternion.identity, Vector3.one);
            m_CommandBuffer.DrawMesh(m_Mesh, matrixUpper, m_Material, 0, 0, propertyBlock);

            m_CommandBufferEvent = CameraEvent.AfterImageEffectsOpaque;
            m_Camera.AddCommandBuffer(m_CommandBufferEvent, m_CommandBuffer);
        }

        private void OnDestroy()
        {
            if (m_CommandBuffer != null)
            {
                m_Camera.RemoveCommandBuffer(m_CommandBufferEvent, m_CommandBuffer);
                m_CommandBuffer.Clear();           
            }
        }

        private void Update()
        {
            #if UNITY_EDITOR
                var half = m_Scale / 2f;

                var v0 = new Vector3(-half, 0f, -half);
                var v1 = new Vector3(-half, 0f, +half);
                var v2 = new Vector3(+half, 0f, +half);
                var v3 = new Vector3(+half, 0f, -half);

                Debug.DrawLine(v0, v1, Color.grey);
                Debug.DrawLine(v1, v2, Color.grey);
                Debug.DrawLine(v2, v3, Color.grey);
                Debug.DrawLine(v3, v0, Color.grey);
            #endif
        }

        private void BuildVertices()
        {
            var factor = m_Scale / m_Divisions;
            var halfScale = new Vector3(m_Scale / 2f, 0f, m_Scale / 2f);

            float increment;
            Vector3 v;

            for (int i = 0; i <= m_Divisions; i++)
            {
                increment = (i * factor);

                // Columns
                v = new Vector3(increment, 0f, 0f) - halfScale;
                m_VertexList.Add(v);
                m_IndexList.Add(i*4 + 0);

                v = new Vector3(increment, 0f, m_Scale) - halfScale;
                m_VertexList.Add(v);
                m_IndexList.Add(i*4 + 1);

                // Rows
                v = new Vector3(0f, 0f, increment) - halfScale;
                m_VertexList.Add(v);
                m_IndexList.Add(i*4 + 2);

                v = new Vector3(m_Scale, 0f, increment) - halfScale;
                m_VertexList.Add(v);
                m_IndexList.Add(i*4 + 3);
            }
        }
    }
}