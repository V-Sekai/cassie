Shader "Display Colored Nodes Shader"
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

		float4x4 _CanvasToWorldMatrix;

		//float _NearFade;
		//float _FarFade;
		//float _FadeZone;
		//float3 _FocusPoint;
		//float _BaseOpacity;

		struct vertexOutput
		{
			float4 pos : SV_POSITION;
			float4 color: COLOR;
		};

		struct geometryOutput
		{
			float4 pos : SV_POSITION;
			float4 color : COLOR;
		};

		//float ComputeOpacity(float3 worldSpacePos, float baseOpacity)
		//{
		//	// Compare world space input position with focus point world space position
		//	float focus_point_distance = distance(worldSpacePos, _FocusPoint);
		//	float opacity = baseOpacity;
		//	if (focus_point_distance > _FarFade)
		//	{
		//		opacity = clamp(1.0 + (_FarFade - focus_point_distance) / _FadeZone, 0.0, 1.0) * baseOpacity;
		//	}

		//	float depth = UnityObjectToViewPos(worldSpacePos).z * -1.0;
		//	if (depth < _NearFade)
		//	{
		//		opacity = clamp(-_FadeZone + depth / _NearFade, 0.0, 1.0) * baseOpacity;
		//	}

		//	return opacity;
		//}

		vertexOutput VSMain(float3 nodePos : POSITION, float4 color : COLOR)
		{
			vertexOutput v;
			v.pos = mul(_CanvasToWorldMatrix, float4(nodePos, 1.0));
			v.color = color;
			return v;
		}

		[maxvertexcount((N_VERTEX_CIRCLE + 1) * 2)]
		void geo(point vertexOutput IN[1], inout TriangleStream<geometryOutput> triStream)
		{
			float3 pos = IN[0].pos.xyz;
			float4 color = IN[0].color;
			// Compare world space input position with focus point world space position
			//float focus_point_distance = distance(pos, _FocusPoint);
			//float opacity = ComputeOpacity(pos, _BaseOpacity);
			//float opacity = 1.0;
			//if (opacity < 0.1)
			//	return; // Don't even bother drawing it

			// Draw circle in clip space, centered at input position
			float4 centerClipPos = UnityObjectToClipPos(pos);
			centerClipPos.z = centerClipPos.z - 0.001; // prevent z fighting between patch and stroke
			geometryOutput o;

			float angle = 2.0 * PI / N_VERTEX_CIRCLE;

			for (int i = 0; i <= N_VERTEX_CIRCLE; i++)
			{
				float4 onCirclePos = centerClipPos + float4(_PointRadius * cos(i * angle), _PointRadius * sin(i * angle), 0.0, 0.0);

				o.pos = onCirclePos;
				o.color = color;
				triStream.Append(o);

				o.pos = centerClipPos;
				o.color = color;
				triStream.Append(o);
			}

			
		}

		float4 PSMain(geometryOutput i) : SV_Target
		{
			float4 c = i.color;
			//c.a = i.opacity;
			return c;
		}
		ENDCG
	}

	}
}