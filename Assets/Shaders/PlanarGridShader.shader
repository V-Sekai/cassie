// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/PlanarGridShader"
{
	Properties{
	  _GridThickness("Grid Thickness", Float) = 0.01
	  _GridSpacing("Grid Spacing", Float) = 10.0
	  _GridColour("Grid Colour", Color) = (0.5, 1.0, 1.0, 1.0)
	  _BaseColour("Base Colour", Color) = (0.0, 0.0, 0.0, 0.0)
	  _MaxDepth("Max Depth", Float) = 20.0
	  _DepthFadeZone("Depth Fade Zone", Float) = 5.0
	}

	SubShader{
		  Tags { "Queue" = "Transparent" }

		  Pass {
			ZWrite Off
			Cull Off
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#include "UnityCG.cginc"
			// Define the vertex and fragment shader functions
			#pragma vertex vert
			#pragma fragment frag

			// Access Shaderlab properties
			uniform float _GridThickness;
			uniform float _GridSpacing;
			uniform float4 _GridColour;
			uniform float4 _BaseColour;
			uniform float _MaxDepth;
			uniform float _DepthFadeZone;

			// Input into the vertex shader
			struct vertexInput {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			// Output from vertex shader into fragment shader
			struct vertexOutput {
			  float4 pos : SV_POSITION;
			  float2 gridPos : TEXCOORD0;
			  float depth : COLOR0;
			};

			// VERTEX SHADER
			vertexOutput vert(vertexInput input) {
			  vertexOutput output;
			  output.depth = UnityObjectToViewPos(input.vertex).z * -1.0;
			  output.pos = UnityObjectToClipPos(input.vertex);
			  // Calculate the world position coordinates to pass to the fragment shader
			  output.gridPos = input.uv;
			  return output;
			}

			// FRAGMENT SHADER
			float4 frag(vertexOutput input) : COLOR{

			float4 c = _BaseColour;
			float2 pos = input.gridPos;
			  if (frac(pos.x / _GridSpacing) < _GridThickness || frac(pos.y / _GridSpacing) < _GridThickness) {
				c = _GridColour;
			  }
			 // else {
				//return _BaseColour;
			 // }

			  if (input.depth > _MaxDepth)
			  {
				  c.a = clamp(1.0 + (_MaxDepth - input.depth) / _DepthFadeZone, 0.0, 1.0) * c.a;
			  }
			  return c;
			}

			ENDCG
		}
	}
}
