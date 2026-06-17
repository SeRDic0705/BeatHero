// 피격 시 스프라이트를 솔리드 색(_FlashColor)으로 번쩍이게 하는 URP 2D 스프라이트 셰이더.
// _FlashAmount(0~1)를 MaterialPropertyBlock으로 렌더러마다 제어한다.
Shader "BeatHero/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline"  = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // 2D SRP Batcher는 머티리얼 CBUFFER 내 _MainTex_ST/_TexelSize를 지원하지 않는다.
            // 스프라이트는 아틀라스 UV를 그대로 쓰므로 _ST(타일링/오프셋) 불필요 → 제외.
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _FlashColor;
                float  _FlashAmount;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                OUT.uv          = IN.uv; // 스프라이트 아틀라스 UV 그대로 사용
                OUT.color       = IN.color * _Color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;
                // 알파는 유지하고 RGB만 플래시 색으로 보간 → 실루엣 형태로 번쩍임
                tex.rgb = lerp(tex.rgb, _FlashColor.rgb, _FlashAmount);
                return tex;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
