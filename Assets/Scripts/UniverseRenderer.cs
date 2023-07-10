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
        #region PUBLIC VARIABLES
        public delegate void OnRendererInit();
        public static OnRendererInit Delegate_OnRendererInit = null;

        [Header("Simulation Settings")]

        [Tooltip("Whether to show diagnostics.")]
        public bool Diagnostics = false;
        [Tooltip("Seed for pseudorandomness.")]
        public int Seed = -99;
        [Tooltip("Simulation speed coefficient.")]
        public float SimulationSpeed = 1f;
        [Tooltip("Unit coefficient. Enables extreme number representation within float32's.")]
        public double SimulationUnitScale = 0.1;
        [Tooltip("ParticleCollections to use in simulation.")]
        public List<ParticleCollectionPack> ParticleCollections;

        [Space]
        [Header("Behaviour")]

        [Tooltip("Gravitational constant (G).")]
        public double GravitationalConstant = 6.674e-11;
        [Tooltip("Distance coefficient for particles in calculations.")]
        public float DistanceCoeff = 1f;
        [Tooltip("Velocity decay for particles in calculations.")]
        public float VelocityDecay = 0.00001f;
        [Tooltip("The distance exponent used in Newton's equation. Quadratic resembles real behaviour (inverse-square law).")]
        public DistanceFunction DistanceFunction = DistanceFunction.Quadratic;

        [Space]
        [Header("Rendering")]

        [Tooltip("Compute shader for calculating particle interactions.")]
        public ComputeShader ComputeShader;
        [Tooltip("Camera to render from (must have CameraController component).")]
        public CameraController TargetCameraController;
        [Tooltip("Proportion of particles to sample on CPU (used for camera tracking and diagnostics).")]
        public float SampleRatio = 0.02f;
        #endregion

        #region PRIVATE VARIABLES
        private bool m_IsInitialised = false;
        private bool m_ScaleIsSet = false;
        private int m_KernelIdx = -1;
        private int m_AggregateInstanceCount;

        private int m_ReadIdx = 0;
        private int m_WriteIdx = 1;

        private Camera m_TargetCamera;
        private Light m_DirectionalLight;
        private ComputeBuffer m_ActorBuffer;
        private ComputeBuffer[] m_ParticleBuffers;
        private UniverseActor[] m_Actors;

        // ParticleData asynchronously read back from GPU
        private static Unity.Collections.NativeArray<ParticleData> s_ParticleDataReadback;
        #endregion

        #region PROPERTIES
        private ComputeBuffer ParticleBufferRead
        {
            get
            {
                return m_ParticleBuffers[m_ReadIdx];
            }
            set {}
        }

        private ComputeBuffer ParticleBufferWrite
        {
            get
            {
                return m_ParticleBuffers[m_WriteIdx];
            }
            set {}
        }
        #endregion

        #region MONOBEHAVIOUR
        private void Awake()
        {
            SetScale(false);
        }

        private void OnValidate()
        {
            if (ParticleCollections == null || ParticleCollections.Count == 0)
                Debug.LogWarning("No ParticleCollectionPacks found!");

            foreach (var entry in ParticleCollections)
            {
                var collection = entry.Collection;

                if (collection == null)
                    Debug.LogWarning("Null ParticleCollection reference found!");
            }

            SetScale(false);
        }

        private void Start()
        {
            UnityEngine.Random.InitState(Seed);

            m_TargetCamera = TargetCameraController.GetComponent<Camera>();
            var directionalLights = FindObjectsOfType<Light>();
            m_DirectionalLight = null;

            for (int i = 0; i < directionalLights.Length; i++)
            {
                if (directionalLights[i].type == LightType.Directional)
                {
                    m_DirectionalLight = directionalLights[i];
                    break;
                }
            }

            m_AggregateInstanceCount = 0;
            foreach(var entry in ParticleCollections)
            {
                var collection = entry.Collection;
                var position = entry.Transform != null ? entry.Transform.position : transform.position;

                if (collection == null)
                    continue;

                collection.Setup(m_AggregateInstanceCount, position, m_DirectionalLight);
                m_AggregateInstanceCount += collection.InstanceCount;
            }

            SetupBuffers();
            SetupComputeShader();
            m_IsInitialised = true;

            Camera.onPostRender += OnPostRenderCallback;
            UniverseActor.Delegate_OnDictUpdate += OnActorsUpdated;
            OnActorsUpdated();
            AsyncGPUReadback.Request(ParticleBufferRead, OnAsyncGPUReadback);

            if (Delegate_OnRendererInit != null)
                Delegate_OnRendererInit.Invoke();
        }

        private void Update()
        {
            if (!m_IsInitialised)
                return;
                
            PingPong();

            foreach (var entry in ParticleCollections)
            {
                var collection = entry.Collection;

                if (collection == null)
                    continue;

                SetGlobalKeywords(collection);
                SetComputeShaderProperties(collection);
                collection.PrepareForRender();

                DispatchCompute(collection);
            }
        }

        private void OnDestroy()
        {
            if (!m_IsInitialised)
                return;

            m_IsInitialised = false;
            m_ActorBuffer.Release();

            ParticleBufferRead.Release();
            ParticleBufferWrite.Release();
            m_ParticleBuffers = null;

            foreach (var entry in ParticleCollections)
            {
                var collection = entry.Collection;

                if (collection != null)
                    collection.Cleanup();
            }

            Camera.onPostRender -= OnPostRenderCallback;
            UniverseActor.Delegate_OnDictUpdate -= OnActorsUpdated;
        }

        private void OnDrawGizmos()
        {
            var r = 0f;
            SetScale(true);

            Gizmos.color = Color.grey;
            foreach (var entry in ParticleCollections)
            {
                var collection = entry.Collection;
                var position = entry.Transform != null ? entry.Transform.position : transform.position;

                if (collection == null)
                    continue;

                r = collection.DistributionOuterDiameter.GetScaled();
                Gizmos.DrawWireSphere(position, r);
            }

            Gizmos.color = Color.green;
            foreach (var actor in UniverseActor.ActorDataDict.Keys)
            {
                r = actor.Radius.GetScaled();
                Gizmos.DrawWireSphere(transform.position, r);
            }
        }
        #endregion

        #region CALLBACKS
        private void OnPostRenderCallback(Camera cam)
        {
            if (cam != m_TargetCamera)
                return;

            foreach (var entry in ParticleCollections)
            {
                var collection = entry.Collection;

                if (collection == null)
                    continue;

                // Render using Material's assigned GeometryBuffer
                collection.Material.SetPass(0);
                Graphics.DrawProceduralIndirectNow(collection.MeshTopology, collection.DrawCallArgsBuffer, 0);
            }
        }

        private void OnActorsUpdated()
        {
            ComputeShader.SetInt(Common.k_ShaderPropActorCount, UniverseActor.ActorDataDict.Count);

            var actors = new ActorData[UniverseActor.Limit];
            UniverseActor.ActorDataDict.Values.CopyTo(actors, 0);
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

            var length = (int)(s_ParticleDataReadback.Length * SampleRatio);
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
                    if (Diagnostics)
                    {
                        Debug.DrawRay(s_ParticleDataReadback[sample].Position, Vector3.Normalize(-s_ParticleDataReadback[sample].Position));
                        Debug.Log(
                            "Position: " + s_ParticleDataReadback[sample].Position.ToString("F10") +
                            "\t\t\t Velocity: " + s_ParticleDataReadback[sample].Velocity.ToString("F10"));
                    }
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
            AsyncGPUReadback.Request(ParticleBufferRead, OnAsyncGPUReadback);
        }
        #endregion

        #region GENERAL
        private void SetScale(bool doCheck)
        {
            if (doCheck && m_ScaleIsSet)
                return;

            MeasurementContainer.SetGlobalScalar(SimulationUnitScale);
            m_ScaleIsSet = true;
        }

        private void PingPong()
        {
            m_ReadIdx = (m_ReadIdx + 1) % 2;
            m_WriteIdx = (m_WriteIdx + 1) % 2;

            ComputeShader.SetBuffer(m_KernelIdx, Common.k_ShaderPropParticleBufferRead, ParticleBufferRead);
            ComputeShader.SetBuffer(m_KernelIdx, Common.k_ShaderPropParticleBufferWrite, ParticleBufferWrite);
        }

        private void DispatchCompute(ParticleCollection collection)
        {
            ComputeShader.Dispatch(m_KernelIdx, collection.InstanceCount / 64, 1, 1);
        }

        private void SetupBuffers()
        {
            m_ActorBuffer = new ComputeBuffer(UniverseActor.Limit, 8*4, ComputeBufferType.Structured);

            m_ParticleBuffers = new ComputeBuffer[]
            {
                new ComputeBuffer(m_AggregateInstanceCount, 32),
                new ComputeBuffer(m_AggregateInstanceCount, 32),
            };

            var aggregateParticles = new ParticleData[m_AggregateInstanceCount];
            int prevInstanceCount = 0;

            foreach (var entry in ParticleCollections)
            {
                var collection = entry.Collection;

                if (collection == null)
                    continue;

                Array.Copy(collection.Particles, 0, aggregateParticles, prevInstanceCount, collection.Particles.Length);
                prevInstanceCount = collection.InstanceCount;
            }

            ParticleBufferRead.SetData(aggregateParticles);
            ParticleBufferWrite.SetData(aggregateParticles);
        }

        private void SetupComputeShader()
        {
            m_KernelIdx = ComputeShader.FindKernel(Common.k_ComputeKernalName);

            // Universal properties
            ComputeShader.SetFloat(Common.k_ShaderPropG, (float)GravitationalConstant);
            ComputeShader.SetFloat(Common.k_ShaderPropDistanceCoeff, DistanceCoeff);
            ComputeShader.SetFloat(Common.k_ShaderPropVelocityDecay, 1f - VelocityDecay);
            ComputeShader.SetBuffer(m_KernelIdx, Common.k_ShaderPropActorBuffer, m_ActorBuffer);

            foreach (int value in Enum.GetValues(typeof(DistanceFunction)))
            {
                var keyword = Common.k_KeywordDistanceFunction + "_" + value.ToString();
                var setActive = (int)DistanceFunction == value;

                if (setActive)
                    ComputeShader.EnableKeyword(keyword);
                else
                    ComputeShader.DisableKeyword(keyword);
            }
        }

        private void SetComputeShaderProperties(ParticleCollection collection)
        {
            ComputeShader.SetFloat(Common.k_ShaderPropTime, Time.time);
            ComputeShader.SetFloat(Common.k_ShaderPropTimeStep, SimulationSpeed);
            ComputeShader.SetMatrix(Common.k_ShaderPropVPMatrix, m_TargetCamera.projectionMatrix * m_TargetCamera.worldToCameraMatrix);

            ComputeShader.SetBuffer(m_KernelIdx, Common.k_ShaderPropGeometryBuffer, collection.GeometryBuffer);
            ComputeShader.SetBuffer(m_KernelIdx, Common.k_ShaderPropDrawCallArgsBuffer, collection.DrawCallArgsBuffer);

            ComputeShader.SetVector(Common.k_ShaderPropUnitConversion, MeasurementContainer.GetUnitConversionVector());

            ComputeShader.SetVector(Common.k_ShaderPropColourA, collection.ColourA);
            ComputeShader.SetVector(Common.k_ShaderPropColourB, collection.ColourB);
            ComputeShader.SetFloat(Common.k_ShaderPropAverageMass, collection.TotalMass.GetScaled() / (float)collection.InstanceCount);
            ComputeShader.SetFloat(Common.k_ShaderPropParticleRadius, collection.ParticleRadius.GetScaled());
            ComputeShader.SetInt(Common.k_ShaderPropInstanceCount, collection.InstanceCount);
            ComputeShader.SetInt(Common.k_ShaderPropStartPosition, collection.StartPosition);

            if (collection.RenderTopology == RenderTopology.Lines)
                ComputeShader.SetFloat(Common.k_ShaderPropTrailLength, collection.TrailLength);
        }

        private static void SetGlobalKeywords(ParticleCollection collection)
        {
            for (var i = 0; i < collection.KeywordsDisable.Count; i++)
                Shader.DisableKeyword(collection.KeywordsDisable[i]);

            for (var i = 0; i < collection.KeywordsEnable.Count; i++)
                Shader.EnableKeyword(collection.KeywordsEnable[i]);
        }
        #endregion
    }
}