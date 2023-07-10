using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine;
using System;

namespace UniverseSimulation
{
    [CreateAssetMenu(fileName = "ParticleCollection", menuName = "Universe Simulation/Create ParticleCollection")]
    public class ParticleCollection : ScriptableObject
    {
        #region PUBLIC VARIABLES
        [Header("Behaviour")]
        [Tooltip("Whether to simulate collision detection.")]
        public bool CollisionDetection = false;
        [Tooltip("Particle cluster initialisation shape.")]
        public InitShape InitialisationShape = InitShape.AccretionDisc;
        [Tooltip("Total number of particles in ParticleCollection.")]
        public ParticleCount ParticleCount = ParticleCount.Large_65536;
        [Tooltip("Probability distribution of particle mass.")]
        public float MassDistributionCurve = 1f;
        [Tooltip("Probability distribution of particle velocity.")]
        public float VelocityDistributionCurve = 1f;
        [Tooltip("Number of particle clusters.")]
        public int ClusterCount = 1;

        [Space]
        [Header("Physical Properties")]

        [Tooltip("Mass of entire ParticleCollection.")]
        public MeasurementContainer TotalMass = new MeasurementContainer(1.5e19, MeasurementUnits.Mass_Kilograms);
        [Tooltip("Maximum radius from origin of particle clusters.")]
        public MeasurementContainer ClusterMaxRadius = new MeasurementContainer(0, MeasurementUnits.Distance_Kilometres);
        [Tooltip("Particle radius used by collision detection and mesh rendering.")]
        public MeasurementContainer ParticleRadius = new MeasurementContainer(500, MeasurementUnits.Distance_Kilometres);
        [Tooltip("Inner diameter of particle clusters.")]
        public MeasurementContainer DistributionInnerDiameter = new MeasurementContainer(149000, MeasurementUnits.Distance_Kilometres);
        [Tooltip("Outer diameter of particle clusters.")]
        public MeasurementContainer DistributionOuterDiameter = new MeasurementContainer(274000, MeasurementUnits.Distance_Kilometres);
        [Tooltip("Linear velocity of particles upon initiation.")]
        public MeasurementContainer SimulationLinearVelocity = new MeasurementContainer(16.4, MeasurementUnits.Speed_KilometresPerSecond);
        [Tooltip("Disc velocity of particles upon initiation.")]
        public MeasurementContainer SimulationDiscVelocity = new MeasurementContainer(10, MeasurementUnits.Speed_KilometresPerSecond);

        [Space]
        [Header("Rendering")]

        [Tooltip("Topology type to render.")]
        public RenderTopology RenderTopology = RenderTopology.Tetrahedrons;
        [Tooltip("Blend mode to use.")]
        public BlendMode BlendMode = BlendMode.Additive;
        [Tooltip("Colour variant for particles.")]
        [ColorUsage(false, true)] public Color ColourA = Color.white;
        [Tooltip("Colour variant for particles.")]
        [ColorUsage(false, true)] public Color ColourB = Color.white;

        [Space]
        [Header("Material")]

        [Tooltip("Tint colour.")]
        [ColorUsage(false, true)] public Color MaterialColour = Color.white;
        [Tooltip("Ambient light colour.")]
        [ColorUsage(false, true)] public Color MaterialAmbient = Color.black;
        [Tooltip("Exposure of particles.")]
        public float MaterialExposure = 1f;
        [Tooltip("Size of particles if RenderTopology is set to Points.")]
        public float MaterialPointSize = 1f;
        [Tooltip("Length of particle trails if RenderTopology is set to Lines.")]
        public float TrailLength = 0.5f;

        [NonSerialized] public int VerticesPerInstance;
        [NonSerialized] public int GeometryBufferStride;
        [NonSerialized] public List<string> KeywordsEnable = new List<string>();
        [NonSerialized] public List<string> KeywordsDisable = new List<string>();
        [NonSerialized] public MeshTopology MeshTopology;

        [NonSerialized] public Material Material;
        [NonSerialized] public ComputeBuffer DrawCallArgsBuffer;
        [NonSerialized] public ComputeBuffer GeometryBuffer;
        [NonSerialized] public ParticleData[] Particles;
        [NonSerialized] public int StartPosition;
        #endregion

        #region PROPERTIES
        public int InstanceCount
        {
            get { return (int)ParticleCount; }
            set {}
        }
        #endregion

        #region GENERAL
        public void Setup(int startPosition, Vector3 origin, Light light)
        {
            SetupMaterial(light);
            SetupBuffers();
            SetupParticles(origin);

            StartPosition = startPosition;
            Material.SetBuffer(Common.k_MaterialPropGeometryBuffer, GeometryBuffer);
        }

        public void Cleanup()
        {
            GeometryBuffer.Release();
            DrawCallArgsBuffer.Release();

            #if UNITY_EDITOR
                if (!Application.isPlaying && Material != null)
                {
                    DestroyImmediate(Material);
                    Material = null;
                }
                else
            #endif
                {
                    Destroy(Material);
                    Material = null;
                }
        }

        public void PrepareForRender()
        {
            // 0: VerticesPerInstance
            // 1: InstanceCount
            // 2: StartVertexLocation
            // 3: StartInstanceLocation
            DrawCallArgsBuffer.SetData(new uint[]
            {
                (uint)VerticesPerInstance,
                0,
                0,
                0,
            });
            
            GeometryBuffer.SetCounterValue(0);
        }

        private void SetupBuffers()
        {
            if (GeometryBuffer == null)
            {
                var vertexCount = VerticesPerInstance * InstanceCount;
                GeometryBuffer = new ComputeBuffer(vertexCount, GeometryBufferStride, ComputeBufferType.Append);
            }

            if (DrawCallArgsBuffer == null)
                DrawCallArgsBuffer = new ComputeBuffer(1, 4*4, ComputeBufferType.IndirectArguments);
        }

        private void SetupMaterial(Light light)
        {
            if (Material == null)
            {
                Material = new Material(Common.ParticleShader)
                {
                    name = "ParticleCollectionMaterial",
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            Material.SetColor(Common.k_MaterialPropColour, MaterialColour);
            Material.SetColor(Common.k_MaterialPropAmbient, MaterialAmbient);
            Material.SetFloat(Common.k_MaterialPropExposure, MaterialExposure);
            Material.SetFloat(Common.k_MaterialPropPointSize, MaterialPointSize);

            Material.SetFloat(Common.k_MaterialPropZTest, (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            Material.SetFloat(Common.k_MaterialPropGeometryData, true ? 1f : 0f);

            var direction = (light == null) ? Vector3.up : light.transform.forward * -1f;
            var colour = (light == null) ? Color.white : light.color * light.intensity;

            Material.SetVector(Common.k_MaterialPropLightDirection, direction);
            Material.SetColor(Common.k_MaterialPropLightColour, colour);

            // This defaults to enabled, but it's set here anyway for robustness
            Material.EnableKeyword(Common.k_KeywordUseGeometryData);

            if (CollisionDetection)
                KeywordsEnable.Add(Common.k_KeywordCollisionDetection);
            else
                KeywordsDisable.Add(Common.k_KeywordCollisionDetection);

            switch(BlendMode)
            {
                case BlendMode.Additive:
                    Material.SetFloat(Common.k_MaterialPropBlendModeSrc, (float)UnityEngine.Rendering.BlendMode.One);
                    Material.SetFloat(Common.k_MaterialPropBlendModeDst, (float)UnityEngine.Rendering.BlendMode.One);
                    break;

                case BlendMode.Solid:
                    Material.SetFloat(Common.k_MaterialPropBlendModeSrc, (float)UnityEngine.Rendering.BlendMode.Zero);
                    Material.SetFloat(Common.k_MaterialPropBlendModeDst, (float)UnityEngine.Rendering.BlendMode.One);
                    break;
            }

            switch(RenderTopology)
            {
                case RenderTopology.Points:
                    VerticesPerInstance = 1;
                    GeometryBufferStride = 24;

                    KeywordsEnable.Add(Common.k_KeywordBuildPoints);
                    KeywordsDisable.Add(Common.k_KeywordBuildLines);
                    KeywordsDisable.Add(Common.k_KeywordBuildTetrahedrons);

                    Material.SetFloat(Common.k_MaterialPropCullMode, (float)UnityEngine.Rendering.CullMode.Off);
                    Material.SetFloat(Common.k_MaterialPropZWrite, false ? 1f : 0f);

                    MeshTopology = MeshTopology.Points;
                    break;

                case RenderTopology.Lines:
                    VerticesPerInstance = 2;
                    GeometryBufferStride = 36;

                    KeywordsEnable.Add(Common.k_KeywordBuildLines);
                    KeywordsDisable.Add(Common.k_KeywordBuildPoints);
                    KeywordsDisable.Add(Common.k_KeywordBuildTetrahedrons);

                    Material.SetFloat(Common.k_MaterialPropCullMode, (float)UnityEngine.Rendering.CullMode.Off);
                    Material.SetFloat(Common.k_MaterialPropZWrite, false ? 1f : 0f);

                    MeshTopology = MeshTopology.Lines;
                    break;

                case RenderTopology.Tetrahedrons:
                    VerticesPerInstance = 12;
                    GeometryBufferStride = 192;

                    KeywordsEnable.Add(Common.k_KeywordBuildTetrahedrons);
                    KeywordsDisable.Add(Common.k_KeywordBuildPoints);
                    KeywordsDisable.Add(Common.k_KeywordBuildLines);

                    Material.SetFloat(Common.k_MaterialPropCullMode, (float)UnityEngine.Rendering.CullMode.Back);
                    Material.SetFloat(Common.k_MaterialPropZWrite, true ? 1f : 0f);

                    MeshTopology = MeshTopology.Triangles;
                    break;
            }
        }

        private void SetupParticles(Vector3 origin)
        {
            var clusterOrigins = new Vector3[ClusterCount];
            var maxRadius = 0f;

            for (var i = 0; i < ClusterCount; i++)
            {
                maxRadius = ClusterMaxRadius.GetScaled();
                clusterOrigins[i] = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(0f, maxRadius);
            }

            var clusterRatio = ClusterCount / (float)InstanceCount;
            Particles = new ParticleData[InstanceCount];

            var massTotalInit = 0f;
            for (var i = 0; i < InstanceCount; i++)
            {
                // Entropy is useful for introducing randomised variation
                Particles[i].Entropy = UnityEngine.Random.Range(0f, 1f);

                var mass = Mathf.Pow(UnityEngine.Random.Range(float.Epsilon, 1f), MassDistributionCurve);
                Particles[i].Mass = mass;
                massTotalInit += mass;                
            }

            var massScalar = TotalMass.GetScaled() / massTotalInit;
            for (var i = 0; i < InstanceCount; i++)
            {
                var clusterIdx = (int)Mathf.Floor(i * clusterRatio);

                // Scale mass such that aggregate matches TotalMass
                Particles[i].Mass *= massScalar;

                // Calculate velocity
                var velocityAlpha = Mathf.Pow(UnityEngine.Random.Range(0f, 1f), VelocityDistributionCurve);
                Vector3 linearVelocity, discVelocity, direction;
  
                switch(InitialisationShape)
                {
                    case InitShape.Sphere:
                        direction = UnityEngine.Random.onUnitSphere;
                        linearVelocity = direction * velocityAlpha;
                        discVelocity = Vector3.Cross(direction, Vector3.up) * velocityAlpha;
                        break;

                    case InitShape.AccretionDisc:
                        var circleDirection = UnityEngine.Random.insideUnitCircle.normalized;
                        direction = new Vector3(circleDirection.x, 0f, circleDirection.y);

                        linearVelocity = Quaternion.Euler(0f, 90f, 0f) * direction * velocityAlpha;
                        discVelocity = Vector3.Cross(direction, Vector3.up) * velocityAlpha;
                        break;

                    default:
                        linearVelocity = discVelocity = direction = Vector3.zero;
                        break;
                }

                Particles[i].Position =
                    origin +
                    clusterOrigins[clusterIdx] +
                    direction * UnityEngine.Random.Range(DistributionInnerDiameter.GetScaled(), DistributionOuterDiameter.GetScaled());

                Particles[i].Velocity =
                    linearVelocity * SimulationLinearVelocity.GetScaled() +
                    discVelocity * SimulationDiscVelocity.GetScaled();
            }
        }
        #endregion
    }
}