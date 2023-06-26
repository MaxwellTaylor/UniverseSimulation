using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UniverseSimulation
{
    [RequireComponent(typeof(Camera))]
    public class CameraGrid : MonoBehaviour
    {
        #region PUBLIC VARIABLES
        public float Scale = 8192f;
        public int Divisions = 128;
        public float Height = 8000f;
        #endregion

        #region PRIVATE VARIABLES
        private Mesh m_Mesh;
        private Material m_Material;
        private Camera m_Camera;
        private CommandBuffer m_CommandBuffer;
        private CameraEvent m_CommandBufferEvent;
        private List<Vector3> m_VertexList = new List<Vector3>();
        private List<int> m_IndexList = new List<int>();
        #endregion

        #region MONOBEHAVIOUR
        private void Start()
        {
            m_Camera = GetComponent<Camera>();

            SetupMaterial();
            BuildVertices();
            SubmitCommandBuffer();
        }

        private void Update()
        {
            #if UNITY_EDITOR
                var half = Scale / 2f;

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

        private void OnDestroy()
        {
            if (m_CommandBuffer != null)
            {
                m_Camera.RemoveCommandBuffer(m_CommandBufferEvent, m_CommandBuffer);
                m_CommandBuffer.Clear();           
            }

            #if UNITY_EDITOR
                if (!Application.isPlaying && m_Material != null)
                {
                    DestroyImmediate(m_Material);
                    m_Material = null;
                }
                else
            #endif
                {
                    Destroy(m_Material);
                    m_Material = null;
                }
        }
        #endregion

        #region GENERAL
        private void SetupMaterial()
        {
            if (m_Material == null)
            {
                m_Material = new Material(Common.ParticleShader)
                {
                    name = "GridMaterial",
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            m_Material.SetColor(Common.k_MaterialPropColour, Color.grey);
            m_Material.SetColor(Common.k_MaterialPropAmbient, Color.black);
            m_Material.SetFloat(Common.k_MaterialPropExposure, 1f);

            m_Material.SetFloat(Common.k_MaterialPropZTest, (float)UnityEngine.Rendering.CompareFunction.Always);
            m_Material.SetFloat(Common.k_MaterialPropCullMode, (float)UnityEngine.Rendering.CullMode.Off);
            m_Material.SetFloat(Common.k_MaterialPropBlendModeSrc, (float)UnityEngine.Rendering.BlendMode.One);
            m_Material.SetFloat(Common.k_MaterialPropBlendModeDst, (float)UnityEngine.Rendering.BlendMode.One);
            m_Material.SetFloat(Common.k_MaterialPropZWrite, false ? 1f : 0f);

            m_Material.DisableKeyword(Common.k_KeywordUseGeometryData);
        }

        private void SubmitCommandBuffer()
        {
            var propertyBlock = new MaterialPropertyBlock();

            m_Mesh = new Mesh();
            m_Mesh.vertices = m_VertexList.ToArray();
            m_Mesh.SetIndices(m_IndexList.ToArray(), MeshTopology.Lines, 0, true);

            m_CommandBuffer = new CommandBuffer()
            {
                name = "Grid",
            };

            // Lower grid
            var positionLower = new Vector3(0f, -Height, 0f);
            var matrixLower = Matrix4x4.TRS(positionLower, Quaternion.identity, Vector3.one);
            m_CommandBuffer.DrawMesh(m_Mesh, matrixLower, m_Material, 0, 0, propertyBlock);

            // Upper grid
            var positionUpper = new Vector3(0f, Height, 0f);
            var matrixUpper = Matrix4x4.TRS(positionUpper, Quaternion.identity, Vector3.one);
            m_CommandBuffer.DrawMesh(m_Mesh, matrixUpper, m_Material, 0, 0, propertyBlock);

            // Submit
            m_CommandBufferEvent = CameraEvent.AfterImageEffectsOpaque;
            m_Camera.AddCommandBuffer(m_CommandBufferEvent, m_CommandBuffer);
        }

        private void BuildVertices()
        {
            var factor = Scale / Divisions;
            var halfScale = new Vector3(Scale / 2f, 0f, Scale / 2f);

            float increment;
            Vector3 v;

            for (int i = 0; i <= Divisions; i++)
            {
                increment = (i * factor);

                // Columns
                v = new Vector3(increment, 0f, 0f) - halfScale;
                m_VertexList.Add(v);
                m_IndexList.Add(i*4 + 0);

                v = new Vector3(increment, 0f, Scale) - halfScale;
                m_VertexList.Add(v);
                m_IndexList.Add(i*4 + 1);

                // Rows
                v = new Vector3(0f, 0f, increment) - halfScale;
                m_VertexList.Add(v);
                m_IndexList.Add(i*4 + 2);

                v = new Vector3(Scale, 0f, increment) - halfScale;
                m_VertexList.Add(v);
                m_IndexList.Add(i*4 + 3);
            }
        }
        #endregion
    }
}