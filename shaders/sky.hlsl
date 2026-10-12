// The sky: the original blits a panorama as a 2D backdrop. It scrolls horizontally with the yaw
// (wrap_width pixels for 360 degrees) and vertically with the pitch, horizon_row at eye level; above
// and below it the screen is filled with top and bottom colours. Drawn as one screen-filling
// triangle before the level, without depth. The enhanced look filters it.

#include "panorama.hlsli"

cbuffer VertexUniforms : register(b0, space1)
{
    // Clip space to a world direction (the camera's rotation and projection, inverted).
    float4x4 clip_to_direction;
};

struct VertexOut
{
    float4 position : SV_Position;
    float3 direction : TEXCOORD0;
};

VertexOut vs_main(uint id : SV_VertexID)
{
    // (-1,-1), (3,-1), (-1,3): covers the screen.
    float2 clip = float2(id == 1 ? 3.0 : -1.0, id == 2 ? 3.0 : -1.0);
    VertexOut output;
    output.position = float4(clip, 0.0, 1.0);
    float4 direction = mul(clip_to_direction, float4(clip, 1.0, 1.0));
    output.direction = direction.xyz / direction.w;
    return output;
}

// A pixel's colour; above and below the panorama, the top and bottom colours.
float3 sky_pixel(int column, int row)
{
    int index;
    if (row < 0)
    {
        index = top_colour;
    }
    else if (row >= panorama_height)
    {
        index = bottom_colour;
    }
    else
    {
        index = panorama_index(column, row);
    }

    return palette_texture.Load(int3(index, 0, 0)).rgb;
}

// TEMPORARY TEST: draws a grid of thin lines every 5 degrees instead of the sky (white: azimuth,
// yellow: elevation, brighter every 45 degrees), to measure how the sky moves as the camera turns.
float4 ps_main(VertexOut input) : SV_Target
{
    float3 d = normalize(input.direction);
    float2 angle = float2(degrees(atan2(d.x, d.y)), degrees(asin(clamp(d.z, -1.0, 1.0))));
    float2 width = max(fwidth(angle), 1e-4);
    float2 cell = angle / 5.0;
    float2 gap = abs(frac(cell + 0.5) - 0.5) * 5.0;
    float2 stripe = saturate(1.0 - (gap - 0.05) / width);
    float2 big = abs(frac(angle / 45.0 + 0.5) - 0.5) * 45.0;
    float2 bigStripe = saturate(1.0 - (big - 0.05) / width);
    float3 colour = float3(0.04, 0.07, 0.2);
    colour = lerp(colour, float3(1.0, 1.0, 1.0), stripe.x * 0.8 + bigStripe.x * 0.2);
    colour = lerp(colour, float3(1.0, 0.9, 0.2), stripe.y * 0.8 + bigStripe.y * 0.2);
    return float4(colour, 1.0);
}
