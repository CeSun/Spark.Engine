#version 330 core
in vec4 FragPos;

uniform vec3 LightLocation;
uniform float FarPlan;

void main()
{
    // get distance between fragment and light source
    float lightDistance = length(FragPos.xyz - LightLocation);

    // map to [0;1] range by dividing by far_plane
    lightDistance = lightDistance / FarPlan;

    // write this as modified depth
    gl_FragDepth = lightDistance;
}