#version 110

varying vec3 vWorldPos;

void main()
{
    vec4 worldPos = gl_ModelViewMatrix * gl_Vertex;

    vWorldPos = worldPos.xyz;

    gl_TexCoord[0] = gl_TextureMatrix[0] * gl_MultiTexCoord0;
    gl_TexCoord[1] = gl_TextureMatrix[1] * gl_MultiTexCoord1;

    gl_Position = gl_ModelViewProjectionMatrix * gl_Vertex;
}