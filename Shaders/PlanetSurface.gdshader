shader_type spatial;
render_mode unshaded, cull_back, depth_draw_opaque;

uniform sampler2D planet_texture : source_color, filter_nearest;
uniform sampler2D ocean_mask : filter_nearest;
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
    for (int i = 0; i < 5; i++) {
        v += noise(p) * a;
        p *= 2.03;
        a *= 0.52;
    }
    return v;
}

float cloud_mask(vec3 n, float time) {
    float drift = time * 0.018;
    vec2 p1 = n.xy * 4.2 + vec2(drift, -drift * 0.35);
    vec2 p2 = n.yz * 4.2 + vec2(-drift * 0.55, drift * 0.45);
    vec2 p3 = n.zx * 4.2 + vec2(drift * 0.3, drift * 0.7);
    float cloud_noise = (fbm(p1) + fbm(p2) + fbm(p3)) / 3.0;
    float soft_mass = smoothstep(0.34, 0.55, cloud_noise);
    float bright_core = smoothstep(0.54, 0.70, cloud_noise);
    return clamp(soft_mass * 0.58 + bright_core * 0.34, 0.0, 1.0);
}

void vertex() {
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec4 base = texture(planet_texture, UV);
    float ocean = texture(ocean_mask, UV).r;
    vec3 n = normalize(world_normal);
    vec3 sun = normalize(sun_direction);
    float sun_dot = dot(n, sun);

    // --- CONTROLE DA SOMBRA (DIA E NOITE) ---
    // Para não haver linha dura ("começa do nada"), calculamos a iluminação esférica real (Wrapped Lambert).
    // O degradê inicia de forma invisível no meio do dia (1.0) e morre suavemente em -0.30.
    float wrap = 0.30;       // 30% do caminho para dentro da noite
    float min_light = 0.02;  // Luz mínima do breu
    float max_light = 1.0;   // Intensidade máxima
    
    // Gradiente natural que flui por toda a esfera, eliminando a "quina" visual
    float light_gradient = clamp((sun_dot + wrap) / (1.0 + wrap), 0.0, 1.0);
    light_gradient = smoothstep(0.0, 1.0, light_gradient); // Caimento volumétrico perfeito
    float surface_light = mix(min_light, max_light, light_gradient);
    
    float day = smoothstep(0.0, 0.25, sun_dot); // Nuvens e brilho alinhados com o horizonte atmosférico

    float waves = (sin(UV.x * 90.0 + time_offset * 0.55) + sin((UV.x + UV.y) * 55.0 - time_offset * 0.8)) * 0.5;
    vec3 ocean_tint = vec3(0.02, 0.14, 0.26) + waves * 0.014;
    vec3 color = mix(base.rgb, base.rgb + ocean_tint, ocean * 0.42);

    vec3 view_dir = normalize(CAMERA_POSITION_WORLD - world_pos);
    vec3 reflected = reflect(-sun, n);
    float spec = pow(max(dot(view_dir, reflected), 0.0), 56.0) * ocean * day * 0.30;

    color *= surface_light;

    float clouds = cloud_mask(n, time_offset);
    float cloud_alpha = clouds * mix(0.02, 0.46, day);
    vec3 cloud_lit = mix(vec3(0.001, 0.002, 0.004), vec3(0.88, 0.94, 1.0), day);
    color = mix(color, cloud_lit, cloud_alpha);

    color += vec3(spec);
    ALBEDO = color;
    ALPHA = 1.0;
}
