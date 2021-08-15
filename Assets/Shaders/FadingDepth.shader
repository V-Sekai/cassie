Shader "Custom/FadingDepth"
{
    Properties
    {
		_NearFade("Near Fade Distance", Range(0.0, 0.5)) = 0.0
		_FarFade ("Far Fade Distance", Range(0.2, 1.0)) = 0.3
		_FadeZone("Size of fading zone", Range(0.01, 1.0)) = 0.5

		//_FocusPoint("Focus Point Position", Vector) = (0, 0, 0)
		_BaseOpacity("Base Opacity", Range(0, 1)) = 1.0

        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
		ZWrite Off
		ZTest LEqual
		LOD 200
		Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        // Physically based Standard lighting model
        #pragma surface surf NoLighting vertex:vert alpha:fade

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        struct Input
        {
			float opacity;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

		half _NearFade;
		half _FarFade;
		half _FadeZone;
		float3 _FocusPoint;
		float _BaseOpacity;

		//uniform float3 focusPoint;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)


		void vert(inout appdata_base v, out Input o) {
			UNITY_INITIALIZE_OUTPUT(Input, o);
			float focus_point_distance = distance(mul(unity_ObjectToWorld, v.vertex).xyz, _FocusPoint.xyz);
			float depth;
			COMPUTE_EYEDEPTH(depth);
			if (depth < _NearFade)
			{
				o.opacity = clamp(-_FadeZone + depth / _NearFade, 0.0, 1.0) * _BaseOpacity;
			}
			//else if (depth > _FarFade)
			//{
			//	o.opacity = clamp(1 + (_FarFade - depth) / _FadeZone, 0.0, 1.0);
			//}
			else if (focus_point_distance > _FarFade)
			{
				o.opacity = clamp(1 + (_FarFade - focus_point_distance) / _FadeZone, 0.0, 1.0) * _BaseOpacity;
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

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = _Color;
            o.Albedo = c.rgb;
			o.Alpha = IN.opacity;
				
        }
        ENDCG
    }
    //FallBack "Diffuse"
}
