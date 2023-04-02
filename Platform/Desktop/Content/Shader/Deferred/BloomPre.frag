#version 330 core
out vec4 glColor;

in vec2 OutTexCoord;
uniform sampler2D ColorTexture;

void main()
{ 
    vec4 FragColor = vec4(texture(ColorTexture, OutTexCoord).rgb, 1.0f);
    float brightness = dot(FragColor.rgb, vec3(0.2126, 0.7152, 0.0722));
    if(brightness > 1.0)
        glColor = vec4(FragColor.rgb, 1.0);
}
