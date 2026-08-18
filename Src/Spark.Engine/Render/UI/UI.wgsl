// UI overlay shader：顶点已在 CPU 端转为 NDC，片元采样纹理并乘 tint 颜色。
struct UIVertexIn {
    @location(0) position : vec2f,
    @location(1) uv       : vec2f,
    @location(2) color    : vec4f,
};

struct UIVertexOut {
    @builtin(position) position : vec4f,
    @location(0) uv            : vec2f,
    @location(1) color         : vec4f,
};

@group(0) @binding(0) var ui_tex  : texture_2d<f32>;
@group(0) @binding(1) var ui_samp : sampler;

@vertex
fn vs_main(in : UIVertexIn) -> UIVertexOut {
    var out : UIVertexOut;
    out.position = vec4f(in.position, 0.0, 1.0);
    out.uv = in.uv;
    out.color = in.color;
    return out;
}

@fragment
fn fs_main(in : UIVertexOut) -> @location(0) vec4f {
    return textureSample(ui_tex, ui_samp, in.uv) * in.color;
}
