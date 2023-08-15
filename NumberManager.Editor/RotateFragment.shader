Shader "Custom/RotateFragment"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Rotate ("Rotate", Int) = 0
        _InvertX ("Invert x", Int) = 0
        _InvertY ("Invert y", Int) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert alpha
            #pragma fragment frag alpha

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float _Rotate;
            float _InvertX;
            float _InvertY;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 invert = {1 - _InvertX, 1 - _InvertY};
                float2 newUV = abs(1 - i.uv - invert);

                float2 swapped = {1 - newUV.y, newUV.x};
                float2 rotated = swapped * _Rotate + newUV * (1 - _Rotate);

                fixed4 c = tex2D (_MainTex, rotated);
                return c;
            }
            ENDCG
        }
    }
}
