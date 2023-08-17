Shader"Custom/NumberSurface"
{
    Properties
    {
        _MainTex( "Main Texture", 2D ) = "white" {}
        _FontTex( "Number Font (RGB)", 2D ) = "white" {}
        _MetallicGlossMap( "Metal Occlusion Gloss (RGA)", 2D ) = "black" {}
        _FontMetalGloss( "Font Metal Gloss (RA)", 2D ) = "black" {}
        _BumpMap( "Bumpmap", 2D ) = "bump" {}
        _EmissionMap( "Emission", 2D ) = "black" {}
        _FontEmission( "Font Emission", 2D ) = "black" {}
        _BlendMode( "Blending Mode", Int ) = 0
        __ColorizeWhiteLvl( "Colorize White Level", Float ) = 1.0
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

        #define MAX_DIGITS 32

        sampler2D _MainTex;
        sampler2D _FontTex;
        sampler2D_half _MetallicGlossMap;
        sampler2D _BumpMap;
        sampler2D_half _EmissionMap;

        half4 _FontSpecular[MAX_DIGITS];
        half4 _FontEmission[MAX_DIGITS];
        fixed _UseFSpec[MAX_DIGITS];
        fixed _UseFEmit[MAX_DIGITS];

        int _NDigits = 1;

        // Blending modes for fonts
        int _BlendMode = 0;
        #define BM_ADD 1
        #define BM_SUBTRACT 2
        #define BM_MULTIPY 3
        #define BM_DIVIDE 4
        #define BM_COLORIZE 5

        float _ColorizeWhiteLvl;

        // (MainTex UV space) digit boundaries
        float4 _DigitBounds[MAX_DIGITS];
        // (FontTex uv space) digit src bottom left positions
        float2 _DigitUV[MAX_DIGITS];

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
            fixed4 outCol = tex2D( _MainTex, IN.uv_MainTex );
            half4 spec = tex2D( _MetallicGlossMap, IN.uv_MainTex );
            half3 emit = tex2D( _EmissionMap, IN.uv_MainTex );

            //spec = half4(GammaToLinearSpace( spec ), spec.a);

            [unroll( MAX_DIGITS )]
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

                    // blend emission & metal/gloss based on diffuse alpha
                    if( _UseFEmit[i] ) emit = lerp( emit, _FontEmission[i].rgb, fCol.a );      // half3
                    if( _UseFSpec[i] )
                    {
                        half4 sTemp = lerp( spec, _FontSpecular[i], fCol.a );
                        spec = half4(sTemp.r, spec.g, 0, sTemp.a); // use base ao, ignore blue
                    }

                    // alpha-premultiplied overlay color
                    fixed4 fColP = fixed4(fCol.rgb * fCol.a, fCol.a);

                    switch( _BlendMode )
                    {
                    case BM_ADD:
                        outCol = outCol + fColP;
                        break;

                    case BM_SUBTRACT:
                        outCol = outCol - fColP;
                        break;

                    case BM_MULTIPY:
                        outCol = outCol * fColP;
                        break;

                    case BM_DIVIDE:
                        outCol = outCol / fColP;
                        break;

                    case BM_COLORIZE:
                        // convert base texture to grayscale, then add font color
                        // scale down gray range, shift to +- around zero & add
					    fixed baseGray = clamp(dot(outCol.rgb, fixed3(0.2126, 0.7152, 0.0722)) / _ColorizeWhiteLvl, 0, 1);
					    outCol = (outCol * (1 - fCol.a)) + (fCol * baseGray);
                        break;

                    default:
                        // normal alpha blending
                        outCol = (outCol * (1 - fCol.a)) + fColP;
                        break;
                    }

                    break;
                }
            }

            o.Albedo = outCol.rgb;
            o.Alpha = 1;

            o.Metallic = spec.r;
            o.Occlusion = spec.g;
            o.Smoothness = spec.a;
            o.Emission = emit;

            o.Normal = UnpackNormal( tex2D( _BumpMap, IN.uv_BumpMap ) );
        }
        ENDCG
    }
    FallBack "Diffuse"
}
