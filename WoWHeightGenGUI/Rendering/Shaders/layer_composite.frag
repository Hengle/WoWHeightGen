#version 330 core

in vec2 vTexCoord;
out vec4 fragColor;

// Layer textures
uniform sampler2D uMinimapTex;
uniform sampler2D uHeightTex;
uniform sampler2D uAreaTex;

// Colormap texture (1D, contains all colormap presets)
uniform sampler1D uColormapTex;

// Per-layer settings (x=minimap, y=height, z=area)
uniform vec3 uOpacities;
uniform ivec3 uVisibilities;
uniform ivec3 uBlendModes;

// Height layer settings
uniform int uColormapType;      // 0=grayscale, 1=terrain, 2=viridis, 3=heatmap
uniform int uColormapSize;      // Samples per colormap (typically 256)

// Area layer settings
uniform int uAreaHighlightMode; // 0=show all, 1=highlight only specified areas
uniform ivec4 uHighlightAreas1; // First 4 area IDs to highlight
uniform ivec4 uHighlightAreas2; // Next 4 area IDs to highlight
uniform int uHighlightCount;    // Number of areas to highlight

// Blend mode constants
const int BLEND_NORMAL = 0;
const int BLEND_MULTIPLY = 1;
const int BLEND_SCREEN = 2;
const int BLEND_OVERLAY = 3;
const int BLEND_SOFT_LIGHT = 4;
const int BLEND_HARD_LIGHT = 5;

// Apply blend mode between base color and blend color
vec3 applyBlendMode(vec3 base, vec3 blend, int mode)
{
    if (mode == BLEND_NORMAL)
    {
        return blend;
    }
    else if (mode == BLEND_MULTIPLY)
    {
        return base * blend;
    }
    else if (mode == BLEND_SCREEN)
    {
        return 1.0 - (1.0 - base) * (1.0 - blend);
    }
    else if (mode == BLEND_OVERLAY)
    {
        vec3 result;
        for (int i = 0; i < 3; i++)
        {
            if (base[i] < 0.5)
                result[i] = 2.0 * base[i] * blend[i];
            else
                result[i] = 1.0 - 2.0 * (1.0 - base[i]) * (1.0 - blend[i]);
        }
        return result;
    }
    else if (mode == BLEND_SOFT_LIGHT)
    {
        vec3 result;
        for (int i = 0; i < 3; i++)
        {
            if (blend[i] < 0.5)
                result[i] = base[i] - (1.0 - 2.0 * blend[i]) * base[i] * (1.0 - base[i]);
            else
                result[i] = base[i] + (2.0 * blend[i] - 1.0) * (sqrt(base[i]) - base[i]);
        }
        return result;
    }
    else if (mode == BLEND_HARD_LIGHT)
    {
        vec3 result;
        for (int i = 0; i < 3; i++)
        {
            if (blend[i] < 0.5)
                result[i] = 2.0 * base[i] * blend[i];
            else
                result[i] = 1.0 - 2.0 * (1.0 - base[i]) * (1.0 - blend[i]);
        }
        return result;
    }
    return blend;
}

// Composite a layer on top of the current result
vec4 compositeLayer(vec4 current, vec4 layerColor, float opacity, int blendMode)
{
    if (layerColor.a <= 0.0 || opacity <= 0.0)
        return current;

    float finalAlpha = layerColor.a * opacity;
    vec3 blended = applyBlendMode(current.rgb, layerColor.rgb, blendMode);

    // Alpha compositing
    vec3 result = mix(current.rgb, blended, finalAlpha);
    float resultAlpha = current.a + finalAlpha * (1.0 - current.a);

    return vec4(result, resultAlpha);
}

// Sample colormap for height value
vec4 sampleColormap(float height)
{
    // Each colormap takes up 1/4 of the texture (256 samples each in a 1024 texture)
    float colormapOffset = float(uColormapType) * 0.25;
    float samplePos = colormapOffset + height * 0.25 * 0.999; // Slight scale to avoid edge issues
    return texture(uColormapTex, samplePos);
}

// Check if an area ID is in the highlight list
bool isAreaHighlighted(uint areaId)
{
    if (uHighlightCount == 0)
        return false;

    for (int i = 0; i < 4 && i < uHighlightCount; i++)
    {
        if (uint(uHighlightAreas1[i]) == areaId)
            return true;
    }
    for (int i = 0; i < 4 && (i + 4) < uHighlightCount; i++)
    {
        if (uint(uHighlightAreas2[i]) == areaId)
            return true;
    }
    return false;
}

void main()
{
    vec4 result = vec4(0.0, 0.0, 0.0, 0.0);

    // Layer 0: Minimap (bottom layer)
    if (uVisibilities.x == 1)
    {
        vec4 minimapColor = texture(uMinimapTex, vTexCoord);
        result = compositeLayer(result, minimapColor, uOpacities.x, uBlendModes.x);
    }

    // Layer 1: Height (middle layer)
    if (uVisibilities.y == 1)
    {
        vec4 heightSample = texture(uHeightTex, vTexCoord);
        float heightValue = heightSample.r; // Height stored in red channel
        vec4 heightColor = sampleColormap(heightValue);
        heightColor.a = heightSample.a; // Preserve alpha from height texture
        result = compositeLayer(result, heightColor, uOpacities.y, uBlendModes.y);
    }

    // Layer 2: Area (top layer)
    if (uVisibilities.z == 1)
    {
        vec4 areaColor = texture(uAreaTex, vTexCoord);

        // Apply area highlighting if enabled
        if (uAreaHighlightMode == 1 && areaColor.a > 0.0)
        {
            // Area ID is encoded in RGB (we decode R*256 + G for up to 65536 areas)
            uint areaId = uint(areaColor.r * 255.0) * 256u + uint(areaColor.g * 255.0);

            if (!isAreaHighlighted(areaId))
            {
                // Dim non-highlighted areas
                areaColor.rgb *= 0.3;
            }
        }

        result = compositeLayer(result, areaColor, uOpacities.z, uBlendModes.z);
    }

    // If no layers are visible, show a dark background
    if (result.a < 0.01)
    {
        result = vec4(0.1, 0.1, 0.1, 1.0);
    }

    fragColor = result;
}
