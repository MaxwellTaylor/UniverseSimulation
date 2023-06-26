using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniverseSimulation
{
    public static class Common
    {
        public const string k_KeywordUseGeometryData = "USE_GEOMETRYDATA";
        public const string k_KeywordBuildPoints = "BUILD_POINTS";
        public const string k_KeywordBuildLines = "BUILD_LINES";
        public const string k_KeywordBuildTetrahedrons = "BUILD_TETRAHEDRONS";

        public const string k_ComputeKernalName = "CSMain";
        public const string k_ShaderNameParticle = "UniverseSimulation/DrawParticles";

        public const string k_ShaderPropTime = "_Time";
        public const string k_ShaderPropTimeStep = "_TimeStep";
        public const string k_ShaderPropVPMatrix = "_VPMatrix";
        public const string k_ShaderPropParticleBufferRead = "_ParticleBufferRead";
        public const string k_ShaderPropParticleBufferWrite = "_ParticleBufferWrite";
        public const string k_ShaderPropG = "_G";
        public const string k_ShaderPropMaxMass = "_MaxMass";
        public const string k_ShaderPropDistanceSoftening = "_DistanceSoftening";
        public const string k_ShaderPropDistanceCoeff = "_DistanceCoeff";
        public const string k_ShaderPropVelocityDecay = "_VelocityDecay";
        public const string k_ShaderPropColourA = "_ColourA";
        public const string k_ShaderPropColourB = "_ColourB";
        public const string k_ShaderPropInstanceCount = "_InstanceCount";
        public const string k_ShaderPropGeometryBuffer = "_GeometryBuffer";
        public const string k_ShaderPropDrawCallArgsBuffer = "_DrawCallArgsBuffer";
        public const string k_ShaderPropActorBuffer = "_ActorBuffer";
        public const string k_ShaderPropTrailLength = "_TrailLength";
        public const string k_ShaderPropActorCount = "_ActorCount";

        public const string k_MaterialPropColour = "_Colour";
        public const string k_MaterialPropAmbient = "_Ambient";
        public const string k_MaterialPropExposure = "_Exposure";
        public const string k_MaterialPropPointSize = "_PointSize";
        public const string k_MaterialPropZTest = "_ZTest";
        public const string k_MaterialPropGeometryData = "_UseGeometryData";
        public const string k_MaterialPropCullMode = "_CullMode";
        public const string k_MaterialPropBlendModeSrc = "_BlendModeSrc";
        public const string k_MaterialPropBlendModeDst = "_BlendModeDst";
        public const string k_MaterialPropZWrite = "_ZWrite";
        public const string k_MaterialPropLightDirection = "_LightDirection";
        public const string k_MaterialPropLightColour = "_LightColour";
        public const string k_MaterialPropGeometryBuffer = "_GeometryBuffer";

        public static Shader ParticleShader
        {
            get { return Shader.Find(k_ShaderNameParticle); }
            set {}
        }

        static Common()
        {
            // ...
        }
    }
}