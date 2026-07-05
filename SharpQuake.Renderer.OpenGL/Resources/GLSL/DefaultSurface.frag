#version 110
uniform sampler2D tex;
uniform sampler2D lm;
uniform int noLm;
uniform float opacity;
uniform int waveDistort;
uniform float time;
uniform float turbScale;
void main()
{
    vec2 texCoord = gl_TexCoord[0].st;

    if (waveDistort == 1)
    {
        float waveSpeed = 0.1;
        float waveFactorS = sin((texCoord.y * 0.125 + time * waveSpeed) * turbScale);
        float waveFactorT = sin((texCoord.x * 0.125 + time * waveSpeed) * turbScale);

        float s = waveFactorS * (1.0 / 128.0);
        float t = waveFactorT * (1.0 / 128.0);

        texCoord = vec2(texCoord.x + s, texCoord.y + t);
    }

    vec4 color = texture2D(tex, texCoord);
    vec3 finalColor;

    if (noLm == 0)
    {
        vec4 lmc = texture2D(lm, gl_TexCoord[1].st);
        float light = min(lmc.r * 2.0, 1.0);
        finalColor = color.rgb * light;
    }
    else
    {
        finalColor = color.rgb;
    }

    gl_FragColor = vec4(finalColor, color.a * opacity);
}