Shader "Standard (Tessellation)" {
    Properties {
        _Tess ("Tessellation", Range(1,32)) = 4
        _maxDist ("Tessellation Distance", Range(0, 500.0)) = 25.0
        _MainTex ("Color (RGB)", 2D) = "white" {}
        _Physic ("Physic (RGBA)", 2D) = "white" {}
        _DispTex ("Displacement", 2D) = "gray" {}
        _NormalMap ("Normal", 2D) = "bump" {}
        _Displacement ("Displacement", Range(0, 1.0)) = 0.3
        _DispOffset ("Disp Offset", Range(0, 1)) = 0.5
        _Color ("Color", color) = (1,1,1,0)
    }

    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard addshadow fullforwardshadows vertex:disp tessellate:tessDistance
        #pragma target 5.0
        #include "Tessellation.cginc"

        struct appdata {
            float4 vertex : POSITION;
            float4 tangent : TANGENT;
            float3 normal : NORMAL;
            float2 texcoord : TEXCOORD0;
            float2 texcoord1 : TEXCOORD1;
            float2 texcoord2 : TEXCOORD2; };

        float _Tess;
        float _maxDist;

        float4 tessDistance (appdata v0, appdata v1, appdata v2) {
            return UnityDistanceBasedTess(v0.vertex, v1.vertex, v2.vertex, _maxDist * 0.2f, _maxDist * 1.2f, _Tess); }

        sampler2D _DispTex;
        sampler2D _Physic;
        uniform float4 _DispTex_ST;
        float _Displacement;
        float _DispOffset;

        void disp (inout appdata v) {
       		const float fadeOut= saturate((_maxDist - distance(mul(unity_ObjectToWorld, v.vertex), _WorldSpaceCameraPos)) / (_maxDist * 0.7f));
            float d = tex2Dlod(_DispTex, float4(v.texcoord.xy * _DispTex_ST.xy + _DispTex_ST.zw,0,0)).r * _Displacement;
            d = d * 0.5 - 0.5 +_DispOffset;
            v.vertex.xyz += v.normal * d * fadeOut;
        }

        struct Input { float2 uv_MainTex; float2 uv_Physic; };

        sampler2D _MainTex;
        sampler2D _NormalMap;
        fixed4 _Color;

        void surf (Input IN, inout SurfaceOutputStandard o) {
            half4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            half4 p = tex2D (_Physic, IN.uv_Physic);
            o.Albedo = c.rgb;
            o.Metallic = p.r; o.Smoothness = p.g; o.Occlusion = p.b;
            o.Normal = UnpackNormal(tex2D(_NormalMap, IN.uv_MainTex));
        }
        ENDCG
    }
    FallBack "Standard"
}
