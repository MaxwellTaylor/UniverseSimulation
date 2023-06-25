using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
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

        private enum InitShape
        {
            Sphere,
            AccretionDisc,
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
        // The diameter of galaxies upon initiation
        [SerializeField] private double m_InitialClusterScaleMin = 1e6;

        // Unit: Meters
        // The diameter of galaxies upon initiation
        [SerializeField] private double m_InitialClusterScaleMax = 1e6;

        // Unit: Meters/second
        // The linear velocity of the universe upon initiation
        [SerializeField] private double m_InitialSimulationLinearVelocity = 1e2;

        // Unit: Meters/second
        // The disc velocity of the universe upon initiation
        [SerializeField] private double m_InitialSimulationDiscVelocity = 1e2;

        // Cluster cluster count
        [SerializeField]private int m_ClusterCount = 4;

        // Cluster distribution radius
        [SerializeField]private int m_ClusterDistributionMaxRadius = 50;

        // The probability distribution of particle mass
        [SerializeField]private float m_MassDistributionExp = 1f;

        // The probability distribution of particle mass
        [SerializeField]private float m_VelocityDistributionExp = 1f;

        // Coefficient to apply to distance
        [SerializeField] private float m_DistanceCoeff = 1f;

        // Distance softening
        [SerializeField][Range(0f, 1f)] private float m_DistanceSoftening = 1f;

        // Velocity decay
        [SerializeField] private float m_VelocityDecay = 0.1f;

        // Initialisation shape
        [SerializeField] private InitShape m_InitialisationShape = InitShape.Sphere;


        [Header("Rendering")]
        // Whether to render points or lines
        [SerializeField] private RenderTopology m_RenderTopology;

        // Camera to render from
        [SerializeField] private CameraController m_TargetCameraController;

        // Length of particle trails
        [SerializeField] private float m_TrailLength = 0.1f;

        // Proportion of particles to sample on CPU for establishing camera tracking behaviour
        [SerializeField] private float m_SampleRatio = 0.05f;

        // Colour variant for particles
        [SerializeField][ColorUsage(false, true)] private Color m_ColourA = Color.white;

        // Colour variant for particles
        [SerializeField][ColorUsage(false, true)] private Color m_ColourB = Color.white;


        [Header("References")]
        // The compute shader for calculating particle interactions
        [SerializeField] private ComputeShader m_ComputeShader;

        // The material for rendering particles
        [SerializeField] private Material m_Material;


        // The target Camera, established by m_TargetCameraController
        private Camera m_TargetCamera;

        // Whether the renderer is initialised
        private bool m_IsInitialised = false;

        // Index used for ping pong'ing between buffers
        private int m_ReadIdx = 0;

        // Index used for ping pong'ing between buffers
        private int m_WriteIdx = 1;

        // Compute buffer for particle properties
        private ComputeBuffer[] m_ParticleBuffer;

        // Compute buffer for sending lines across to vertex shader
        private ComputeBuffer m_GeometryBuffer;

        // Compute buffer for ActorData
        private ComputeBuffer m_ActorBuffer;

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

        // ParticleData asynchronously read back from GPU
        private static Unity.Collections.NativeArray<ParticleData> s_ParticleDataReadback;


        private void Awake()
        {
            UniverseActor.SetScale(m_SimulationUnitScale);
        }

        private void Start()
        {
            m_TargetCamera = m_TargetCameraController.GetComponent<Camera>();
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
            m_IsInitialised = true;

            Camera.onPostRender += OnPostRenderCallback;

            UniverseActor.Delegate_OnDictUpdate += UpdateActorBuffer;
            UpdateActorBuffer();

            AsyncGPUReadback.Request(m_ParticleBuffer[m_ReadIdx], OnAsyncGPUReadback);
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

            m_InitialClusterScaleMin *= m_SimulationUnitScale;
            m_InitialClusterScaleMax *= m_SimulationUnitScale;
            m_InitialSimulationLinearVelocity *= m_SimulationUnitScale;
            m_InitialSimulationDiscVelocity *= m_SimulationUnitScale;
        }

        private void SetupBuffers()
        {
            UnityEngine.Random.InitState(m_Seed);

            m_ParticleBuffer = new ComputeBuffer[]
            {
                new ComputeBuffer(m_InstanceCount, 32),
                new ComputeBuffer(m_InstanceCount, 32),
            };

            var clusterOrigins = new Vector3[m_ClusterCount];
            for (var i = 0; i < m_ClusterCount; i++)
            {
                clusterOrigins[i] = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(0f, m_ClusterDistributionMaxRadius);
            }

            var clusterRatio = m_ClusterCount / (float)m_InstanceCount;
            var particles = new ParticleData[m_InstanceCount];

            InitialiseParticles(clusterRatio, clusterOrigins, ref particles);

            m_ParticleBuffer[m_ReadIdx].SetData(particles);
            m_ParticleBuffer[m_WriteIdx].SetData(particles);

            var vertexCount = m_VerticesPerInstance * m_InstanceCount;
            m_GeometryBuffer = new ComputeBuffer(vertexCount, m_GeometryBufferStride, ComputeBufferType.Append);
            m_DrawCallArgsBuffer = new ComputeBuffer(1, 4*4, ComputeBufferType.IndirectArguments);
            m_ActorBuffer = new ComputeBuffer(UniverseActor.Limit, 7*4, ComputeBufferType.Structured);
        }

        private void InitialiseParticles(float clusterRatio, Vector3[] clusterOrigins, ref ParticleData[] particles)
        {
            for (var i = 0; i < m_InstanceCount; i++)
            {
                var clusterIdx = (int)Mathf.Floor(i * clusterRatio);
                var massAlpha = Mathf.Pow(UnityEngine.Random.Range(0f, 1f), m_MassDistributionExp);
                var velocityAlpha = Mathf.Pow(UnityEngine.Random.Range(0f, 1f), m_VelocityDistributionExp);

                particles[i].Mass = Mathf.Lerp((float)m_MinMass, (float)m_MaxMass, massAlpha);
                particles[i].Entropy = UnityEngine.Random.Range(0f, 1f);

                Vector3 linearVelocity;
                Vector3 discVelocity;
                Vector3 direction;

                switch(m_InitialisationShape)
                {
                    case InitShape.Sphere:
                        direction = UnityEngine.Random.onUnitSphere;
                        linearVelocity = direction * velocityAlpha;
                        discVelocity = Vector3.Cross(direction, Vector3.up) * velocityAlpha;

                        particles[i].Position =
                            transform.position +
                            clusterOrigins[clusterIdx] +
                            direction * (float)m_InitialClusterScaleMax;

                        particles[i].Velocity =
                            linearVelocity * (float)m_InitialSimulationLinearVelocity +
                            discVelocity * (float)m_InitialSimulationDiscVelocity;
                        break;

                    case InitShape.AccretionDisc:
                        var circleDirection = UnityEngine.Random.insideUnitCircle.normalized;
                        direction = new Vector3(circleDirection.x, 0f, circleDirection.y);

                        linearVelocity = Quaternion.Euler(0f, 90f, 0f) * direction * velocityAlpha;
                        discVelocity = Vector3.Cross(direction, Vector3.up) * velocityAlpha;

                        particles[i].Position =
                            transform.position +
                            clusterOrigins[clusterIdx] +
                            direction * UnityEngine.Random.Range((float)m_InitialClusterScaleMin, (float)m_InitialClusterScaleMax);

                        particles[i].Velocity =
                            linearVelocity * (float)m_InitialSimulationLinearVelocity +
                            discVelocity * (float)m_InitialSimulationDiscVelocity;
                        break;
                }
            }
        }

        private void SetupShaderProperties()
        { 
            m_ComputeShader.SetFloat("_G", (float)m_GravitationalConstant);
            m_ComputeShader.SetFloat("_MaxMass", (float)m_MaxMass);
            m_ComputeShader.SetFloat("_DistanceSoftening", m_DistanceSoftening);
            m_ComputeShader.SetFloat("_DistanceCoeff", m_DistanceCoeff);
            m_ComputeShader.SetFloat("_VelocityDecay", 1f - m_VelocityDecay);

            m_ComputeShader.SetVector("_ColourA", m_ColourA);
            m_ComputeShader.SetVector("_ColourB", m_ColourB);

            m_ComputeShader.SetInt("_InstanceCount", m_InstanceCount);

            m_ComputeShader.SetBuffer(m_KernelIdx, "_GeometryBuffer", m_GeometryBuffer);
            m_ComputeShader.SetBuffer(m_KernelIdx, "_DrawCallArgsBuffer", m_DrawCallArgsBuffer);
            m_ComputeShader.SetBuffer(m_KernelIdx, "_ActorBuffer", m_ActorBuffer);

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
            if (!m_IsInitialised)
                return;
                
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

        private void UpdateActorBuffer()
        {
            m_ComputeShader.SetInt("_ActorCount", UniverseActor.ActorsDict.Count);

            var actors = new UniverseActor.ActorData[UniverseActor.Limit];
            UniverseActor.ActorsDict.Values.CopyTo(actors, 0);

            m_ActorBuffer.SetData(actors);
        }

        private void OnAsyncGPUReadback(AsyncGPUReadbackRequest request)
        {
            if (!m_IsInitialised)
                return;

            if (request.hasError)
            {
                Debug.LogWarning("GPU readback failed!");
                return;
            }

            s_ParticleDataReadback = request.GetData<ParticleData>();

            var length = (int)(s_ParticleDataReadback.Length * m_SampleRatio);
            var centreWS = Vector3.zero;
            var centreCS = Vector3.zero;
            var sample = 0;

            var mat = m_TargetCamera.projectionMatrix * m_TargetCamera.worldToCameraMatrix;
            var rectCS = new Rect(0f, 0f, 0f, 0f);

            for (var i = 0; i < length; i++)
            {
                // Calculate the average position of particles
                sample = (int)UnityEngine.Random.Range(0, s_ParticleDataReadback.Length - 1);
                centreWS += s_ParticleDataReadback[sample].Position * (1f / length);

    #if UNITY_EDITOR
                Debug.DrawRay(s_ParticleDataReadback[sample].Position, Vector3.Normalize(-s_ParticleDataReadback[sample].Position));
                /*
                Debug.Log(
                    "Position: " + s_ParticleDataReadback[sample].Position.ToString("F10") +
                    "\t\t\t Velocity: " + s_ParticleDataReadback[sample].Velocity.ToString("F10"));
                */
    #endif

                // Calculate whether particle cloud is too big or small in frame
                // XY values > 1 || < -1 imply that a particle is outside the view frustrum
                centreCS = mat.MultiplyPoint(s_ParticleDataReadback[sample].Position);

                // Construct a rect that describes the bounds of the particle cloud in clipspace
                if (centreCS.x < rectCS.x)
                    rectCS.x = centreCS.x;

                if (centreCS.y < rectCS.y)
                    rectCS.y = centreCS.y;

                if (centreCS.x > rectCS.x + rectCS.width)
                    rectCS.width = centreCS.x - rectCS.x;

                if (centreCS.y > rectCS.y + rectCS.height)
                    rectCS.height = centreCS.y - rectCS.y;
            }

            var particleAreaCS = rectCS.width * rectCS.height;
            CameraController.Push(centreWS, particleAreaCS);

            // Set up next request
            AsyncGPUReadback.Request(m_ParticleBuffer[m_ReadIdx], OnAsyncGPUReadback);
        }

        private void OnPostRenderCallback(Camera cam)
        {
            if (cam != m_TargetCamera)
                return;

            m_Material.SetPass(0);

            if (m_RenderTopology == RenderTopology.Points)
                Graphics.DrawProceduralIndirectNow(MeshTopology.Points, m_DrawCallArgsBuffer, 0);
                
            else if (m_RenderTopology == RenderTopology.Lines)
                Graphics.DrawProceduralIndirectNow(MeshTopology.Lines, m_DrawCallArgsBuffer, 0);
        }

        private void OnDestroy()
        {
            m_IsInitialised = false;

            m_ParticleBuffer[m_ReadIdx].Release();
            m_ParticleBuffer[m_WriteIdx].Release();
            m_GeometryBuffer.Release();
            m_DrawCallArgsBuffer.Release();
            m_ActorBuffer.Release();

            Camera.onPostRender -= OnPostRenderCallback;
            UniverseActor.Delegate_OnDictUpdate -= UpdateActorBuffer;
        }
    }
}