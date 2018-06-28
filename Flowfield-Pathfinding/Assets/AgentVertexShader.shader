Shader "SimplestInstancedShader"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
			Tags{ "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
			#pragma multi_compile_instancing
			#pragma instancing_options procedural:setup
            #include "UnityCG.cginc"
			#include "UnityLightingCommon.cginc"

			StructuredBuffer<float3> velocityBuffer;

			void setup()
			{
			}

			float3 hsv_to_rgb(float3 HSV)
			{
				float3 RGB = HSV.z;

				float var_h = HSV.x * 6;
				float var_i = floor(var_h);   // Or ... var_i = floor( var_h )
				float var_1 = HSV.z * (1.0 - HSV.y);
				float var_2 = HSV.z * (1.0 - HSV.y * (var_h - var_i));
				float var_3 = HSV.z * (1.0 - HSV.y * (1 - (var_h - var_i)));
				if (var_i == 0) { RGB = float3(HSV.z, var_3, var_1); }
				else if (var_i == 1) { RGB = float3(var_2, HSV.z, var_1); }
				else if (var_i == 2) { RGB = float3(var_1, HSV.z, var_3); }
				else if (var_i == 3) { RGB = float3(var_1, var_2, HSV.z); }
				else if (var_i == 4) { RGB = float3(var_3, var_1, HSV.z); }
				else { RGB = float3(HSV.z, var_1, var_2); }

				return (RGB);
			}

            struct v2f
            {
                float4 vertex : SV_POSITION;
				fixed4 diff : COLOR0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // necessary only if you want to access instanced properties in fragment Shader.
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)
           
            v2f vert(appdata_base v)
            {
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o); // necessary only if you want to access instanced properties in the fragment Shader.

				o.vertex = UnityObjectToClipPos(v.vertex);
				half3 worldNormal = UnityObjectToWorldNormal(v.normal);
				half nl = max(0, dot(worldNormal, _WorldSpaceLightPos0.xyz));
				o.diff = nl * _LightColor0;

				// the only difference from previous shader:
				// in addition to the diffuse lighting from the main light,
				// add illumination from ambient or light probes
				// ShadeSH9 function from UnityCG.cginc evaluates it,
				// using world space normal
				o.diff.rgb += ShadeSH9(half4(worldNormal, 1));
				return o;
            }
           
            fixed4 frag(v2f i, uint instanceID : SV_InstanceID) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); // necessary only if any instanced properties are going to be accessed in the fragment Shader.
				fixed3 vel = velocityBuffer[instanceID];
				//return fixed4(hsv_to_rgb(float3(atan2(vel.z, vel.x)/3.14159, 0.9, 0.9)), 1.0) * i.diff;
				return fixed4(vel, 1.0);
			}
            ENDCG
        }
    }
}