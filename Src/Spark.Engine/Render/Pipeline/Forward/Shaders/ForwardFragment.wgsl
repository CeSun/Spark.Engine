@fragment
fn fs_main(in : VertexOutput) -> @location(0) vec4f {
    var base = mp.base_color;
{{BASE_COLOR_TEXTURE}}
    base = base * vec4f(in.color, 1.0);

    var metallic = mp.metallic_roughness.x;
    var roughness = mp.metallic_roughness.y;
{{MR_TEXTURE}}

    var n = normalize(in.world_normal);
    var color = base.rgb;
{{SHADING}}
    color = color + mp.emissive.rgb * mp.emissive.w;
{{EMISSIVE_TEXTURE}}
{{MASK}}
    return vec4f(color, base.a);
}
