Shader "Custom/test"
{

	SubShader{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
		//ZWrite On
		//ZTest LEqual
		LOD 200
		Blend SrcAlpha OneMinusSrcAlpha


		CGPROGRAM
		#pragma surface surf Lambert vertex:vert alpha:fade
		#pragma target 3.0


		struct Input {
			float4 vertColor;
		};

		void vert(inout appdata_full v, out Input o) {
			UNITY_INITIALIZE_OUTPUT(Input, o);
			o.vertColor = v.color;
			
		}

		void surf(Input IN, inout SurfaceOutput o) {
			o.Albedo = IN.vertColor.rgb;
			o.Alpha = IN.vertColor.a;
			//o.Alpha = 0.5f;
		}
		ENDCG
	}
		FallBack "Diffuse"
}
