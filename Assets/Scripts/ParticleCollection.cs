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
        [Header("Rendering")]

        [Tooltip("Topology type to render.")]
        public RenderTopology RenderTopology = RenderTopology.Points;
        [Tooltip("Colour variant for particles.")]
        [ColorUsage(false, true)] public Color ColourA = Color.white;
        [Tooltip("Colour variant for particles.")]
        [ColorUsage(false, true)] public Color ColourB = Color.white;

        [Space]
        [Header("Behaviour")]

        [Tooltip("Particle cluster initialisation shape.")]
        public InitShape InitialisationShape = InitShape.AccretionDisc;
        [Tooltip("Total number of particles in ParticleCollection.")]
        public ParticleCount ParticleCount = ParticleCount._32768;
        [Tooltip("Number of particle clusters.")]
        public int ClusterCount = 1;
        [Tooltip("Maximum particle cluster distribution radius (unit: Meters).")]
        public float ClusterMaxRadius = 0;
        [Tooltip("Probability distribution of particle mass.")]
        public float MassDistributionExp = 1f;
        [Tooltip("Probability distribution of particle velocity.")]
        public float VelocityDistributionExp = 1f;

        [Space]
        [Header("Physical Properties")]

        [Tooltip("Minimum mass of a particle (unit: Kilograms).")]
        public double MinMass = 1e0;
        [Tooltip("Maximum mass of a particle (unit: Kilograms).")]
        public double MaxMass = 1e1;
        [Tooltip("Inner diameter of particle clusters (unit: Meters). Used for AccretionDiscs.")]
        public double DistributionScaleMin = 1e1;
        [Tooltip("Outer diameter of particle clusters (unit: Meters).")]
        public double DistributionScaleMax = 5e1;
        [Tooltip("Linear velocity of particles upon initiation (unit: Meters/Second).")]
        public double SimulationLinearVelocity = 1e1;
        [Tooltip("Disc velocity of particles upon initiation (unit: Meters/second).")]
        public double SimulationDiscVelocity = 1e1;

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
        public float TrailLength = 0.01f;

        [NonSerialized] public int VerticesPerInstance;
        [NonSerialized] public int GeometryBufferStride;
        [NonSerialized] public string[] KeywordsEnable;
        [NonSerialized] public string[] KeywordsDisable;
        [NonSerialized] public MeshTopology MeshTopology;

        [NonSerialized] public Material Material;
        [NonSerialized] public ComputeBuffer DrawCallArgsBuffer;
        [NonSerialized] public ComputeBuffer GeometryBuffer;
        [NonSerialized] public ParticleData[] Particles;
        [NonSerialized] public int StartPosition;
        #endregion

        #region PRIVATE VARIABLES
        private static double s_SimulationUnitScale;
        private static List<ParticleCollection> s_InternalParticleCollectionsList;
        #endregion

        #region PROPERTIES
        public int InstanceCount
        {
            get { return (int)ParticleCount; }
            set {}
        }
        #endregion

        #region CONSTRUCTOR
        public ParticleCollection()
        {
            if (s_InternalParticleCollectionsList == null)
                s_InternalParticleCollectionsList = new List<ParticleCollection>();

            if (!s_InternalParticleCollectionsList.Contains(this))
                s_InternalParticleCollectionsList.Add(this);
        }
        #endregion

        #region GENERAL
        public static void SetScale(double scale)
        {
            s_SimulationUnitScale = scale;

            foreach (var collection in s_InternalParticleCollectionsList)
            {
                collection.MinMass *= s_SimulationUnitScale;
                collection.MaxMass *= s_SimulationUnitScale;
                collection.DistributionScaleMin *= s_SimulationUnitScale;
                collection.DistributionScaleMax *= s_SimulationUnitScale;
                collection.SimulationLinearVelocity *= s_SimulationUnitScale;
                collection.SimulationDiscVelocity *= s_SimulationUnitScale;
            }
        }

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
            if (s_InternalParticleCollectionsList.Contains(this))
                s_InternalParticleCollectionsList.Remove(this);

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

            switch(RenderTopology)
            {
                case RenderTopology.Points:
                    VerticesPerInstance = 1;
                    GeometryBufferStride = 24;
                    KeywordsEnable = new string[] { Common.k_KeywordBuildPoints };
                    KeywordsDisable = new string[] { Common.k_KeywordBuildLines, Common.k_KeywordBuildTetrahedrons };

                    Material.SetFloat(Common.k_MaterialPropCullMode, (float)UnityEngine.Rendering.CullMode.Off);
                    Material.SetFloat(Common.k_MaterialPropBlendModeSrc, (float)UnityEngine.Rendering.BlendMode.One);
                    Material.SetFloat(Common.k_MaterialPropBlendModeDst, (float)UnityEngine.Rendering.BlendMode.One);
                    Material.SetFloat(Common.k_MaterialPropZWrite, false ? 1f : 0f);

                    MeshTopology = MeshTopology.Points;
                    break;

                case RenderTopology.Lines:
                    VerticesPerInstance = 2;
                    GeometryBufferStride = 36;
                    KeywordsEnable = new string[] { Common.k_KeywordBuildLines };
                    KeywordsDisable = new string[] { Common.k_KeywordBuildPoints, Common.k_KeywordBuildTetrahedrons };

                    Material.SetFloat(Common.k_MaterialPropCullMode, (float)UnityEngine.Rendering.CullMode.Off);
                    Material.SetFloat(Common.k_MaterialPropBlendModeSrc, (float)UnityEngine.Rendering.BlendMode.One);
                    Material.SetFloat(Common.k_MaterialPropBlendModeDst, (float)UnityEngine.Rendering.BlendMode.One);
                    Material.SetFloat(Common.k_MaterialPropZWrite, false ? 1f : 0f);

                    MeshTopology = MeshTopology.Lines;
                    break;

                case RenderTopology.Tetrahedrons:
                    VerticesPerInstance = 12;
                    GeometryBufferStride = 192;
                    KeywordsEnable = new string[] { Common.k_KeywordBuildTetrahedrons };
                    KeywordsDisable = new string[] { Common.k_KeywordBuildPoints, Common.k_KeywordBuildLines };

                    Material.SetFloat(Common.k_MaterialPropCullMode, (float)UnityEngine.Rendering.CullMode.Back);
                    Material.SetFloat(Common.k_MaterialPropBlendModeSrc, (float)UnityEngine.Rendering.BlendMode.Zero);
                    Material.SetFloat(Common.k_MaterialPropBlendModeDst, (float)UnityEngine.Rendering.BlendMode.One);
                    Material.SetFloat(Common.k_MaterialPropZWrite, true ? 1f : 0f);

                    MeshTopology = MeshTopology.Triangles;
                    break;
            }
        }

        private void SetupParticles(Vector3 origin)
        {
            var clusterOrigins = new Vector3[ClusterCount];
            for (var i = 0; i < ClusterCount; i++)
            {
                clusterOrigins[i] = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(0f, ClusterMaxRadius);
            }

            var clusterRatio = ClusterCount / (float)InstanceCount;
            Particles = new ParticleData[InstanceCount];

            for (var i = 0; i < InstanceCount; i++)
            {
                var clusterIdx = (int)Mathf.Floor(i * clusterRatio);
                var massAlpha = Mathf.Pow(UnityEngine.Random.Range(0f, 1f), MassDistributionExp);
                var velocityAlpha = Mathf.Pow(UnityEngine.Random.Range(0f, 1f), VelocityDistributionExp);

                Particles[i].Mass = Mathf.Lerp((float)MinMass, (float)MaxMass, massAlpha);
                Particles[i].Entropy = UnityEngine.Random.Range(0f, 1f);

                Vector3 linearVelocity;
                Vector3 discVelocity;
                Vector3 direction;

                switch(InitialisationShape)
                {
                    case InitShape.Sphere:
                        direction = UnityEngine.Random.onUnitSphere;
                        linearVelocity = direction * velocityAlpha;
                        discVelocity = Vector3.Cross(direction, Vector3.up) * velocityAlpha;

                        Particles[i].Position =
                            origin +
                            clusterOrigins[clusterIdx] +
                            direction * (float)DistributionScaleMax;

                        Particles[i].Velocity =
                            linearVelocity * (float)SimulationLinearVelocity +
                            discVelocity * (float)SimulationDiscVelocity;
                        break;

                    case InitShape.AccretionDisc:
                        var circleDirection = UnityEngine.Random.insideUnitCircle.normalized;
                        direction = new Vector3(circleDirection.x, 0f, circleDirection.y);

                        linearVelocity = Quaternion.Euler(0f, 90f, 0f) * direction * velocityAlpha;
                        discVelocity = Vector3.Cross(direction, Vector3.up) * velocityAlpha;

                        Particles[i].Position =
                            origin +
                            clusterOrigins[clusterIdx] +
                            direction * UnityEngine.Random.Range((float)DistributionScaleMin, (float)DistributionScaleMax);

                        Particles[i].Velocity =
                            linearVelocity * (float)SimulationLinearVelocity +
                            discVelocity * (float)SimulationDiscVelocity;
                        break;
                }
            }
        }
        #endregion
    }
}