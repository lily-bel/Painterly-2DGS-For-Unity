Shader "Custom/2DGS_Full"
{
    Properties
    {
        _MainTex ("Brush Atlas (2x2) or Single", 2D) = "white" {}
        
        [Header(Color and Contrast)]
        _Gamma ("Gamma Correction (1.0 = Linear, 2.2 = sRGB)", Float) = 2.2 
        _Tint ("Global Tint (Fine-tune brightness)", Color) = (1, 1, 1, 1) 
        [Toggle] _EnableLighting ("Enable Reactive Lighting", Float) = 1.0
        
        [Header(Living Canvas (Vertex Wobble))]
        [Toggle] _EnableWobble ("Enable Living Canvas", Float) = 0.0
        [Enum(Smooth Wave, 0, Choppy Boil, 1)] _WobbleType ("Wobble Style", Float) = 1.0
        _WobbleSpeed ("Wobble Speed (Acts as FPS in Mode 1)", Range(0.1, 60.0)) = 12.0
        _WobbleStrength ("Wobble Strength (Distance)", Range(0.0, 0.05)) = 0.01
        _WobbleFrequency ("Wobble Frequency (Smooth Only)", Range(0.1, 20.0)) = 5.0
        
        [Header(Transparency Handling)]
        [Toggle] _EnableDithering ("Enable Advanced Transparency", Float) = 0.0
        [Enum(Uniform Static, 0, Gaussian Grain, 1, Opacity Scale, 2)] _DitherType ("Transparency Mode", Float) = 2.0
        _DitherSpread ("Dither Amount / Spread (Modes 0 & 1)", Range(0.0, 5.0)) = 1.0
        _OpacityScaleCurve ("Opacity Scale Curve (Mode 2 only)", Range(0.1, 5.0)) = 2.0
        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        
        [Header(2x2 Atlas Settings)]
        _RatioCutoff ("Square vs Long Ratio Cutoff", Float) = 1.5
        _MidOpacityThreshold ("Mid Opacity Threshold", Range(0,1)) = 0.7
        _LowOpacityThreshold ("Low Opacity Threshold", Range(0,1)) = 0.35
        
        [Header(Size and Variance)]
        _ScaleMultiplier ("Global Brush Size", Float) = 1.0
        _FakeVariance ("Fake Size Variance", Range(0, 1)) = 0.0
        
        [Header(Target Billboarding)]
        _LookAtTarget ("Look At Target (0=Surface, 1=Target)", Range(0,1)) = 1.0
        
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
            #include "UnityLightingCommon.cginc"

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
                float3 normal : NORMAL;         
                float4 screenPos : TEXCOORD1;   
            };

            sampler2D _MainTex;
            
            float _Gamma; 
            float4 _Tint; 
            
            float _EnableWobble;
            float _WobbleType;
            float _WobbleSpeed;
            float _WobbleStrength;
            float _WobbleFrequency;
            
            float _SingleTextureMode;
            float _EnableLighting;
            float _EnableDithering;
            float _DitherType;
            float _DitherSpread;
            float _OpacityScaleCurve;
            float _AlphaCutoff;
            
            float _RatioCutoff;
            float _MidOpacityThreshold;
            float _LowOpacityThreshold;
            
            float _ScaleMultiplier;
            float _FakeVariance;
            float _LookAtTarget;

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
                float3 toTarget = normalize(_WorldSpaceCameraPos.xyz - worldCenter);

                float3 worldX = normalize(mul((float3x3)_Transform, RotateByQuaternion(float3(1,0,0), splat.rotation)));
                float3 worldY = normalize(mul((float3x3)_Transform, RotateByQuaternion(float3(0,1,0), splat.rotation)));

                float worldScale = length(mul((float3x3)_Transform, float3(1, 0, 0)));

                float apparentScaleX = length(cross(worldX, toTarget));
                float apparentScaleY = length(cross(worldY, toTarget));

                float randVal = random(v.instanceID);
                float rawWidth = lerp(splat.scale.x, splat.scale.x * (randVal * 3.0), _FakeVariance);
                float rawHeight = lerp(splat.scale.y, splat.scale.y * (random(v.instanceID + 1u) * 3.0), _FakeVariance);

                float width = lerp(rawWidth, rawWidth * apparentScaleX, _LookAtTarget);
                float height = lerp(rawHeight, rawHeight * apparentScaleY, _LookAtTarget);

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

                float opacityScaleModifier = 1.0;
                if (_EnableDithering > 0.5 && _DitherType > 1.5) 
                {
                    opacityScaleModifier = pow(max(splat.color.a, 0.0001), _OpacityScaleCurve);
                }

                float2 scaledVertex = vertex2D * float2(width, height) * _ScaleMultiplier * opacityScaleModifier;

                float3 surfaceLocal = RotateByQuaternion(float3(scaledVertex.x, scaledVertex.y, 0), splat.rotation);
                float3 surfaceOffset = mul((float3x3)_Transform, surfaceLocal);

                float3 projX = worldX - dot(worldX, toTarget) * toTarget;
                float3 projY = worldY - dot(worldY, toTarget) * toTarget;
                float3 billboardRight = length(projX) > 0.0001 ? normalize(projX) : float3(1,0,0);
                float3 billboardUp = length(projY) > 0.0001 ? normalize(projY) : float3(0,1,0);
                
                float3 billboardOffset = (scaledVertex.x * billboardRight + scaledVertex.y * billboardUp) * worldScale;

                float3 finalOffset = lerp(surfaceOffset, billboardOffset, _LookAtTarget);
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
                
                float3 localNormal = float3(0, 0, 1);
                float3 worldNormal = normalize(mul((float3x3)_Transform, RotateByQuaternion(localNormal, splat.rotation)));
                float3 billboardNormal = -toTarget; 
                o.normal = normalize(lerp(worldNormal, billboardNormal, _LookAtTarget));

                o.screenPos = ComputeScreenPos(o.pos);
                o.color = splat.color;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.uv);
                
                float3 linearColor = texColor.rgb * i.color.rgb;
                
                if (_EnableLighting > 0.5)
                {
                    float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                    float NdotL = max(0.2, abs(dot(normalize(i.normal), lightDir))); 
                    linearColor *= NdotL;
                }
                
                linearColor *= _Tint.rgb;
                float3 correctedColor = pow(max(linearColor, 0.0001), 1.0 / _Gamma);
                fixed4 finalColor = fixed4(correctedColor, texColor.a * i.color.a);

                if (_EnableDithering > 0.5)
                {
                    if (_DitherType < 0.5) 
                    {
                        float2 screenPixel = (i.screenPos.xy / i.screenPos.w) * _ScreenParams.xy;
                        float rawUniform = frac(sin(dot(screenPixel, float2(12.9898, 78.233))) * 43758.5453);
                        float ditherPattern = (rawUniform - 0.5) * _DitherSpread + _AlphaCutoff;
                        clip(finalColor.a - ditherPattern);
                    }
                    else if (_DitherType < 1.5)
                    {
                        float2 screenPixel = (i.screenPos.xy / i.screenPos.w) * _ScreenParams.xy;
                        float noise1 = frac(sin(dot(screenPixel, float2(12.9898, 78.233))) * 43758.5453);
                        float noise2 = frac(sin(dot(screenPixel, float2(39.346, 11.135))) * 29562.345);
                        float noise3 = frac(sin(dot(screenPixel, float2(73.156, 52.235))) * 64832.193);
                        
                        float rawGaussian = (noise1 + noise2 + noise3) / 3.0;
                        float ditherPattern = (rawGaussian - 0.5) * _DitherSpread + _AlphaCutoff;
                        clip(finalColor.a - ditherPattern);
                    }
                    else 
                    {
                        clip(texColor.a - _AlphaCutoff);
                    }
                }
                else
                {
                    clip(finalColor.a - _AlphaCutoff);
                }

                return finalColor;
            }
            ENDCG
        }
    }
}