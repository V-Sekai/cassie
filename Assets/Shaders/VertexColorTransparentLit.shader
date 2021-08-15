Shader "Custom/VertexColorTransparentLit"
{

	SubShader{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
		ZWrite Off
		ZTest LEqual
		LOD 200
		Blend SrcAlpha OneMinusSrcAlpha

		// extra pass that renders to depth buffer only,
		// if the surface is opaque
		Pass {
			ZWrite On
			ZTest LEqual
			ColorMask 0


			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#include "UnityCG.cginc"

				struct v2f {
					float4 color : COLOR;
					float4 pos : SV_POSITION;
				};

				v2f vert(appdata_full v) {
					v2f o;
					o.pos = UnityObjectToClipPos(v.vertex);
					o.color = v.color;
					//o.color.w = 1.0;
					return o;
				}

				half4 frag(v2f i) : SV_Target{

					if (i.color.a < 0.5)
						discard;
					return i.color;
				}
			ENDCG
		}

		ZWrite Off
		ZTest LEqual

		CGPROGRAM
		#pragma surface surf NoLighting vertex:vert alpha:fade
		#pragma target 3.0


		struct Input {
			float4 vertColor;
		};

		void vert(inout appdata_full v, out Input o) {
			UNITY_INITIALIZE_OUTPUT(Input, o);
			o.vertColor = v.color;
			
		}

		fixed4 LightingNoLighting(SurfaceOutput s, fixed3 lightDir, fixed atten)
		{
			fixed4 c;
			c.rgb = s.Albedo; 
			c.a = s.Alpha;
			return c;
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
