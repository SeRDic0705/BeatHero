Shader "UI/IrisWipe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color    ("Tint",     Color)        = (0,0,0,1)
        _Radius   ("Radius",   Range(0, 1))  = 1
        _Softness ("Softness", Range(0.001, 0.1)) = 0.02

        _StencilComp      ("Stencil Comparison", Float) = 8
        _Stencil          ("Stencil ID",          Float) = 0
        _StencilOp        ("Stencil Operation",   Float) = 0
        _StencilWriteMask ("Stencil Write Mask",  Float) = 255
        _StencilReadMask  ("Stencil Read Mask",   Float) = 255
        _ColorMask        ("Color Mask",           Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "IgnoreProjector"= "True"
            "RenderType"     = "Transparent"
            "PreviewType"    = "Plane"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                float2 uv            : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 color         : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _Color;
            float  _Radius;
            float  _Softness;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex        = UnityObjectToClipPos(OUT.worldPosition);
                OUT.uv            = v.uv;
                OUT.color         = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv    = IN.uv - 0.5;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                uv.x *= aspect;

                // cornerDist: 화면 코너까지의 거리 (aspect-corrected UV 공간)
                float cornerDist = 0.5 * sqrt(aspect * aspect + 1.0);
                float maxDist    = cornerDist + _Softness * 2.0;
                float threshold  = _Radius * maxDist;

                float dist  = length(uv);
                // dist < threshold → 원 안 (투명), dist > threshold → 원 밖 (검정)
                float alpha = smoothstep(threshold - _Softness, threshold + _Softness, dist);

                fixed4 color = IN.color;
                color.a *= alpha * UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                clip(color.a - 0.001);
                return color;
            }
            ENDCG
        }
    }
}
