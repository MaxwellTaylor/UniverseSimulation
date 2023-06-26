Shader "UniverseSimulation/DrawParticles"
{
    Properties
    {
        [Header(Primatives)]
        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("CullMode", Integer) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendModeSrc ("BlendSrc", Integer) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _BlendModeDst ("BlendDst", Integer) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Integer) = 1

        [Toggle] _ZWrite ("ZWrite", Integer) = 0
        [Toggle(USE_GEOMETRYDATA)] _UseGeometryData("GeometryData", Float) = 1

        [Space]

        [Header(Aesthetics)]
        _Colour ("Colour", Color) = (1.0, 1.0, 1.0, 1.0)
        _Ambient ("Ambient", Color) = (0.0, 0.0, 0.0, 1.0)
        _Exposure("Exposure", float) = 1.0
        _PointSize ("Size", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" }

        Pass
        {
            Cull [_CullMode] 
            ZTest [_ZTest] 
            ZWrite [_ZWrite] 
            Blend [_BlendModeDst] [_BlendModeSrc]

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_fog    
            #pragma multi_compile BUILD_POINTS BUILD_LINES BUILD_TETRAHEDRONS
            #pragma shader_feature USE_GEOMETRYDATA

            #include "UnityCG.cginc"

            float3 _Colour;
            float3 _Ambient;

            float _PointSize;
            float _Exposure;
            float _MinBrightness;

#if defined(USE_GEOMETRYDATA) && defined(BUILD_TETRAHEDRONS)
            float3 _LightColour;
            float3 _LightDirection;
#endif

            #if defined(USE_GEOMETRYDATA)
                struct GeometryData
                {
                #if defined(BUILD_POINTS)
                    float3 Vertices[1];
                #elif defined(BUILD_LINES)
                    float3 Vertices[2];
                #elif defined(BUILD_TETRAHEDRONS)
                    float3 Vertices[12];
                    float3 Normals[4];
                #endif

                    float3 Colour;
                };

                StructuredBuffer<GeometryData> _GeometryBuffer;
            #else
                struct GeometrySimple
                {
                    float4 vertex : POSITION;
                };
            #endif

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 colour : COLOR0;
                UNITY_FOG_COORDS(0)

                #if defined(BUILD_POINTS)
                    float size : PSIZE;
                #elif defined(BUILD_TETRAHEDRONS)
                    float3 normal : TEXCOORD1;
                    float3 toCamera : TEXCOORD2;
                #endif
            };

#if defined(USE_GEOMETRYDATA)
            v2f vert (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
#else
            v2f vert (GeometrySimple v)
#endif
            {
                v2f o;
                float3 vertexColour = (1.0).xxx;

#if defined(USE_GEOMETRYDATA)
                GeometryData geo = _GeometryBuffer[instanceID];
                vertexColour = geo.Colour;

                #if defined(BUILD_POINTS)
                    int idx = vertexID;
                    o.size = _PointSize;
                #elif defined(BUILD_LINES)
                    int idx = vertexID % 2;
                #elif defined(BUILD_TETRAHEDRONS)
                    int idx = vertexID % 12;
                    o.normal = geo.Normals[idx / 3];                    
                #endif

                float4 vertexWS = float4(geo.Vertices[idx], 1.0);
                o.vertex = mul(UNITY_MATRIX_VP, vertexWS);

                #if defined(BUILD_TETRAHEDRONS)
                    o.toCamera = normalize(_WorldSpaceCameraPos - vertexWS.xyz);
                #endif
#else
                o.vertex = UnityObjectToClipPos(v.vertex);
#endif

                o.colour = vertexColour * _Colour;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 colour = float4(i.colour, 1.0);

                #if defined(USE_GEOMETRYDATA) && defined(BUILD_TETRAHEDRONS)
                    // Diffuse
                    float nDotL = dot(i.normal, _LightDirection);
                    colour.rgb *= saturate(nDotL) * _LightColour;

                    // Specular
                    float3 halfway = normalize(i.toCamera + _LightDirection);
                    float nDotH = dot(halfway, i.normal);
                    nDotH = saturate(nDotH);

                    #define SMOOTHNESS 10.0
                    colour.rgb += pow(nDotH, SMOOTHNESS);
                #endif

                colour.rgb += _Ambient;
                colour.rgb *= _Exposure;

                UNITY_APPLY_FOG(i.fogCoord, colour);
                return colour;
            }

            ENDCG
        }
    }
}