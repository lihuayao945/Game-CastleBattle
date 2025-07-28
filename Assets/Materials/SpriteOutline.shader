Shader "Custom/SpriteOutline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineSize ("Outline Size", Range(0, 10)) = 1
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _OutlineColor;
            float _OutlineSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 texelSize = _MainTex_TexelSize.xy * _OutlineSize;
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // 检查周围像素
                fixed4 up = tex2D(_MainTex, i.uv + float2(0, texelSize.y));
                fixed4 down = tex2D(_MainTex, i.uv + float2(0, -texelSize.y));
                fixed4 right = tex2D(_MainTex, i.uv + float2(texelSize.x, 0));
                fixed4 left = tex2D(_MainTex, i.uv + float2(-texelSize.x, 0));

                // 如果当前像素是透明的，但周围有非透明像素，则显示边缘
                if (col.a < 0.1)
                {
                    float outline = up.a + down.a + right.a + left.a;
                    if (outline > 0.1)
                    {
                        return _OutlineColor;
                    }
                }

                return col * i.color;
            }
            ENDCG
        }
    }
} 