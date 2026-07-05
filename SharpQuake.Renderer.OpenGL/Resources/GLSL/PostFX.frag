#version 120

uniform sampler2D tex;

uniform int blur;
uniform vec2 texelSize;

/*
    Try values like:
    1.0 = very subtle
    2.0 = visible
    4.0 = obvious
    8.0 = very obvious
*/
uniform float blurRadius = 8.0;

void main()
{
    vec2 uv = gl_TexCoord[0].st;

    if (blur == 0)
    {
        gl_FragColor = texture2D(tex, uv);
        return;
    }

    vec2 offset = texelSize * blurRadius;

    vec4 color = vec4(0.0);

    color += texture2D(tex, uv + offset * vec2(-1.0, -1.0)) * 1.0;
    color += texture2D(tex, uv + offset * vec2( 0.0, -1.0)) * 2.0;
    color += texture2D(tex, uv + offset * vec2( 1.0, -1.0)) * 1.0;

    color += texture2D(tex, uv + offset * vec2(-1.0,  0.0)) * 2.0;
    color += texture2D(tex, uv + offset * vec2( 0.0,  0.0)) * 4.0;
    color += texture2D(tex, uv + offset * vec2( 1.0,  0.0)) * 2.0;

    color += texture2D(tex, uv + offset * vec2(-1.0,  1.0)) * 1.0;
    color += texture2D(tex, uv + offset * vec2( 0.0,  1.0)) * 2.0;
    color += texture2D(tex, uv + offset * vec2( 1.0,  1.0)) * 1.0;

    color /= 16.0;

    gl_FragColor = color;
}