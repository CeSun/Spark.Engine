{GLVERSION}
out vec4 FragColor;

in vec3 outColor;

void main()
{
    FragColor = vec4(outColor, 1.0f);
}