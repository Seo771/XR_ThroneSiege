Shader "ThroneSiege/Wall Fog Surface"
{
    Properties
    {
        _Color ("Fog Color", Color) = (0.41, 0.44, 0.52, 0.55)
        _EdgeFade ("Edge Softness", Range(0.01, 0.5)) = 0.15
        [Toggle] _UseDepthFade ("Use Depth Fade (Requires Camera Depth Texture)", Float) = 0
        _SoftDistance ("Intersection Fade Distance", Range(0.1, 10)) = 3
        _NearFade ("Camera Fade Start", Float) = 0.1
        _FarFade ("Camera Fade End", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            fixed4 _Color;
            float _EdgeFade, _UseDepthFade, _SoftDistance, _NearFade, _FarFade;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.eyeDepth = -UnityObjectToViewPos(v.vertex).z;
                o.uv = v.uv;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float soft = 1;
                if (_UseDepthFade > 0.5)
                {
                    float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(
                        _CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));
                    soft = saturate((depth - i.eyeDepth) / max(0.001, _SoftDistance));
                }
                float2 edge = min(i.uv, 1.0 - i.uv);
                float border = smoothstep(0.0, _EdgeFade, edge.x)
                    * smoothstep(0.0, _EdgeFade, edge.y);
                float cameraFade = smoothstep(_NearFade, max(_NearFade + 0.001, _FarFade), i.eyeDepth);
                return fixed4(_Color.rgb, _Color.a * soft * border * cameraFade);
            }
            ENDCG
        }
    }
    Fallback Off
}
