Shader "UniverseSimulation/DrawParticles"
{
    Properties
    {
        _Colour ("Colour", Color) = (1.0, 1.0, 1.0, 1.0)
        _PointSize ("Point Size", Float) = 1
        _Exposure("Exposure", float) = 1.0

        [Toggle(USE_GEOMETRYDATA)] _UseGeometryData("Use GeometryData", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" }

        Pass
        {
            Cull Off 
            ZTest Always 
            ZWrite Off 
            Blend One One

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_fog    
            #pragma multi_compile _ BUILD_LINES
            #pragma shader_feature USE_GEOMETRYDATA

            #include "UnityCG.cginc"

            float3 _Colour;

            float _PointSize;
            float _Exposure;
            float _MinBrightness;

            #if defined(USE_GEOMETRYDATA)
                struct GeometryData
                {
                    #if defined(BUILD_LINES)
                        float3 Vertices[2];
                    #else
                        float3 Vertices[1];
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
                float size : PSIZE;
                float3 colour : COLOR0;
                UNITY_FOG_COORDS(0)
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

                float4 vertexWS = float4(geo.Vertices[vertexID % 2], 1.0);
                o.vertex = mul(UNITY_MATRIX_VP, vertexWS);
#else
                o.vertex = UnityObjectToClipPos(v.vertex);
#endif

                o.size = _PointSize;
                o.colour = vertexColour * _Colour * _Exposure;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 colour = float4(i.colour, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, colour);
                return colour;
            }

            ENDCG
        }
    }
}