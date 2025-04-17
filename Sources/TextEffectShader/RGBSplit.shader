Shader "CustomTMP/RGBSplit" 
{
	Properties 
	{
		[HDR]_FaceColor ("Face Color", Color) = (1,1,1,1)
		_MainTex ("Font Atlas", 2D) = "white" {}
		_TextureWidth ("Texture Width", float) = 512
		_TextureHeight ("Texture Height", float) = 512
		_GradientScale ("Gradient Scale", float) = 5.0

		_WeightNormal ("Weight Normal", float) = 0
		_WeightBold ("Weight Bold", float) = 0.5

		_ScaleRatioA ("Scale RatioA", float) = 1
		_ScaleRatioB ("Scale RatioB", float) = 1
		_ScaleRatioC ("Scale RatioC", float) = 1

		_PerspectiveFilter	("Perspective Correction", Range(0, 1)) = 0.875
		_ClipRect ("Clip Rect", vector) = (-32767, -32767, 32767, 32767)

		_Sharpness ("Sharpness", Range(-1,1)) = 0
		[HDR]_UnderlayColor	("Border Color", Color) = (0,0,0, 0.5)
		_UnderlayOffsetX ("Border OffsetX", Range(-1,1)) = 512
		_UnderlayOffsetY ("Border OffsetY", Range(-1,1)) = 512
		_UnderlayDilate	("Border Dilate", Range(-1,1)) = 0
		_UnderlaySoftness ("Border Softness", Range(0,1)) = 0
		[HDR]_OutlineColor ("Outline Color", Color) = (0,0,0,1)
		_OutlineWidth ("Outline Thickness", Range(0, 1)) = 0
		_OutlineSoftness ("Outline Softness", Range(0,1)) = 0
		_MaskSoftnessX ("Mask SoftnessX", float) = 0
		_MaskSoftnessY ("Mask SoftnessY", float) = 0

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_SplitOffsetX ("Split OffsetX", Range(-0.5, 0.5)) = 0
		_SplitOffsetY ("Split OffsetY", Range(-0.5, 0.5)) = 0
		_Amplitude ("Split Amplitude", Range(0.5, 10)) = 2.5
	}

	SubShader 
	{
		Tags
		{
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"RenderType"="Transparent"
		}

		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}
		Cull Off
		ZWrite Off
		Lighting Off
		Fog { Mode Off }
		ZTest [unity_GUIZTestMode]
		Blend One OneMinusSrcAlpha

		HLSLINCLUDE
		
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

		CBUFFER_START(UnityPerMaterial)	
			float4 _FaceColor;
			float4 _MainTex_ST;
			float _TextureWidth;
			float _TextureHeight;
			float _GradientScale;
			float _Sharpness;
			float _PerspectiveFilter;
			float _WeightNormal;
			float _WeightBold;
			float _ScaleRatioA;
			float _ScaleRatioB;
			float _ScaleRatioC;
			float4 _OutlineColor;
			float _OutlineWidth;
			float _OutlineSoftness;
			float4 _UnderlayColor;
			float _UnderlayOffsetX;
			float _UnderlayOffsetY;
			float _UnderlayDilate;
			float _UnderlaySoftness;
			float4 _ClipRect;
			float _MaskSoftnessX;
			float _MaskSoftnessY;
			float _StencilComp;
			float _Stencil;
			float _StencilOp;
			float _StencilWriteMask;
			float _SplitOffsetX;
			float _SplitOffsetY;
			float _Amplitude;
		CBUFFER_END

		ENDHLSL

		Pass 
		{
			HLSLPROGRAM
			#pragma target 3.0
			#pragma vertex VertShader
			#pragma fragment PixShader
			#pragma shader_feature __ UNDERLAY_ON
			#pragma shader_feature __ OUTLINE_ON
			#pragma shader_feature __ SPLIT_ON

			#pragma multi_compile __ UNITY_UI_CLIP_RECT
			#pragma multi_compile __ UNITY_UI_ALPHACLIP

			struct vertex_t
			{
				UNITY_VERTEX_INPUT_INSTANCE_ID
				float4 position : POSITION;
				float3 normal : NORMAL;
				float4 color : COLOR;
				float4 texcoord0 : TEXCOORD0;
				float2 texcoord1 : TEXCOORD1;
			};

			struct pixel_t 
			{
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
				float4 position : SV_POSITION;
				float4 color : COLOR;
				float2 atlas : TEXCOORD0;		// Atlas
				float4 param : TEXCOORD1;		// alphaClip, scale, bias, weight
				float4 mask : TEXCOORD2;		// Position in object space(xy), pixel Size(zw)
				float3 viewDir : TEXCOORD3;

				#if UNDERLAY_ON
					float4 texcoord2 : TEXCOORD4;		// u,v, scale, bias
					float4 underlayColor : COLOR1;
				#endif
				float4 texcoord3: TEXCOORD5;
			};

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			float3x3 _EnvMatrix;

			float4 GetColor(float d, float4 faceColor, float4 outlineColor, float outline, float softness)
			{
				float faceAlpha = 1 - saturate((d - outline * 0.5 + softness * 0.5) / (1.0 + softness));
				float outlineAlpha = saturate((d + outline * 0.5)) * sqrt(min(1.0, outline));

				faceColor.rgb *= faceColor.a;
				outlineColor.rgb *= outlineColor.a;
				faceColor = lerp(faceColor, outlineColor, outlineAlpha);
				faceColor *= faceAlpha;
				
				return faceColor;
			}

			float4 GetColor(float d, float4 faceColor, float softness)
			{
				float faceAlpha = 1 - saturate((d + softness * 0.5) / (1.0 + softness)); 
				faceColor.rgb *= faceColor.a;
				faceColor *= faceAlpha;
				
				return faceColor;
			}

			pixel_t VertShader(vertex_t input)
			{
				pixel_t output;

				ZERO_INITIALIZE(pixel_t, output);
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input,output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float bold = step(input.texcoord0.w, 0);

				float4 vert = input.position;
				float4 vPosition = TransformObjectToHClip(vert.xyz);

				float2 pixelSize = vPosition.w;
				pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
				float scale = rsqrt(dot(pixelSize, pixelSize));
				scale *= abs(input.texcoord0.w) * _GradientScale * (_Sharpness + 1);
				if (UNITY_MATRIX_P[3][3] == 0)
				{
					scale = lerp(abs(scale) * (1 - _PerspectiveFilter), scale, abs(dot(TransformObjectToWorldNormal(input.normal.xyz), normalize(GetCameraPositionWS() -  mul(unity_ObjectToWorld, vert).xyz)))); // GetCameraPositionWS() - i.worldPos
				}

				float weight = lerp(_WeightNormal, _WeightBold, bold) / 4.0;
				weight = weight * _ScaleRatioA * 0.5;

				float bias = (.5 - weight) + (.5 / scale);

				float alphaClip = (1.0 - _OutlineWidth * _ScaleRatioA - _OutlineSoftness * _ScaleRatioA);
				alphaClip = alphaClip / 2.0 - ( .5 / scale) - weight;

				#if UNDERLAY_ON
					float4 underlayColor = _UnderlayColor;
					underlayColor.rgb *= underlayColor.a;
					
					float bScale = scale;
					bScale /= 1 + ((_UnderlaySoftness * _ScaleRatioC) * bScale);
					float bBias = (0.5 - weight) * bScale - 0.5 - ((_UnderlayDilate * _ScaleRatioC) * 0.5 * bScale);

					float x = -(_UnderlayOffsetX * _ScaleRatioC) * _GradientScale / _TextureWidth;
					float y = -(_UnderlayOffsetY * _ScaleRatioC) * _GradientScale / _TextureHeight;
					float2 bOffset = float2(x, y);
				#endif
				
				#if SPLIT_ON
					float sScale = scale;
					float sBias = (0.5 - weight) * sScale - 0.5 - ((0.2 * _ScaleRatioC) * 0.5 * sScale);
					float2 sOffset = float2(-(_SplitOffsetX * _ScaleRatioC) * _GradientScale / _TextureWidth, -(_SplitOffsetY * _ScaleRatioC) * _GradientScale / _TextureHeight);
					
					float splitAmount = (1.0 + sin(_Time.y * 6.0)) * 0.5;
					splitAmount *= 1.0 + sin(_Time.y * 16.0) * 0.5;
					splitAmount *= 1.0 + sin(_Time.y * 19.0) * 0.5;
					splitAmount *= 1.0 + sin(_Time.y * 27.0) * 0.5;
					splitAmount = pow(splitAmount, _Amplitude) * 0.05;
					splitAmount /= max(_TextureHeight, _TextureWidth);
					sOffset += splitAmount;
				#endif

				float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
				output.position = vPosition;
				output.color = input.color;
				output.atlas = input.texcoord0;
				output.param = float4(alphaClip, scale, bias, weight);
				output.mask = float4(vert.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * float2(_MaskSoftnessX, _MaskSoftnessY) + pixelSize.xy));
				output.viewDir = mul((float3x3)_EnvMatrix, _WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld, vert).xyz);
				#if UNDERLAY_ON
					output.texcoord2 = float4(input.texcoord0 + bOffset, bScale, bBias);
					output.underlayColor = underlayColor;
				#endif
				#if SPLIT_ON
					output.texcoord3 = float4(sOffset, sScale, sBias);
				#endif
				return output;
			}

			float4 PixShader(pixel_t input) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);

				float c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.atlas).a;

				#if !defined(UNDERLAY_ON) && !defined(SPLIT_ON)
					clip(c - input.param.x);
				#endif

				float scale = input.param.y;
				float bias = input.param.z;
				float weight = input.param.w;
				float sd = (bias - c) * scale;

				float softness = (_OutlineSoftness * _ScaleRatioA) * scale;
				float4 faceColor = _FaceColor;
				faceColor.rgb *= input.color.rgb;
				faceColor = GetColor(sd, faceColor, softness);
				
				#if OUTLINE_ON
					float4 outlineColor = _OutlineColor;
					float outline = (_OutlineWidth * _ScaleRatioA) * scale;
					faceColor = GetColor(sd, faceColor, outlineColor, outline, softness);
				#endif

				#if UNDERLAY_ON
					float d = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord2.xy).a * input.texcoord2.z;
					faceColor += input.underlayColor * saturate(d - input.texcoord2.w) * (1 - faceColor.a);
				#endif

				#if SPLIT_ON
					float s_red = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.atlas + input.texcoord3.xy).a * input.texcoord3.z;
					faceColor += float4(0.9553, 0.0862, 0.3411, 0.75) * saturate(s_red - input.texcoord3.w) * (1 - faceColor.a);
					float s_blue = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.atlas - input.texcoord3.xy).a * input.texcoord3.z;
					faceColor += float4(0.35, 1, 1, 0.75) * saturate(s_blue - input.texcoord3.w) * (1 - faceColor.a);
				#endif

				// Alternative implementation to UnityGet2DClipping with support for softness.
				#if UNITY_UI_CLIP_RECT
					float2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
					faceColor *= m.x * m.y;
				#endif

				#if UNITY_UI_ALPHACLIP
					clip(faceColor.a - 0.001);
				#endif

				return faceColor * input.color.a;
			}

			ENDHLSL
		}
	}
	Fallback "TextMeshPro/Mobile/Distance Field"
}
