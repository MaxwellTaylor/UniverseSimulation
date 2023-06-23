Shader "UniverseRendering/Particles"
{
    Properties
    {
        _PointSize ("Point Size", Float) = 1
        _Exposure("Exposure", float) = 1.0
        _MinBrightness("Minimum Brightness", float) = 0.1
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
            #pragma multi_compile _ BUILD_LINES

            #include "UnityCG.cginc"

            float _PointSize;
            float _Exposure;
            float _MinBrightness;

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

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float size : PSIZE;
                float distance : TEXCOORD0;
                float3 colour : COLOR0;
            };

            v2f vert (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;
                GeometryData geo = _GeometryBuffer[instanceID];

                o.size = _PointSize;
                o.colour = geo.Colour * _Exposure;
                o.vertex = mul(UNITY_MATRIX_VP, float4(geo.Vertices[vertexID % 2], 1.0));

                float distToVert = length(geo.Vertices[vertexID % 2] - _WorldSpaceCameraPos);
                o.distance = distToVert / (_ProjectionParams.z * 0.1);

                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 colour = max(i.colour * (1.0 - i.distance), (_MinBrightness).xxx);
                return float4(colour, 1.0);
            }

            ENDCG
        }
    }
}