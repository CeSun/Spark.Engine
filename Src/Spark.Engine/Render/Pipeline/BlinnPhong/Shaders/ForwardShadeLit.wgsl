fn shade_lit(albedo : vec3f, metallic : f32, roughness : f32, n : vec3f, world_pos : vec3f, camera_pos : vec3f) -> vec3f {
    var v = normalize(camera_pos - world_pos);
    var nrm = normalize(n);
    var shininess = mix(256.0, 2.0, roughness);
    var spec_color = mix(vec3f(0.04), albedo, metallic);
    var color = albedo * 0.04;
    for (var i = 0u; i < frame.light_count; i = i + 1u) {
        var l = frame.lights[i];
        var lcol = l.color_intensity.rgb * l.color_intensity.w;
        var to_light : vec3f;
        var atten = 1.0;
        var t = l.type_outer.x;
        if (t < 0.5) {
            var d = l.position_range.xyz - world_pos;
            var dist = length(d);
            to_light = d / max(dist, 1e-4);
            atten = clamp(1.0 - dist / max(l.position_range.w, 1e-4), 0.0, 1.0);
        } else if (t < 1.5) {
            to_light = -normalize(l.direction_cone.xyz);
            atten = 1.0;
        } else {
            var d = l.position_range.xyz - world_pos;
            var dist = length(d);
            to_light = d / max(dist, 1e-4);
            var cos_a = dot(-to_light, normalize(l.direction_cone.xyz));
            var inner = l.direction_cone.w;
            var outer = l.type_outer.y;
            var spot = smoothstep(outer, inner, cos_a);
            atten = spot * clamp(1.0 - dist / max(l.position_range.w, 1e-4), 0.0, 1.0);
        }
        var ndl = max(dot(nrm, to_light), 0.0);
        var h = normalize(to_light + v);
        var spec = pow(max(dot(nrm, h), 0.0), shininess);
        var shadow = 1.0;
        if (i == frame.shadow_light) {
            var ls = frame.shadow_view_proj * vec4f(world_pos, 1.0);
            var ndc = ls.xyz / ls.w;
            var suv = ndc.xy * 0.5 + 0.5;
            suv.y = 1.0 - suv.y;
            shadow = textureSampleCompare(shadow_map, shadow_samp, suv, ndc.z - 0.002);
        }
        color = color + lcol * atten * shadow * (albedo * ndl + spec_color * spec * ndl);
    }
    return color;
}
