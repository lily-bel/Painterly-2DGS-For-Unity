using UnityEngine;
using UnityEditor;

public class DitheredMipmapGenerator : EditorWindow {
    public enum MipMethod {
        NearestNeighbor,
        StochasticDither
    }

    private Texture2D sourceTexture; // Mip 0
    private Texture2D[] customMips = new Texture2D[8]; // Indices 1 through 7 are used

    private string savePath = "Assets/DitheredTexture.asset";

    private MipMethod generationMethod = MipMethod.NearestNeighbor;
    private int horizontalStretch = 4;
    private bool ditherBaseTexture = false;

    // Scroll position for the window in case it gets tall
    private Vector2 scrollPos;

    [MenuItem("Tools/Dithered Mipmap Generator")]
    public static void ShowWindow() {
        GetWindow<DitheredMipmapGenerator>("Dither Mipmaps");
    }

    private void OnGUI() {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("Source Textures", EditorStyles.boldLabel);
        sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Mip 0 (Base - Required)", sourceTexture, typeof(Texture2D), false);

        EditorGUILayout.Space();
        GUILayout.Label("Custom Mipmaps (Optional)", EditorStyles.miniBoldLabel);
        GUILayout.Label("Empty slots will be auto-generated from the nearest filled slot above them.", EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space();

        // Dynamically create slots 1 through 7
        for (int i = 1; i < 8; i++) {
            int divisor = 1 << i; // calculates 2, 4, 8, 16, etc.
            customMips[i] = (Texture2D)EditorGUILayout.ObjectField($"Mip {i} (1/{divisor} size)", customMips[i], typeof(Texture2D), false);
        }

        EditorGUILayout.Space();
        savePath = EditorGUILayout.TextField("Save Path", savePath);

        EditorGUILayout.Space();

        GUILayout.Label("Generation Settings", EditorStyles.boldLabel);

        generationMethod = (MipMethod)EditorGUILayout.EnumPopup(new GUIContent("Generation Method", "NearestNeighbor preserves exact shapes without blurring. StochasticDither averages colors and applies noise."), generationMethod);

        if (generationMethod == MipMethod.StochasticDither) {
            ditherBaseTexture = EditorGUILayout.Toggle(new GUIContent("Dither Custom Inputs", "Applies the stochastic dither to Mip 0 and any custom mips provided."), ditherBaseTexture);
            horizontalStretch = EditorGUILayout.IntSlider(new GUIContent("Horizontal Noise Stretch", "Increases the horizontal grouping of the dither pattern."), horizontalStretch, 1, 16);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate Asset", GUILayout.Height(30))) {
            if (sourceTexture == null) {
                Debug.LogError("Please assign a base source texture (Mip 0). Make sure all assigned textures have Read/Write Enabled.");
                return;
            }

            GenerateTexture();
        }

        EditorGUILayout.EndScrollView();
    }

    private void GenerateTexture() {
        int width = sourceTexture.width;
        int height = sourceTexture.height;

        Texture2D newTex = new Texture2D(width, height, TextureFormat.RGBA32, true);

        // --- Process Mip 0 ---
        Color[] prevPixels = sourceTexture.GetPixels(0);

        if (generationMethod == MipMethod.StochasticDither && ditherBaseTexture) {
            ApplyDitherToPixels(prevPixels, width, height, 0);
        }

        newTex.SetPixels(prevPixels, 0);

        int mipCount = newTex.mipmapCount;

        // --- Process Remaining Mips ---
        for (int m = 1; m < mipCount; m++) {
            int prevW = Mathf.Max(1, width >> (m - 1));
            int prevH = Mathf.Max(1, height >> (m - 1));

            int currW = Mathf.Max(1, width >> m);
            int currH = Mathf.Max(1, height >> m);

            // Grab the custom texture if we have one in the array (and we aren't past the array limit)
            Texture2D customTex = (m < 8) ? customMips[m] : null;
            Color[] currPixels = null;

            if (customTex != null) {
                if (customTex.width == currW && customTex.height == currH) {
                    currPixels = customTex.GetPixels(0);

                    if (generationMethod == MipMethod.StochasticDither && ditherBaseTexture) {
                        ApplyDitherToPixels(currPixels, currW, currH, m);
                    }
                } else {
                    Debug.LogWarning($"[Dither Tool] Custom Mip {m} ignored! Expected size {currW}x{currH}, but got {customTex.width}x{customTex.height}. Falling back to generation.");
                }
            }

            // --- Auto-Generate Downsampled Mip (If no valid custom texture was found) ---
            if (currPixels == null) {
                currPixels = new Color[currW * currH];

                for (int y = 0; y < currH; y++) {
                    for (int x = 0; x < currW; x++) {
                        if (generationMethod == MipMethod.NearestNeighbor) {
                            // Nearest Neighbor Logic (GIMP 'None' Style)
                            int px = Mathf.FloorToInt(((float)x / currW) * prevW);
                            int py = Mathf.FloorToInt(((float)y / currH) * prevH);

                            currPixels[y * currW + x] = GetPixelClamp(prevPixels, prevW, prevH, px, py);
                        } else {
                            // Averaged + Stochastic Dither Logic
                            int px = x * 2;
                            int py = y * 2;

                            Color c00 = GetPixelClamp(prevPixels, prevW, prevH, px, py);
                            Color c10 = GetPixelClamp(prevPixels, prevW, prevH, px + 1, py);
                            Color c01 = GetPixelClamp(prevPixels, prevW, prevH, px, py + 1);
                            Color c11 = GetPixelClamp(prevPixels, prevW, prevH, px + 1, py + 1);

                            float weightSum = c00.a + c10.a + c01.a + c11.a;
                            float avgR, avgG, avgB;

                            if (weightSum > 0.001f) {
                                avgR = (c00.r * c00.a + c10.r * c10.a + c01.r * c01.a + c11.r * c11.a) / weightSum;
                                avgG = (c00.g * c00.a + c10.g * c10.a + c01.g * c01.a + c11.g * c11.a) / weightSum;
                                avgB = (c00.b * c00.a + c10.b * c10.a + c01.b * c01.a + c11.b * c11.a) / weightSum;
                            } else {
                                avgR = (c00.r + c10.r + c01.r + c11.r) * 0.25f;
                                avgG = (c00.g + c10.g + c01.g + c11.g) * 0.25f;
                                avgB = (c00.b + c10.b + c01.b + c11.b) * 0.25f;
                            }

                            float avgA = weightSum * 0.25f;
                            float threshold = GetSpatialNoise(x, y, m, horizontalStretch);
                            float ditheredAlpha = avgA > threshold ? 1.0f : 0.0f;

                            currPixels[y * currW + x] = new Color(avgR, avgG, avgB, ditheredAlpha);
                        }
                    }
                }
            }

            // Apply pixels to the texture map
            newTex.SetPixels(currPixels, m);

            // CRITICAL: We pass currPixels forward to become prevPixels for the next loop.
            // This guarantees the algorithm ALWAYS daisy-chains from the most recently established mip level.
            prevPixels = currPixels;
        }

        newTex.Apply(false);
        AssetDatabase.CreateAsset(newTex, savePath);
        AssetDatabase.SaveAssets();

        EditorGUIUtility.PingObject(newTex);
        Debug.Log($"Custom texture saved to: {savePath} using {generationMethod}");
    }

    private void ApplyDitherToPixels(Color[] pixels, int width, int height, int mipLevel) {
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int index = y * width + x;
                Color c = pixels[index];

                float threshold = GetSpatialNoise(x, y, mipLevel, horizontalStretch);
                float ditheredAlpha = c.a > threshold ? 1.0f : 0.0f;

                pixels[index] = new Color(c.r, c.g, c.b, ditheredAlpha);
            }
        }
    }

    private Color GetPixelClamp(Color[] pixels, int width, int height, int x, int y) {
        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);
        return pixels[y * width + x];
    }

    private float GetSpatialNoise(int x, int y, int mipLevel, int stretchX) {
        int groupX = x / stretchX;
        float sn = Mathf.Sin(groupX * 12.9898f + y * 78.233f + mipLevel * 37.719f) * 43758.5453123f;
        return sn - Mathf.Floor(sn);
    }
}