shader_type spatial;
render_mode blend_mix, unshaded, cull_back, depth_prepass_alpha;

uniform vec4 cloud_color : source_color = vec4(1.0, 1.0, 1.0, 0.34);
uniform vec3 sun_direction = vec3(1.0, 0.2, 0.0);
uniform float time_offset = 0.0;

varying vec3 world_normal;

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
        p *= 2.03;
        a *= 0.52;
    }
    return v;
}

float cloud_noise_at(vec3 n, float drift) {
    vec2 p1 = n.xy * 4.2 + vec2(drift, -drift * 0.35);
    vec2 p2 = n.yz * 4.2 + vec2(-drift * 0.55, drift * 0.45);
    vec2 p3 = n.zx * 4.2 + vec2(drift * 0.3, drift * 0.7);
    return (fbm(p1) + fbm(p2) + fbm(p3)) / 3.0;
}

void vertex() {
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
    vec3 n = normalize(world_normal);
    vec3 sun = normalize(sun_direction);
    float sun_dot = dot(n, sun);
    float day = smoothstep(0.02, 0.36, sun_dot);
    float night_gradient = pow(clamp((sun_dot + 1.0) / 1.12, 0.0, 1.0), 1.22);
    float night_visibility = mix(0.003, 0.13, night_gradient);
    float visibility = mix(night_visibility, 0.62, day);

    float drift = time_offset * 0.018;
    float layer_a = cloud_noise_at(n, drift);
    float layer_b = cloud_noise_at(normalize(n + vec3(0.09, -0.04, 0.12)), -drift * 1.7 + 2.3);
    float streaks = cloud_noise_at(normalize(n + sun * 0.08), drift * 2.8 + 7.0);
    float cloud_noise = mix(layer_a, layer_b, 0.42);
    float soft_mass = smoothstep(0.31, 0.52, cloud_noise);
    float bright_core = smoothstep(0.53, 0.69, cloud_noise + streaks * 0.08);
    float cloud = clamp(soft_mass * 0.78 + bright_core * 0.58, 0.0, 1.0);

    vec3 night_color = vec3(0.002, 0.003, 0.006);
    vec3 day_color = mix(vec3(0.70, 0.78, 0.88), cloud_color.rgb, bright_core);
    ALBEDO = mix(night_color, day_color, day);
    ALPHA = cloud * cloud_color.a * visibility;
}
