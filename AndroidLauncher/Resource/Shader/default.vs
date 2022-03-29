{GLVERSION}
layout (location = 0) in vec3 Location;
layout (location = 1) in vec3 Normal;
layout (location = 2) in vec3 Color;
layout (location = 3) in vec2 TexCoord;

out vec3 outColor;

void main()
{
    gl_Position = vec4(Location, 1.0);
    outColor = Color;
}