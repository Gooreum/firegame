// 샘플 붓(SampleCanvas)용: 정점색 + 미리 곱한 알파. uv2.x = 1이면 보통 칠하기(source-over), 0이면 빛 겹침(lighter).
// 한 메시 안에서 두 섞기를 그리는 순서 그대로 섞을 수 있다(웹 캔버스의 globalCompositeOperation 두 가지).
Shader "FireGame/SampleCanvas"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 mode : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float mode : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.mode = v.mode.x;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                float a = i.color.a * t.a;
                return fixed4(i.color.rgb * t.rgb * a, a * i.mode);
            }
            ENDCG
        }
    }
}
