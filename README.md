# Painterly-2DGS-For-Unity
A fast 2DGS implementation for Unity with brushstroke texture options.

<img width="1212" height="570" alt="splatgif" src="https://github.com/user-attachments/assets/32bfc3a4-118e-4e07-9479-1fe83501b9ef" />

## How to use
Download the package and import into Unity. This shader works on **.ply files**. You may need to reimport the ply files for the editor scripts to take effect.
Add the Point Cloud Renderer runtime script to your .ply object and assign a shader to the component.

## Shaders
4 shaders are included.
-**2DGS** - This is an implementation of 2DGS using dithering instead of transparency.
-**2DGS Fast** - A faster implementation of dithered 2DGS. Uses a faster dither falloff so the texture can be smaller while retaining similar coverage.
-**2DGS Paint** - 2DGS with brushstrokes. Uses a 2x2 texture atlas to represent long/short/solid/faint splats with different brushstrokes.
-**2DGS Atlas Test** - Test shader for seeing texture atlas ranges.

# Other Contents
- The package contains an editor script (Tools -> Dithered Mipmap Generator) to generate mipmaps with more precision. These act as normal textures after generation. The tool has options for how the scaling is done to preserve dithering. Horizontal noise stretch can be set (around 8 works best) to preserve the streakiness of the texture at smaller sizes. Set the filter mode of the resulting file to Point.
- 2DGS Test scene has several 2DGS .ply files for testing. Models are in the Point Clouds folder.

# Aknowledgements
Point cloud rendering based on [https://github.com/keijiro/pcx](keijiro's pcx shader).
2DGS test .ply files from [https://huggingface.co/datasets/nerfbaselines/nerfbaselines-supplementary/tree/49f0235911db92781c172ffe14a837af3a96bf1a/scaffold-gs](Nerf Baselines).
