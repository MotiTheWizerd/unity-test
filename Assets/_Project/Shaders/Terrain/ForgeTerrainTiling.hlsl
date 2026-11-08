#ifndef FORGE_TERRAIN_TILING_INCLUDED
#define FORGE_TERRAIN_TILING_INCLUDED

// Forge: breaks up the ground's tiling, so a street of one ground doesn't show the same stones every few meters.
// Every layer is read twice, its texture shifted by one of a handful of offsets that a slow value noise over the world
// picks, and the two reads are blended only where the noise crosses from one offset to the next (Inigo Quilez's
// "texture repetition" technique 3). Colour, normal and mask maps share the pattern, so bumps and height stay with
// the colour. A second, larger noise varies the brightness over tens of meters. The noise reads world coordinates
// (_ForgeWorldOrigin, which WorldFrame keeps), so the pattern holds still when the scene is rebased.
// A pass that never calls ForgeTilingSetup samples exactly as URP's terrain does: offsets and mix stay zero.

float4 _ForgeWorldOrigin; // the world position at the scene's zero

static const float ForgePatchSize = 7.0;       // meters across one patch of the offset noise
static const float ForgeMacroSize = 23.0;      // meters across the large brightness variation
static const half ForgeMacroStrength = 0.12h;  // how far brightness strays, up or down

static float2 forgeOffsetA = float2(0.0, 0.0);
static float2 forgeOffsetB = float2(0.0, 0.0);
static half forgeMix = 0.0h;
static half forgeTint = 1.0h;

// A hash without sin, which loses precision at world coordinates kilometres out (Dave Hoskins' hash12).
float ForgeHash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float ForgeValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(ForgeHash(i), ForgeHash(i + float2(1.0, 0.0)), u.x),
                lerp(ForgeHash(i + float2(0.0, 1.0)), ForgeHash(i + float2(1.0, 1.0)), u.x), u.y);
}

void ForgeTilingSetup(float3 positionWS)
{
    float2 world = positionWS.xz + _ForgeWorldOrigin.xz;
    float k = ForgeValueNoise(world / ForgePatchSize) * 8.0;
    float i = floor(k);
    forgeOffsetA = sin(float2(3.0, 7.0) * i);
    forgeOffsetB = sin(float2(3.0, 7.0) * (i + 1.0));
    forgeMix = (half)smoothstep(0.3, 0.7, frac(k));
    float macro = ForgeValueNoise(world / ForgeMacroSize) * 0.65 + ForgeValueNoise(world / (ForgeMacroSize * 0.31)) * 0.35;
    forgeTint = 1.0h + ForgeMacroStrength * (half)(macro * 2.0 - 1.0);
}

// One layer's map, read at both offsets and blended; gradients from the unshifted UV, so mips match across the seam.
#define FORGE_LAYER_SAMPLE(tex, samp, uv) lerp( \
    SAMPLE_TEXTURE2D_GRAD(tex, samp, (uv) + forgeOffsetA, ddx(uv), ddy(uv)), \
    SAMPLE_TEXTURE2D_GRAD(tex, samp, (uv) + forgeOffsetB, ddx(uv), ddy(uv)), forgeMix)

#endif
