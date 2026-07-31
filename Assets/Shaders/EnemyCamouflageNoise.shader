Shader "Custom/EnemyDistancePixelation"
{
    // Shader unlit compatible con URP para SpriteRenderer. En vez de sustituir bloques
    // aleatorios con ruido cada frame, toma una muestra estable por cada celda de la
    // cuadrícula. Esto da pixelado real sin que la silueta del enemigo parpadee.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PixelGridSize ("Pixel grid size (0 = full resolution)", Float) = 0
        _SpriteUVRect ("Sprite UV rect", Vector) = (0,0,1,1)
        _DistanceTint ("Distance darkness tint", Color) = (0.8,0.8,0.8,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
                float _PixelGridSize;
                float4 _SpriteUVRect;
                fixed4 _DistanceTint;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float _AlphaSplitEnabled;

            fixed4 SampleSpriteTexture(float2 uv)
            {
                fixed4 color = tex2D(_MainTex, uv);

                #if ETC1_EXTERNAL_ALPHA
                fixed4 alpha = tex2D(_AlphaTex, uv);
                color.a = alpha.r;
                #endif

                return color;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 sampleUV = IN.texcoord;

                // La UV del SpriteRenderer puede ocupar solo una región de un atlas. Se lleva
                // primero al espacio local del sprite, se cuantiza allí y luego se devuelve al
                // atlas. Así 16 significa 16 x 16 celdas del sprite, no de toda la textura.
                if (_PixelGridSize > 0.5)
                {
                    float2 localUV = (IN.texcoord - _SpriteUVRect.xy) / _SpriteUVRect.zw;
                    // Evita que la arista UV exacta (1.0) salte a la celda siguiente o,
                    // en un atlas, muestree accidentalmente el sprite vecino.
                    localUV = clamp(localUV, 0.0, 0.999999);
                    localUV = (floor(localUV * _PixelGridSize) + 0.5) / _PixelGridSize;
                    sampleUV = _SpriteUVRect.xy + localUV * _SpriteUVRect.zw;
                }

                fixed4 color = SampleSpriteTexture(sampleUV) * IN.color;
                color.rgb *= color.a;
                // Tint multiplicativo: conserva los colores y el alpha originales, pero los
                // oscurece de forma estable según el nivel de distancia elegido en C#.
                color.rgb *= _DistanceTint.rgb;
                return color;
            }
            ENDCG
        }
    }
}
