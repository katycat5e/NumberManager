Shader "Custom/NumSurface"
{
    Properties
    {
        _MainTex( "Albedo (RGB)", 2D ) = "white" {}
        _FontTex( "Number Font (RGB)", 2D ) = "white" {}
        _MetallicGlossMap( "Metal Occlusion Gloss (RGA)", 2D ) = "white" {}
        _BumpMap( "Bumpmap", 2D ) = "bump" {}
        _EmissionMap( "Emission", 2D ) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _FontTex;
        sampler2D_half _MetallicGlossMap;
        sampler2D _BumpMap;
        sampler2D_half _EmissionMap;

        int _NDigits = 1;
        // (MainTex UV space) digit boundaries
        float4 _DigitBounds[32];
        // (FontTex uv space) digit src bottom left positions
        float2 _DigitUV[32];

        float2 _FontTransform;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
        };

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        //UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        //UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D( _MainTex, IN.uv_MainTex );
            half4 s = tex2D( _MetallicGlossMap, IN.uv_MainTex );
            half4 e = tex2D( _EmissionMap, IN.uv_MainTex );

            s = half4(GammaToLinearSpace( s ), s.a);
            half3 emit = GammaToLinearSpace( e );

            [unroll( 32 )]
            for( int i = 0; i < _NDigits; i++ )
            {
                // vector (bottomLeft -> input.uv, topRight -> input.uv)
                float4 vComp = IN.uv_MainTex.xyxy - _DigitBounds[i];
                float4 vTest = sign( vComp );

                if( (vTest.x + vTest.y > 0) && (vTest.z + vTest.w < 0) )
                {
                    // get offset from bottom left of digit
                    float2 fontUV = vComp.xy;
                    fontUV = fontUV * _FontTransform;

                    // font texture UV
                    fontUV = fontUV + _DigitUV[i];
                    fixed4 fCol = tex2D( _FontTex, fontUV );

                    c = (c * (1 - fCol.a)) + (fCol * fCol.a);

                    break;
                }
            }

            o.Albedo = c.rgb;

            o.Alpha = 1;

            o.Metallic = s.r;
            o.Occlusion = s.g;
            o.Smoothness = s.a;
            o.Emission = emit;

            o.Normal = UnpackNormal( tex2D( _BumpMap, IN.uv_BumpMap ) );
        }
        ENDCG
    }
    FallBack "Diffuse"
}
