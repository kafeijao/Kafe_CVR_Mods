Shader "Noachi/WireFrame" {
    Properties {
        [HDR]_Color("Color", color) = (0,0.7,0.9,1)
        [HDR]_ColorWireFrame("ColorWireFrame", color) = (0,0.7,0.9,1)
        _Thickness("Wireframethiccccness", range(0,.333)) = .1
    }

    SubShader {

        Pass
        {
            ZWrite On
            ColorMask 0
        }
        

        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        cull off
        
        Pass {
            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma target 5.0
            

            struct appdata {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2g {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct g2f {
                float4 vertex : SV_POSITION;
                float3 barycentric : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _Color, _ColorWireFrame;
            float _Thickness;

            v2g vert (appdata v)
            {
                v2g o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            [maxvertexcount(3)]
            void geom(triangle v2g IN[3], inout TriangleStream<g2f> triStream) {
                UNITY_SETUP_INSTANCE_ID(IN[0]);
                UNITY_SETUP_INSTANCE_ID(IN[1]);
                UNITY_SETUP_INSTANCE_ID(IN[2]);

                g2f o;
                o.vertex = IN[0].vertex;
                o.barycentric = float3(1.0, 0.0, 0.0);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                triStream.Append(o);
                o.vertex = IN[1].vertex;
                o.barycentric = float3(0.0, 1.0, 0.0);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                triStream.Append(o);
                o.vertex = IN[2].vertex;
                o.barycentric = float3(0.0, 0.0, 1.0);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                triStream.Append(o);
            }
            
            float4 frag(g2f i) : SV_Target
            {
                float closest = min(i.barycentric.x, min(i.barycentric.y, i.barycentric.z));
                return lerp(_Color,_ColorWireFrame,step(closest,_Thickness));
            }
            ENDCG
        }
    }
}