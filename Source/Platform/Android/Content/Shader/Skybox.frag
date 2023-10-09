#version 300 es

precision highp float;
out vec4 FragColor;

in vec3 TexCoords;

uniform samplerCube skybox;
uniform sampler2D NormalTexture;
uniform vec2 BufferSize;
uniform vec2 ScreenSize;

void main()
{   
    vec2 ScreenCoord = ((gl_FragCoord.xy + 0.5) / ScreenSize);
     ScreenCoord = ScreenCoord * (ScreenSize / BufferSize);;
     if (length(texture(NormalTexture, ScreenCoord).xyz) > 0.0f)
        discard;
    FragColor = texture(skybox, TexCoords);
}