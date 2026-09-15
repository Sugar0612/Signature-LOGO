Shader "SignatureLogo/SpriteOutline"
{
    // 签名描边着色器：在字迹周围画一圈深色描边（字幕同款做法），
    // 让签名贴在视频等明亮背景上依然清晰可读；黑背景上描边自然不可见、无副作用。
    // 用法：SpriteRenderer.sharedMaterial 指向本材质，SpriteRenderer 会按渲染器自动覆写 _MainTex。
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 0.85)
        _OutlineWidth ("Outline Width (Texels)", Range(0, 8)) = 2.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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
                float4 _MainTex_TexelSize; // SpriteRenderer 覆写贴图时会同步更新
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR; // SpriteRenderer.color（整体染色）走顶点色传入
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half SampleAlpha (float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // 8 方向取邻域 alpha 最大值 = 描边覆盖（对角按 0.707 折算，轮廓更圆润）
                float2 t = _MainTex_TexelSize.xy * _OutlineWidth;
                half oa = SampleAlpha(IN.uv + float2(t.x, 0.0));
                oa = max(oa, SampleAlpha(IN.uv - float2(t.x, 0.0)));
                oa = max(oa, SampleAlpha(IN.uv + float2(0.0, t.y)));
                oa = max(oa, SampleAlpha(IN.uv - float2(0.0, t.y)));
                float2 d = t * 0.7071;
                oa = max(oa, SampleAlpha(IN.uv + d));
                oa = max(oa, SampleAlpha(IN.uv - d));
                oa = max(oa, SampleAlpha(IN.uv + float2(d.x, -d.y)));
                oa = max(oa, SampleAlpha(IN.uv - float2(d.x, -d.y)));

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half a = tex.a;
                half3 glyph = IN.color.rgb * tex.rgb;
                half outlineA = _OutlineColor.a * oa * (1.0 - a);

                // 字迹叠在描边之上（直接 alpha 合成：rgb 与 a 分开算再还原）
                half4 result;
                result.a = a + outlineA;
                result.rgb = (glyph * a + _OutlineColor.rgb * outlineA) / max(result.a, 1e-4);
                return result;
            }
            ENDHLSL
        }
    }
}
