Shader "Custom/2DGS_Basic"
{
    Properties
    {
        _MainTex ("Brush Atlas (2x2) or Single", 2D) = "white" {}
        
        [Header(Color and Contrast)]
        _Gamma ("Gamma Correction (1.0 = Linear, 2.2 = sRGB)", Float) = 2.2 
       
        [Header(Living Canvas (Vertex Wobble))]
        [Toggle] _EnableWobble ("Enable Living Canvas", Float) = 0.0
        [Enum(Smooth Wave, 0, Choppy Boil, 1)] _WobbleType ("Wobble Style", Float) = 1.0
        _WobbleSpeed ("Wobble Speed (Acts as FPS in Mode 1)", Range(0.1, 60.0)) = 12.0
        _WobbleStrength ("Wobble Strength (Distance)", Range(0.0, 0.05)) = 0.01
        _WobbleFrequency ("Wobble Frequency (Smooth Only)", Range(0.1, 20.0)) = 5.0
        
        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        
        [Header(2x2 Atlas Settings)]
        _RatioCutoff ("Square vs Long Ratio Cutoff", Float) = 1.5
        _MidOpacityThreshold ("Mid Opacity Threshold", Range(0,1)) = 0.7
        _LowOpacityThreshold ("Low Opacity Threshold", Range(0,1)) = 0.35
        
        [Header(Size and Variance)]
        _ScaleMultiplier ("Global Brush Size", Float) = 1.0
        
        [Header(Overrides)]
        [Toggle] _SingleTextureMode ("Use Single Texture (No Atlas)", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 100

        Pass
        {
            Cull Off 
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "UnityCG.cginc"

            struct SplatData
            {
                float4 position;
                float4 rotation; 
                float4 scale;    
                float4 color;
            };

            StructuredBuffer<SplatData> _SplatBuffer;
            float4x4 _Transform;

            struct appdata
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;  
            };

            sampler2D _MainTex;
            float _Gamma; 
            
            float _EnableWobble;
            float _WobbleType;
            float _WobbleSpeed;
            float _WobbleStrength;
            float _WobbleFrequency;
            
            float _SingleTextureMode;
            float _AlphaCutoff;
            
            float _RatioCutoff;
            float _MidOpacityThreshold;
            float _LowOpacityThreshold;
            
            float _ScaleMultiplier;

            // Integer Bit-Shift Hash
            float random(uint seed) 
            { 
                seed ^= seed >> 15;
                seed *= 2079152473u;
                seed ^= seed >> 17;
                seed *= 638857285u;
                seed ^= seed >> 16;
                return float(seed) / 4294967295.0;
            }

            float3 RotateByQuaternion(float3 v, float4 q)
            {
                float3 t = 2.0 * cross(q.xyz, v);
                return v + q.w * t + cross(q.xyz, t);
            }

            v2f vert (appdata v)
            {
                v2f o;
                SplatData splat = _SplatBuffer[v.instanceID];

                float2 quadPos[6] = {
                    float2(-0.5, -0.5), float2(-0.5, 0.5), float2(0.5, 0.5),
                    float2(-0.5, -0.5), float2(0.5, 0.5), float2(0.5, -0.5)
                };
                float2 quadUV[6] = {
                    float2(0, 0), float2(0, 1), float2(1, 1),
                    float2(0, 0), float2(1, 1), float2(1, 0)
                };

                float2 vertex2D = quadPos[v.vertexID];
                float2 baseUV = quadUV[v.vertexID];

                float3 worldCenter = mul(_Transform, float4(splat.position.xyz, 1.0)).xyz;

                float width = splat.scale.x;
                float height = splat.scale.y;

                float maxSide = max(width, height);
                float minSide = max(min(width, height), 0.00001);
                float ratio = maxSide / minSide;

                float2 finalUV = baseUV;
                if (height > width) {
                    finalUV = float2(1.0 - baseUV.y, baseUV.x);
                }

                if (_SingleTextureMode > 0.5)
                {
                    o.uv = finalUV;
                }
                else
                {
                    // --- THE NEW 2x2 ATLAS LOGIC ---
                    int row = 0;
                    int col = 0;

                    if (splat.color.a < _LowOpacityThreshold)
                    {
                        row = 1; // Bottom Row
                        col = 1; // Right Column (Low Opacity)
                    }
                    else if (splat.color.a < _MidOpacityThreshold)
                    {
                        row = 1; // Bottom Row
                        col = 0; // Left Column (Mid Opacity)
                    }
                    else
                    {
                        row = 0; // Top Row (Full Opacity)
                        if (ratio > _RatioCutoff)
                        {
                            col = 1; // Right Column (Long)
                        }
                        else
                        {
                            col = 0; // Left Column (Square)
                        }
                    }

                    // Map to halves (0.5)
                    float uvOffsetY = (1 - row) * 0.5; // (1-row) keeps Row 0 at the visual TOP
                    float uvOffsetX = col * 0.5;
                    float2 inset = float2(0.005, 0.005); 
                    
                    o.uv = finalUV * (float2(0.5, 0.5) - (inset * 2.0)) + float2(uvOffsetX, uvOffsetY) + inset;
                }

                float2 scaledVertex = vertex2D * float2(width, height) * _ScaleMultiplier;
                float3 surfaceLocal = RotateByQuaternion(float3(scaledVertex.x, scaledVertex.y, 0), splat.rotation);
                float3 finalOffset = mul((float3x3)_Transform, surfaceLocal);
                
                float3 finalWorldPos = worldCenter + finalOffset;

                if (_EnableWobble > 0.5)
                {
                    float3 wobble = float3(0,0,0);
                    if (_WobbleType < 0.5)
                    {
                        float t = _Time.y * _WobbleSpeed;
                        float splatOffset = random(v.instanceID) * 6.28318; 
                        
                        wobble.x = sin(finalWorldPos.y * _WobbleFrequency + t + splatOffset) * _WobbleStrength;
                        wobble.y = cos(finalWorldPos.x * _WobbleFrequency + (t * 0.8) + splatOffset) * _WobbleStrength;
                        wobble.z = sin(finalWorldPos.z * _WobbleFrequency + (t * 1.2) + splatOffset) * _WobbleStrength;
                    }
                    else
                    {
                        uint steppedTime = (uint)floor(_Time.y * _WobbleSpeed);
                        float randX = random(v.instanceID + steppedTime * 12345u) * 2.0 - 1.0;
                        float randY = random(v.instanceID + steppedTime * 67890u) * 2.0 - 1.0;
                        float randZ = random(v.instanceID + steppedTime * 13579u) * 2.0 - 1.0;
                        
                        wobble = float3(randX, randY, randZ) * _WobbleStrength;
                    }
                    
                    finalWorldPos += wobble;
                }

                o.pos = mul(UNITY_MATRIX_VP, float4(finalWorldPos, 1.0));
                o.color = splat.color;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
{
                // 1. Sample the texture first
                fixed4 texColor = tex2D(_MainTex, i.uv);
    
                // 2. Calculate final alpha immediately
                float finalAlpha = texColor.a * i.color.a;
    
                // 3. EARLY CLIP - Discard hidden pixels before doing any expensive math!
                clip(finalAlpha - _AlphaCutoff);

                // 4. Now, do the color math ONLY for pixels that survived the clip
                float3 linearColor = texColor.rgb * i.color.rgb;
                float3 correctedColor = pow(max(linearColor, 0.0001), 1.0 / _Gamma);
    
                return fixed4(correctedColor, finalAlpha);
            }
            ENDCG
        }
    }
}