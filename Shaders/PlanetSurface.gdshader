shader_type spatial;
render_mode unshaded, cull_back, depth_draw_opaque;

uniform sampler2D planet_texture : source_color, filter_nearest;
uniform sampler2D ocean_mask : filter_nearest;
uniform vec3 sun_direction = vec3(1.0, 0.2, 0.0);
uniform float time_offset = 0.0;

varying vec3 world_normal;
varying vec3 world_pos;

void vertex() {
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec4 base = texture(planet_texture, UV);
    float ocean = texture(ocean_mask, UV).r;
    vec3 n = normalize(world_normal);
    vec3 sun = normalize(sun_direction);
    float day = smoothstep(-0.18, 0.26, dot(n, sun));

    float waves = (sin(UV.x * 90.0 + time_offset * 0.55) + sin((UV.x + UV.y) * 55.0 - time_offset * 0.8)) * 0.5;
    vec3 ocean_tint = vec3(0.02, 0.14, 0.26) + waves * 0.014;
    vec3 color = mix(base.rgb, base.rgb + ocean_tint, ocean * 0.42);

    vec3 view_dir = normalize(CAMERA_POSITION_WORLD - world_pos);
    vec3 reflected = reflect(-sun, n);
    float spec = pow(max(dot(view_dir, reflected), 0.0), 56.0) * ocean * day * 0.30;

    color *= mix(0.045, 1.0, day);
    color += vec3(spec);
    ALBEDO = color;
    ALPHA = 1.0;
}
