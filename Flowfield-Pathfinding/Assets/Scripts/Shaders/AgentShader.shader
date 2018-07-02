Shader "AgentShader"
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
            #include "AutoLight.cginc"

			StructuredBuffer<float3> velocityBuffer;

			void setup()
			{
			}

            struct v2f
            {
                float4 vertex : SV_POSITION;
				fixed4 diff : COLOR0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // necessary only if you want to access instanced properties in fragment Shader.
				LIGHTING_COORDS(0, 1)
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)
           
            v2f vert(appdata_base v)
            {
				v2f o;

				TRANSFER_VERTEX_TO_FRAGMENT(o);
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
				float attenuation = LIGHT_ATTENUATION(i);
				fixed3 vel = velocityBuffer[instanceID] * i.diff * attenuation;
				return fixed4(vel, 1.0);
			}
            ENDCG
        }
    }

		Fallback "VertexLit"
}