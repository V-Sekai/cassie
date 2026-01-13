Shader "Custom/ModelShader"
{
    Properties
    {
        _NearFadeEye("Near Fade Eye Distance", Range(0.0, 0.5)) = 0.0
        _NearFadeHand("Near Fade Hand Distance", Range(0.2, 1.0)) = 0.3
        _FadeZone("Size of fading zone", Range(0.01, 1.0)) = 0.5

        _BaseOpacity("Base Opacity", Range(0, 1)) = 1.0
        _MinOpacity("Minimum Opacity", Range(0, 1)) = 0.2

        _Color("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        ZWrite Off
        ZTest LEqual
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

        Stencil
        {
            Ref 0
            Comp Equal
        }


        CGPROGRAM
        #pragma surface surf NoLighting vertex:vert alpha:fade

        #pragma target 3.0

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        half _NearFadeEye;
        half _NearFadeHand;
        half _FadeZone;
        float3 _FocusPoint;
        float _BaseOpacity;
        float _MinOpacity;

        //uniform float3 focusPoint;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        struct Input
        {
            float opacity;
        };


        void vert(inout appdata_base v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float focus_point_distance = distance(mul(unity_ObjectToWorld, v.vertex).xyz, _FocusPoint.xyz);
            float depth;
            COMPUTE_EYEDEPTH(depth);
            if (depth < _NearFadeEye)
            {
                o.opacity = clamp(-_FadeZone + depth / _NearFadeEye, _MinOpacity, 1.0) * _BaseOpacity;
            }
            else if (focus_point_distance < _NearFadeHand)
            {
                o.opacity = clamp(focus_point_distance / _NearFadeHand, _MinOpacity, 1.0) * _BaseOpacity;
            }
            else
            {
                o.opacity = _BaseOpacity;
            }
        }

        fixed4 LightingNoLighting(SurfaceOutput s, fixed3 lightDir, fixed atten)
        {
            fixed4 c;
            c.rgb = s.Albedo;
            c.a = s.Alpha;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 c = _Color;
            o.Albedo = c.rgb;
            o.Alpha = IN.opacity;

        }

        ENDCG
        
    }
}
