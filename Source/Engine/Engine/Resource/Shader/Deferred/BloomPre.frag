#version 300 es

precision highp float;
layout (location = 0) out vec3 glColor;

in vec2 OutTexCoord;
uniform sampler2D ColorTexture;

void main()
{ 
    vec3 FragColor = texture(ColorTexture, OutTexCoord).rgb;
    float brightness = dot(FragColor.rgb, vec3(0.2126, 0.7152, 0.0722));
    if(brightness >= 1.0)
        glColor = FragColor;
    else 
        glColor = vec3(0, 0, 0);
}