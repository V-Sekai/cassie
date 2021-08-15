Shader "Display Nodes Shader"
{

	Subshader
	{

		Tags { "Queue" = "Overlay-1" "RenderType" = "Transparent" }
		ZWrite Off
		ZTest LEqual
		LOD 200
		Blend SrcAlpha OneMinusSrcAlpha

	Pass
	{
		Cull Off
		CGPROGRAM
		#include "UnityCG.cginc"
		#pragma vertex VSMain
		#pragma geometry geo
		#pragma fragment PSMain
		#pragma target 5.0

		#define PI 3.1415
		#define N_VERTEX_CIRCLE 20


		float _PointRadius;

		fixed4 _Color;
		float _BaseOpacity;

		struct geometryOutput
		{
			float4 pos : SV_POSITION;
			float opacity : COLOR0;
		};

		float4 VSMain(float3 nodePos : POSITION) : SV_POSITION
		{

			return float4(nodePos, 1.0);
		}

		float4 ScreenScale(float4 vec)
		{
			vec.x = vec.x / _ScreenParams.x;
			vec.y = vec.y / _ScreenParams.y;
			return vec;
		}

		[maxvertexcount((N_VERTEX_CIRCLE + 1) * 2)]
		void geo(point float4 IN[1] : SV_POSITION, inout TriangleStream<geometryOutput> triStream)
		{
			float3 pos = IN[0].xyz;

			// Draw circle in clip space, centered at input position
			float4 centerClipPos = UnityObjectToClipPos(pos);
			centerClipPos.z = centerClipPos.z - 0.001; // prevent z fighting between patch and stroke
			geometryOutput o;

			float angle = 2.0 * PI / N_VERTEX_CIRCLE;

			for (int i = 0; i <= N_VERTEX_CIRCLE; i++)
			{
				float4 onCirclePos = centerClipPos + ScreenScale(float4(_PointRadius * cos(i * angle), _PointRadius * sin(i * angle), 0.0, 0.0));

				o.pos = onCirclePos;
				o.opacity = _BaseOpacity;
				triStream.Append(o);

				o.pos = centerClipPos;
				o.opacity = _BaseOpacity;
				triStream.Append(o);
			}

			
		}

		float4 PSMain(geometryOutput i) : SV_Target
		{
			float4 c = _Color;
			c.a = i.opacity;
			return c;
		}
		ENDCG
	}

	}
}