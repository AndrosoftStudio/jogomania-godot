shader_type spatial;
render_mode blend_add, unshaded, cull_back, depth_draw_never;

uniform vec4 atmosphere_color : source_color = vec4(0.34, 0.62, 1.0, 1.0);
uniform vec4 twilight_color : source_color = vec4(1.0, 0.45, 0.16, 1.0);
uniform float falloff : hint_range(0.1, 10.0) = 2.15;
uniform float intensity : hint_range(0.0, 5.0) = 0.95;
uniform vec3 sun_direction = vec3(1.0, 0.2, 0.0);

varying vec3 world_normal;

void vertex() {
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
    vec3 n = normalize(world_normal);
    vec3 sun = normalize(sun_direction);
    float ndotl = dot(n, sun);
    float day = smoothstep(-0.18, 0.32, ndotl);
    float twilight = smoothstep(-0.42, -0.16, ndotl) * (1.0 - smoothstep(-0.13, 0.05, ndotl));
    float rim = pow(clamp(1.0 - dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0), falloff);
    float thin_haze = rim * rim;
    float night_rim = rim * smoothstep(-0.90, -0.18, ndotl) * 0.18;

    vec3 glow_color = mix(atmosphere_color.rgb, twilight_color.rgb, twilight * 0.85);
    ALBEDO = glow_color * intensity;
    ALPHA = (night_rim + rim * 0.50 * day + thin_haze * 0.90 * twilight) * atmosphere_color.a;
}
