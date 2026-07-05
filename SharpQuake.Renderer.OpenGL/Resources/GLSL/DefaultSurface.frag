#version 110

uniform sampler2D tex;
uniform sampler2D lm;

uniform int noLm;
uniform float opacity;

uniform int waveDistort;
uniform float time;
uniform float turbScale;

uniform int numDLights;

uniform vec3 dlightOrigin0;
uniform vec3 dlightColor0;
uniform float dlightRadius0;
uniform float dlightMinLight0;

uniform vec3 dlightOrigin1;
uniform vec3 dlightColor1;
uniform float dlightRadius1;
uniform float dlightMinLight1;

uniform vec3 dlightOrigin2;
uniform vec3 dlightColor2;
uniform float dlightRadius2;
uniform float dlightMinLight2;

uniform vec3 dlightOrigin3;
uniform vec3 dlightColor3;
uniform float dlightRadius3;
uniform float dlightMinLight3;

varying vec3 vWorldPos;

vec3 CalcDLight(vec3 origin, vec3 color, float radius, float minLight)
{
    vec3 toLight = origin - vWorldPos;
    float dist = length(toLight);

    float denom = max(radius - minLight, 1.0);
    float atten = clamp((radius - dist - minLight) / denom, 0.0, 1.0);

    return color * atten;
}

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

        float staticLight = min(lmc.r * 2.0, 1.0);

        vec3 dynamicLight = vec3(0.0);

        if (numDLights > 0)
            dynamicLight += CalcDLight(dlightOrigin0, dlightColor0, dlightRadius0, dlightMinLight0);

        if (numDLights > 1)
            dynamicLight += CalcDLight(dlightOrigin1, dlightColor1, dlightRadius1, dlightMinLight1);

        if (numDLights > 2)
            dynamicLight += CalcDLight(dlightOrigin2, dlightColor2, dlightRadius2, dlightMinLight2);

        if (numDLights > 3)
            dynamicLight += CalcDLight(dlightOrigin3, dlightColor3, dlightRadius3, dlightMinLight3);

        vec3 staticColor = color.rgb * staticLight;
        vec3 dynamicColor = color.rgb * dynamicLight * 1.5;
        finalColor = staticColor + dynamicColor;
        finalColor = clamp(finalColor, 0.0, 1.0);
    }
    else
    {
        finalColor = color.rgb;
    }

    gl_FragColor = vec4(finalColor, color.a * opacity);
}