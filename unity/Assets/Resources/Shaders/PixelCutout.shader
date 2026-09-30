// 입체 건물·나무·차·사람. 텍스처 × 버텍스 색(탄 자국·젖음 틴트)을 반으로 잘라 그리고 깊이를 남긴다.
// 그래야 뒤에 그려지는 불꽃·빛·파티클(반투명)이 건물·나무 뒤에서 가려진다.
// URP 스프라이트 셰이더(Sprite-Unlit-Default) 구조를 따른다: 스프라이트 텍스처·색은 렌더러마다 URP가 넘긴다.
Shader "FireGame/PixelCutout"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite On

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                clip(c.a - _Cutoff);
                return c;
            }
            ENDHLSL
        }
    }
}
