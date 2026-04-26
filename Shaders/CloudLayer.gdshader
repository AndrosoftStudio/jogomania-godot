shader_type spatial;
render_mode blend_mix, unshaded, cull_back, depth_draw_never;

uniform vec4 cloud_color : source_color = vec4(1.0, 1.0, 1.0, 0.42);
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

void vertex() {
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
    vec3 n = normalize(world_normal);
    float day = smoothstep(-0.25, 0.12, dot(n, normalize(sun_direction)));

    float drift = time_offset * 0.018;
    vec2 p1 = n.xy * 4.2 + vec2(drift, -drift * 0.35);
    vec2 p2 = n.yz * 4.2 + vec2(-drift * 0.55, drift * 0.45);
    vec2 p3 = n.zx * 4.2 + vec2(drift * 0.3, drift * 0.7);
    float cloud = (fbm(p1) + fbm(p2) + fbm(p3)) / 3.0;
    cloud = smoothstep(0.48, 0.66, cloud);

    ALBEDO = cloud_color.rgb;
    ALPHA = cloud * cloud_color.a * day;
}
