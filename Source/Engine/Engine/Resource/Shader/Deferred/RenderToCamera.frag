#version 300 es

precision highp float;
out vec4 glColor;

in vec2 OutTexCoord;
uniform sampler2D ColorTexture;

void main()
{
    vec4 color = texture(ColorTexture, OutTexCoord);
	float alpha = color.a;
    vec3 rgb = color.rgb / (color.rgb + vec3(1.0));
    glColor = vec4(pow(color.rgb, vec3(1.0/ 2.2)), alpha);
}
