{GLVERSION}
out vec4 FragColor;

in vec3 fsColor;
in vec2 fsTexCoord;


void main()
{
    FragColor = vec4(fsColor, 1.0);
}