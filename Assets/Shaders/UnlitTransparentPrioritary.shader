Shader "Custom/UnlitTransparentPrioritary"
{
    Properties
    {
        _BaseOpacity("Base Opacity", Range(0, 1)) = 1.0
        _Color("Color", Color) = (1,1,1,1)
        _ZFightingOffset("Z Fighting Offset", Float) = 0.0001
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" }
        ZWrite Off
        ZTest Always
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Stencil
        {
            Ref 1
            Comp Always
            Pass Replace
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            fixed4 _Color;
            float _BaseOpacity;
            float _ZFightingOffset;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.vertex.z = o.vertex.z - _ZFightingOffset; // prevent z fighting between patch and stroke
                //o.vertex.z = o.vertex.z - 0.0001f; // prevent z fighting between patch and stroke
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = _Color;
                c.a = _BaseOpacity;

                return c;
            }
            ENDCG
        }
    }
}
