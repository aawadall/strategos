// StrategosMapDrape.shader
// Unlit textured pass for the 3D map drape.
//
// UNLIT ON PURPOSE. The draped texture is a rendered topographic sheet and
// MapRasterizer has already baked hillshade into those pixels from the same
// elevation grid the mesh is built from. Lighting it again would double the
// shading, and it means this whole path needs no light rig, no shadow setup and
// no render-pipeline asset -- the project currently runs on the Built-in
// pipeline with no SRP assigned.
//
// No LightMode tag. Built-in runs the pass as-is; if a Universal pipeline asset
// is ever assigned, URP treats an untagged pass as SRPDefaultUnlit and this
// keeps rendering rather than turning magenta.
//
// Cull Off, deliberately. A heightfield with a skirt is a near-closed solid
// viewed from outside, so back faces are not normally visible; turning culling
// off costs nothing at this triangle count and removes an entire class of bug
// where a winding mistake makes the drape invisible from above. Do not "tidy"
// this to Cull Back without checking the mesh's winding first.

Shader "Strategos/MapDrape"
{
    Properties
    {
        _MainTex ("Drape", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Tint;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * _Tint;
            }
            ENDCG
        }
    }

    Fallback Off
}
