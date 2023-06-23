using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using System;

namespace UniverseSimulation
{
    public class UniverseRenderer : MonoBehaviour
    {
        private struct ParticleData
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Mass;
            public float Entropy;
        }

        private enum RenderTopology
        {
            Points,
            Lines,
        }

        private enum ParticleCount
        {
            _4096 = 4096,
            _8192 = 8192,
            _16384 = 16384,
            _32768 = 32768,
            _65536 = 65536,
            _131072 = 131072,
            _262144 = 262144,
        }

        [Header("Simulation Settings")]
        // Seed for pseudorandomness
        [SerializeField] private int m_Seed = 12;

        // Total number of particles in simulation
        [SerializeField] private ParticleCount m_ParticleCount = ParticleCount._32768;

        // Speed coefficient for simulation
        [SerializeField] private float m_SimulationSpeed = 1f;

        // The gravitational constant (G)
        [SerializeField] private double m_GravitationalConstant = 6.674e-11;

        // A coefficient applied to all units,
        // enabling extreme numbers to be represented within 32-bit floats
        [SerializeField] private double m_SimulationUnitScale = 1e-14;


        [Header("Particle Settings")]
        // Unit: Kilograms
        // The minimum mass of a particle upon initiation
        [SerializeField] private double m_MinMass = 1e27;

        // Unit: Kilograms
        // The maximum mass of a particle upon initiation
        [SerializeField] private double m_MaxMass = 1e31;

        // Unit: Meters
        // The diameter of the universe upon initiation
        [SerializeField] private double m_InitialSimulationScale = 1e6;

        // Unit: Meters/second
        // The linear velocity of the universe upon initiation
        [SerializeField] private double m_InitialSimulationLinearVelocity = 1e2;

        // Unit: Meters/second
        // The twist velocity of the universe upon initiation
        [SerializeField] private double m_InitialSimulationTwistVelocity = 1e2;

        // The probability distribution of particle mass
        [SerializeField]private float m_MassDistributionExp = 1;

        // The probability distribution of particle mass
        [SerializeField]private float m_VelocityDistributionExp = 1;

        // Coefficient to apply to distance
        [SerializeField] private float m_DistanceCoeff = 1;

        // Distance softening
        [SerializeField][Range(0, 1)] private float m_DistanceSoftening = 1;


        [Header("Rendering")]
        // Whether to render points or lines
        [SerializeField] private RenderTopology m_RenderTopology;

        // Camera to render from
        [SerializeField] private Camera m_TargetCamera;

        // Length of particle trails
        [SerializeField] private float m_TrailLength = 0.1f;


        [Header("References")]
        // The compute shader for calculating particle interactions
        [SerializeField] private ComputeShader m_ComputeShader;

        // The material for rendering particles
        [SerializeField] private Material m_Material;

        // Index used for ping pong'ing between buffers
        private int m_ReadIdx = 0;

        // Index used for ping pong'ing between buffers
        private int m_WriteIdx = 1;

        // Compute buffer for particle properties
        private ComputeBuffer[] m_ParticleBuffer;

        // Compute buffer for sending lines across to vertex shader
        private ComputeBuffer m_GeometryBuffer;

        // Compute buffer for tracking vertex count on GPU
        private ComputeBuffer m_DrawCallArgsBuffer;

        // The stride (bytes) of the vertex buffer
        private int m_GeometryBufferStride;

        // The number of vertices per particle instance
        private int m_VerticesPerInstance;

        // The kernel index to use in the compute shader
        private int m_KernelIdx;

        // Total number of particles in simulation
        private int m_InstanceCount;


        private void Start()
        {
            m_InstanceCount = (int)m_ParticleCount;

            switch(m_RenderTopology)
            {
                case RenderTopology.Points:
                    Shader.DisableKeyword("BUILD_LINES");
                    m_GeometryBufferStride = 24;
                    m_VerticesPerInstance = 1;
                    break;

                case RenderTopology.Lines:
                    Shader.EnableKeyword("BUILD_LINES");
                    m_GeometryBufferStride = 36;
                    m_VerticesPerInstance = 2;
                    break;
            }

            m_KernelIdx = m_ComputeShader.FindKernel("CSMain");

            ScaleParameters();
            SetupBuffers();
            SetupShaderProperties();

            Camera.onPostRender += OnPostRenderCallback;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawSphere(transform.position, 1f);
        }

        private void ScaleParameters()
        {
            m_MinMass *= m_SimulationUnitScale;
            m_MaxMass *= m_SimulationUnitScale;

            m_GravitationalConstant *= m_SimulationUnitScale;

            m_InitialSimulationScale *= m_SimulationUnitScale;
            m_InitialSimulationLinearVelocity *= m_SimulationUnitScale;
            m_InitialSimulationTwistVelocity *= m_SimulationUnitScale;
        }

        private void SetupBuffers()
        {
            UnityEngine.Random.InitState(m_Seed);

            m_ParticleBuffer = new ComputeBuffer[]
            {
                new ComputeBuffer(m_InstanceCount, 32),
                new ComputeBuffer(m_InstanceCount, 32),
            };

            var particles = new ParticleData[m_InstanceCount];
            for (var i = 0; i < m_InstanceCount; i++)
            {
                var direction = UnityEngine.Random.onUnitSphere;
                var massAlpha = Mathf.Pow(UnityEngine.Random.Range(0f, 1f), m_MassDistributionExp);
                var velocityAlpha = Mathf.Pow(UnityEngine.Random.Range(0f, 1f), m_VelocityDistributionExp);

                var linearVelocity = direction * velocityAlpha;
                var twistVelocity = Vector3.Cross(direction, Vector3.up) * velocityAlpha;

                particles[i].Position = transform.position + direction * (float)m_InitialSimulationScale;
                particles[i].Velocity = linearVelocity * (float)m_InitialSimulationLinearVelocity + twistVelocity * (float)m_InitialSimulationTwistVelocity;
                particles[i].Mass = Mathf.Lerp((float)m_MinMass, (float)m_MaxMass, massAlpha);
                particles[i].Entropy = UnityEngine.Random.Range(0f, 1f) * (1f - linearVelocity.magnitude);
            }

            m_ParticleBuffer[m_ReadIdx].SetData(particles);
            m_ParticleBuffer[m_WriteIdx].SetData(particles);

            var vertexCount = m_VerticesPerInstance * m_InstanceCount;
            m_GeometryBuffer = new ComputeBuffer(vertexCount, m_GeometryBufferStride, ComputeBufferType.Append);
            m_DrawCallArgsBuffer = new ComputeBuffer(1, 16, ComputeBufferType.IndirectArguments);
        }

        private void SetupShaderProperties()
        { 
            m_ComputeShader.SetFloat("_G", (float)m_GravitationalConstant);
            m_ComputeShader.SetFloat("_MaxMass", (float)m_MaxMass);
            m_ComputeShader.SetFloat("_DistanceSoftening", m_DistanceSoftening);
            m_ComputeShader.SetFloat("_DistanceCoeff", m_DistanceCoeff);
            m_ComputeShader.SetInt("_InstanceCount", m_InstanceCount);        

            m_ComputeShader.SetBuffer(m_KernelIdx, "_GeometryBuffer", m_GeometryBuffer);
            m_ComputeShader.SetBuffer(m_KernelIdx, "_DrawCallArgsBuffer", m_DrawCallArgsBuffer);

            m_Material.SetBuffer("_GeometryBuffer", m_GeometryBuffer);

            if (m_RenderTopology == RenderTopology.Lines)
                m_ComputeShader.SetFloat("_TrailLength", m_TrailLength);
        }

        private void PingPong()
        {
            m_ReadIdx = (m_ReadIdx + 1) % 2;
            m_WriteIdx = (m_WriteIdx + 1) % 2;
        }

        private void Update()
        {
            // 0: VerticesPerInstance
            // 1: InstanceCount
            // 2: StartVertexLocation
            // 3: StartInstanceLocation
            m_DrawCallArgsBuffer.SetData(new uint[]
            {
                (uint)m_VerticesPerInstance,
                0,
                0,
                0,
            });
            m_GeometryBuffer.SetCounterValue(0);

            PingPong();
            m_ComputeShader.SetFloat("_TimeStep", Time.deltaTime * m_SimulationSpeed);
            m_ComputeShader.SetMatrix("_VPMatrix", m_TargetCamera.projectionMatrix * m_TargetCamera.worldToCameraMatrix);
            m_ComputeShader.SetBuffer(m_KernelIdx, "_ParticleBufferRead", m_ParticleBuffer[m_ReadIdx]);
            m_ComputeShader.SetBuffer(m_KernelIdx, "_ParticleBufferWrite", m_ParticleBuffer[m_WriteIdx]);

            m_ComputeShader.Dispatch(m_KernelIdx, m_InstanceCount / 64, 1, 1);
        }

        private void OnPostRenderCallback(Camera cam)
        {
            if (cam != m_TargetCamera)
                return;

    #if UNITY_EDITOR
            //DebugLog();
    #endif

            m_Material.SetPass(0);

            if (m_RenderTopology == RenderTopology.Points)
                Graphics.DrawProceduralIndirectNow(MeshTopology.Points, m_DrawCallArgsBuffer, 0);
                
            else if (m_RenderTopology == RenderTopology.Lines)
                Graphics.DrawProceduralIndirectNow(MeshTopology.Lines, m_DrawCallArgsBuffer, 0);
        }

        private void OnDestroy()
        {
            m_ParticleBuffer[m_ReadIdx].Release();
            m_ParticleBuffer[m_WriteIdx].Release();
            m_GeometryBuffer.Release();
            m_DrawCallArgsBuffer.Release();

            Camera.onPostRender -= OnPostRenderCallback;
        }

    #if UNITY_EDITOR
        private void DebugLog()
        {
            var data = new ParticleData[m_InstanceCount];
            m_ParticleBuffer[m_ReadIdx].GetData(data);

            for (var i = 0; i < 1; i++)
            {
                Debug.Log("\t Position: " + data[i].Position.ToString("F10"));
                Debug.Log("\t Velocity: " + data[i].Velocity.ToString("F10"));
            }
        }
    #endif
    }
}