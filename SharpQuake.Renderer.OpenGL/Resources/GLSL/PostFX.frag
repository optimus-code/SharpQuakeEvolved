#version 120

uniform sampler2D tex;
uniform sampler2D blurTex;

/*
    0.0 = no grain
    0.5 = default grain opacity
    1.0 = strong grain
*/
uniform float noiseGrain = 0.5;
uniform float time = 0.0;

/*
    0.0 = no general screen blur
    1.0 = fully blurred screen

    This controls ONLY the general full-screen blur effect.
    It does not control whether the blur texture was generated.
*/
uniform float screenBlurAmount = 0.0;
uniform float fadeScreen = 0.0;

/*
    0.0 = no bloom
    1.0+ = stronger bloom

    This uses the blurred texture additively.
*/
uniform float bloomIntensity = 0.0;

/*
    0 = do not sample blurTex
    1 = blurTex is available
*/
uniform int blurEnabled = 0;

float random(vec2 p, float seed)
{
    return fract(sin(dot(p, vec2(12.9898, 78.233)) + seed * 37.719) * 43758.5453);
}

vec3 lerp(vec3 a, vec3 b, float t)
{
    return mix(a, b, t);
}

void main()
{
    vec2 uv = gl_TexCoord[0].st;

    vec4 scene = texture2D(tex, uv);
    vec3 color = scene.rgb;

    float safeScreenBlurAmount = clamp(screenBlurAmount, 0.0, 1.0);
    float safeBloomIntensity = max(bloomIntensity, 0.0);
    float safeNoiseGrain = max(noiseGrain, 0.0);

    if (blurEnabled != 0)
    {
        vec4 blurred = texture2D(blurTex, uv);

        // Optional general full-screen blur.
        if (safeScreenBlurAmount > 0.0)
        {
            color = mix(color, blurred.rgb, safeScreenBlurAmount);
        }

        // Optional bloom/glow contribution.
        if (safeBloomIntensity > 0.0)
        {
            color += blurred.rgb * safeBloomIntensity;
        }
    }

    if (safeNoiseGrain > 0.0)
    {
        float frame = floor(time * 24.0);

        float noise = random(gl_FragCoord.xy, frame);

        float grain = noise - 0.5;

        color += grain * safeNoiseGrain;
    }

    gl_FragColor = vec4(lerp(clamp(color, 0.0, 1.0), vec3(0, 0, 0), fadeScreen), scene.a);
}