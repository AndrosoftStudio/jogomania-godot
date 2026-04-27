shader_type spatial;
render_mode blend_add, unshaded, cull_back, depth_draw_never;

uniform vec4 atmosphere_color : source_color = vec4(0.34, 0.62, 1.0, 1.0);
uniform vec4 twilight_color : source_color = vec4(1.0, 0.45, 0.16, 1.0);
uniform float falloff : hint_range(0.1, 10.0) = 2.15;
uniform float intensity : hint_range(0.0, 5.0) = 0.95;
uniform vec3 sun_direction = vec3(1.0, 0.2, 0.0);
uniform float time_offset = 0.0;

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
    for (int i = 0; i < 4; i++) {
        v += noise(p) * a;
        p *= 2.07;
        a *= 0.52;
    }
    return v;
}

float tri_noise(vec3 n, float scale, float drift) {
    float a = fbm(n.xy * scale + vec2(drift, -drift * 0.31));
    float b = fbm(n.yz * scale + vec2(-drift * 0.43, drift * 0.27));
    float c = fbm(n.zx * scale + vec2(drift * 0.21, drift * 0.39));
    return (a + b + c) / 3.0;
}

void vertex() {
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec3 n = normalize(world_normal);
    vec3 sun = normalize(sun_direction);
    vec3 view_dir = normalize(CAMERA_POSITION_WORLD - world_pos);
    vec3 camera_dir = normalize(CAMERA_POSITION_WORLD);
    float ndotl = dot(n, sun);
    float camera_day = smoothstep(-0.20, 0.25, dot(camera_dir, sun));
    float day = smoothstep(-0.02, 0.46, ndotl);
    float night_fade = smoothstep(-0.90, -0.05, ndotl);
    float terminator_line = smoothstep(-0.080, 0.012, ndotl) * (1.0 - smoothstep(0.030, 0.180, ndotl));
    float rim = pow(clamp(1.0 - max(dot(n, view_dir), 0.0), 0.0, 1.0), falloff);
    float outer_rim = pow(clamp(1.0 - abs(dot(n, view_dir)), 0.0, 1.0), 4.0);
    float haze_noise = tri_noise(n, 7.0, time_offset * 0.035);
    float aurora_noise = tri_noise(n + sun * 0.22, 15.0, time_offset * 0.090);

    float blue_alpha = rim * (0.08 + haze_noise * 0.055) * day * camera_day;
    float night_limb = rim * 0.035 * night_fade * (1.0 - day);
    float orange_alpha = outer_rim * (0.48 + haze_noise * 0.30) * terminator_line;
    float aurora = smoothstep(0.54, 0.78, aurora_noise) * smoothstep(0.62, 0.88, abs(n.y)) * (1.0 - day) * rim * 0.16;

    vec3 glow_color = atmosphere_color.rgb * (blue_alpha + night_limb);
    glow_color += twilight_color.rgb * orange_alpha;
    glow_color += vec3(0.20, 0.95, 0.72) * aurora;

    ALBEDO = glow_color * intensity;
    ALPHA = clamp((blue_alpha + orange_alpha + night_limb + aurora) * atmosphere_color.a, 0.0, 0.85);
}
