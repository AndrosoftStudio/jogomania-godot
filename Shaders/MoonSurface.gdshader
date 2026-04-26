shader_type spatial;
render_mode unshaded, cull_back, depth_draw_opaque;

uniform sampler2D moon_texture : source_color, filter_linear;
uniform vec3 sun_direction = vec3(1.0, 0.2, 0.0);

varying vec3 world_normal;

void vertex() {
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
    vec3 n = normalize(world_normal);
    float day = smoothstep(-0.03, 0.18, dot(n, normalize(sun_direction)));
    vec3 base = texture(moon_texture, UV).rgb;
    ALBEDO = base * mix(0.045, 1.0, day);
    ALPHA = 1.0;
}
