Shader "SignatureLogo/VideoUnlit"
{
    // 视频背景专用无光照着色器：不透明 + 双面 + 纯贴色（乘 _Tint 做亮度压暗）。
    // 必须放在 Resources 目录：VideoBackground 运行时通过 Resources.Load 加载，
    // 否则打包时未被资产引用的着色器会被剥离，导致 Shader.Find 返回空、视频消失。
    Properties
    {
        [MainTexture] _MainTex ("Video (RenderTexture)", 2D) = "black" {}
        _Tint ("Tint (Brightness)", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off    // 双面兜底：朝向异常也不会整块消失
        ZWrite On

        Pass
        {
            Name "Default"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Tint;
            }
            ENDHLSL
        }
    }
}
