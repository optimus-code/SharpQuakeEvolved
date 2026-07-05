#version 120

uniform sampler2D tex;

uniform float texelWidth;
uniform float texelHeight;

uniform float directionX;
uniform float directionY;

/*
    Try:
    4.0  = soft blur
    8.0  = medium blur
    12.0 = strong blur
    20.0 = very strong blur
*/
uniform float blurRadius = 12.0;

const int SAMPLE_RADIUS = 16;

void main()
{
    vec2 uv = gl_TexCoord[0].st;

    vec2 direction = vec2(directionX, directionY);

    if (dot(direction, direction) <= 0.0)
    {
        gl_FragColor = texture2D(tex, uv);
        return;
    }

    direction = normalize(direction);

    vec2 texel = vec2(texelWidth, texelHeight);

    float radius = max(blurRadius, 0.001);
    float sigma = radius * 0.5;

    vec4 color = vec4(0.0);
    float totalWeight = 0.0;

    for (int i = -SAMPLE_RADIUS; i <= SAMPLE_RADIUS; i++)
    {
        float fi = float(i);

        // Spread the fixed sample count across blurRadius texels.
        float sampleDistance = fi * radius / float(SAMPLE_RADIUS);

        float weight = exp(-(sampleDistance * sampleDistance) / (2.0 * sigma * sigma));

        vec2 sampleUv = uv + direction * texel * sampleDistance;

        color += texture2D(tex, sampleUv) * weight;
        totalWeight += weight;
    }

    gl_FragColor = color / totalWeight;
}