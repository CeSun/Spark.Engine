{GLVERSION}
out vec4 FragColor;

in vec3 fsColor;
in vec2 fsTexCoord;

uniform sampler2D texture1;

void main()
{
    FragColor = texture(texture1, fsTexCoord) * vec4(fsColor, 1.0);
}