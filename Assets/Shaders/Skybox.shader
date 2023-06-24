
Shader "UniverseSimulation/Skybox"
{
    Properties
    {
        [HDR] _ColourInner ("Inner Colour", Color) = (0.5, 0.5, 0.5, 1.0)
        [HDR] _ColourOuter ("Outer Colour", Color) = (0.5, 0.5, 0.5, 1.0)
        [HDR] _ColourXNeg ("Negative X Colour", Color) = (0.5, 0.5, 0.5, 1.0)
        [HDR] _ColourXPos ("Positive X Colour", Color) = (0.5, 0.5, 0.5, 1.0)
        _Falloff("Falloff", float) = 2.0
        _Exposure("Exposure", float) = 1.0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            half3 _ColourInner;
            half3 _ColourOuter;
            half3 _ColourXNeg;
            half3 _ColourXPos;

            float _Falloff;
            float _Exposure;

            struct appdata_t
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float4 vertex : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.vertex);
                o.vertex = v.vertex;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half3 colour = _ColourOuter;

                float alphaXPos = saturate(i.vertex.x);
                float alphaXNeg = saturate(-i.vertex.x);

                colour = lerp(colour, _ColourXPos, alphaXPos);
                colour = lerp(colour, _ColourXNeg, alphaXNeg);

                float alphaY = saturate(abs(i.vertex.y));
                alphaY = 1.0 - pow(alphaY, _Falloff);

                colour = lerp(colour, _ColourInner, alphaY);

                return half4(colour * _Exposure, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}