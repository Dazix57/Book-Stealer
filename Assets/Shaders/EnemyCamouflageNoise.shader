Shader "Custom/EnemyCamouflageNoise"
{
    // Variante "camuflaje" de Sprites/Default (misma base que Custom/SpriteXRay, ya probada en
    // este proyecto). En vez de una luz o un halo, cada píxel del sprite se sustituye —al azar,
    // en bloques tipo pixel-art, igual que el efecto de pixelado ya usado en la cámara principal
    // (ver Assets/Materials/PixelCamera.renderTexture, 320x180 con filtro Point)— por un color de
    // "ruido" oscuro. _Clarity (0-1, actualizado desde EnemyFacingSprite según la distancia a la
    // cámara) controla qué fracción de bloques muestra el sprite real: 0 = puro ruido (el enemigo
    // se pierde por completo, camuflado), 1 = sprite nítido, sin ruido. El ruido se recorta a la
    // silueta del sprite (tex.a) y es animado (varía con el tiempo), para que se sienta como
    // estática y no como un patrón fijo.
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Clarity ("Clarity (0 = puro ruido, 1 = nitido)", Range(0, 1)) = 1
        // Tope: aunque _Clarity llegue a 0, nunca se reemplaza el 100% del sprite por ruido en el
        // mismo instante — evita el efecto "todo el sprite titila a la vez" de antes.
        _MaxNoiseAmount ("Max Noise Amount (tope, no llega a 100%)", Range(0, 1)) = 0.35
        _NoiseColor ("Noise Color", Color) = (0.015, 0.015, 0.015, 1)
        _NoiseGridSize ("Noise Grid Size (bloques por sprite)", Float) = 12
        _NoiseSpeed ("Noise Animation Speed", Float) = 1.5
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
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float _Clarity;
            float _MaxNoiseAmount;
            fixed4 _NoiseColor;
            float _NoiseGridSize;
            float _NoiseSpeed;

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

            // Hash 2D barato (patrón conocido de "Book of Shaders"): suficiente para ruido tipo
            // estática, no necesita ser criptográficamente uniforme.
            float Hash(float2 p, float seed)
            {
                p = frac(p * float2(123.34, 456.21) + seed);
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = SampleSpriteTexture(IN.texcoord);
                fixed4 spriteColor = tex * IN.color;
                spriteColor.rgb *= spriteColor.a;

                // Cuadrícula tipo pixel-art: el ruido se decide por bloque, no por texel, para que
                // se vea como el mismo pixelado grueso que ya usa la cámara, no como grano fino.
                float2 blockUV = floor(IN.texcoord * _NoiseGridSize);
                float n = Hash(blockUV, floor(_Time.y * _NoiseSpeed));

                fixed4 noiseColor = fixed4(_NoiseColor.rgb, 1) * tex.a;

                // lejos (_Clarity bajo) = más bloques se vuelven ruido, con tope en _MaxNoiseAmount
                float noiseThreshold = min(1 - _Clarity, _MaxNoiseAmount);
                fixed4 c = (n < noiseThreshold) ? noiseColor : spriteColor;

                return c;
            }
        ENDCG
        }
    }
}
