shader_type spatial;
render_mode unshaded, cull_back, depth_draw_opaque;

uniform sampler2D sun_texture : source_color, filter_linear_mipmap;
uniform float time_offset = 0.0;
uniform vec4 core_color : source_color = vec4(1.0, 0.82, 0.22, 1.0);
uniform vec4 flare_color : source_color = vec4(1.0, 0.18, 0.02, 1.0);

varying vec3 world_normal;
varying vec3 world_pos;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

float fbm(vec2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 5; i++) {
        v += noise(p) * a;
        p *= 2.05;
        a *= 0.52;
    }
    return v;
}

float surface_noise(vec3 n, float time) {
    float a = fbm(n.xy * 6.0 + vec2(time * 0.20, -time * 0.09));
    float b = fbm(n.yz * 6.5 + vec2(-time * 0.13, time * 0.17));
    float c = fbm(n.zx * 7.0 + vec2(time * 0.08, time * 0.21));
    return (a + b + c) / 3.0;
}

void vertex() {
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec3 n = normalize(world_normal);
    vec2 uv1 = UV + vec2(time_offset * 0.010, 0.0);
    vec2 uv2 = UV + vec2(-time_offset * 0.015, time_offset * 0.004);
    vec3 base = mix(texture(sun_texture, uv1).rgb, texture(sun_texture, uv2).rgb, 0.35);

    float plasma = surface_noise(n, time_offset);
    float fine = surface_noise(normalize(n + vec3(0.17, -0.11, 0.23)), time_offset * 1.7);
    float flare = smoothstep(0.80, 0.96, plasma) * (0.45 + fine * 0.35);

    vec3 color = mix(base, core_color.rgb, 0.18 + plasma * 0.18);
    color += flare_color.rgb * flare * 0.45;

    vec3 view_dir = normalize(CAMERA_POSITION_WORLD - world_pos);
    float rim = pow(clamp(1.0 - abs(dot(normalize(world_normal), view_dir)), 0.0, 1.0), 2.0);
    color += flare_color.rgb * rim * (0.10 + flare * 0.20);

    ALBEDO = color;
    EMISSION = color * (1.25 + flare * 0.90);
    ALPHA = 1.0;
}
