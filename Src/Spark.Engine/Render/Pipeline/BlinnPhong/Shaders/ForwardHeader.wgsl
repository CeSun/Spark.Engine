struct Light {
    color_intensity : vec4f,
    position_range  : vec4f,
    direction_cone  : vec4f,
    type_outer      : vec4f,
};

struct FrameUniforms {
    view_proj    : mat4x4f,
    camera_pos   : vec4f,
    light_count  : u32,
    pad0 : u32,
    pad1 : u32,
    pad2 : u32,
    lights : array<Light, {{MAX_LIGHTS}}>,
    shadow_view_proj : mat4x4f,
    shadow_light     : u32,
    pad3 : u32,
    pad4 : u32,
    pad5 : u32,
};

struct ObjectUniforms {
    world      : mat4x4f,
    normal_mat : mat4x4f,
};

struct MaterialParamsUniform {
    base_color         : vec4f,
    metallic_roughness : vec4f,
    emissive           : vec4f,
    normal_strength    : vec4f,
};

struct VertexInput {
    @location(0) position : vec3f,
    @location(1) color    : vec3f,
    @location(2) uv       : vec2f,
    @location(3) normal   : vec3f,
};

struct VertexOutput {
    @builtin(position) clip_position : vec4f,
    @location(0) world_pos    : vec3f,
    @location(1) world_normal : vec3f,
    @location(2) uv           : vec2f,
    @location(3) color        : vec3f,
};

@group(0) @binding(0) var<uniform> frame : FrameUniforms;
@group(0) @binding(1) var shadow_map  : texture_depth_2d;
@group(0) @binding(2) var shadow_samp : sampler_comparison;
@group(1) @binding(0) var<uniform> obj   : ObjectUniforms;
@group(2) @binding(0) var<uniform> mp    : MaterialParamsUniform;
@group(3) @binding(0) var base_color_tex : texture_2d<f32>;
@group(3) @binding(1) var normal_tex     : texture_2d<f32>;
@group(3) @binding(2) var emissive_tex   : texture_2d<f32>;
@group(3) @binding(3) var mr_tex         : texture_2d<f32>;
@group(3) @binding(4) var mask_tex       : texture_2d<f32>;
@group(3) @binding(5) var samp           : sampler;

@vertex
fn vs_main(in : VertexInput) -> VertexOutput {
    var out : VertexOutput;
    var world_pos = obj.world * vec4f(in.position, 1.0);
    out.clip_position = frame.view_proj * world_pos;
    out.world_pos = world_pos.xyz;
    out.world_normal = (obj.normal_mat * vec4f(in.normal, 0.0)).xyz;
    out.uv = in.uv;
    out.color = in.color;
    return out;
}
