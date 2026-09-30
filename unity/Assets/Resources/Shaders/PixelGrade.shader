// 픽셀 3D 화면 보정: 도트 월드 텍스처를 화면에 옮기며 채도·대비를 누르고, 어두운 곳은 푸르게·밝은 곳은 따뜻하게 물들이고,
// 비스듬한 햇빛 띠와 가장자리 어둠(비네트)을 더한다. HUD는 이 쿼드 밖이라 그대로다.
Shader "FireGame/PixelGrade"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Saturation ("Saturation", Range(0, 2)) = 0.85
        _Contrast ("Contrast", Range(0.5, 2)) = 1.08
        _Shadow ("Shadow Tint", Color) = (0.85, 0.88, 1.05, 1)
        _Light ("Light Tint", Color) = (1.06, 1.0, 0.9, 1)
        _ShaftColor ("Shaft Color (a = strength)", Color) = (1, 0.92, 0.7, 0.06)
        _Vignette ("Vignette", Range(0, 2)) = 0.55
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite On
        Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _Saturation;
            half _Contrast;
            half4 _Shadow;
            half4 _Light;
            half4 _ShaftColor;
            half _Vignette;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half3 c = tex2D(_MainTex, i.uv).rgb;
                half luma = dot(c, half3(0.299, 0.587, 0.114));
                c = lerp(luma.xxx, c, _Saturation);
                c = (c - 0.5) * _Contrast + 0.5;
                c *= lerp(_Shadow.rgb, _Light.rgb, saturate(luma * 1.4));
                // 햇빛 띠: 왼쪽 위에서 비스듬히 내려오고 천천히 흐른다. 화면 위쪽일수록 진하다.
                half band = sin((i.uv.x * 1.6 + i.uv.y * 0.9) * 6.0 + _Time.y * 0.15);
                half shaft = smoothstep(0.55, 1.0, band) * (0.4 + i.uv.y * 0.6);
                c += shaft * _ShaftColor.rgb * _ShaftColor.a;
                float2 d = i.uv - 0.5;
                c *= 1.0 - _Vignette * dot(d, d);
                return fixed4(saturate(c), 1);
            }
            ENDCG
        }
    }
}
