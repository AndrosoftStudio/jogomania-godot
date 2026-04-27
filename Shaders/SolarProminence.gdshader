shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never;

uniform vec4 prominence_color : source_color = vec4(1.0, 0.32, 0.04, 0.55);
uniform float pulse = 1.0;

void fragment() {
    ALBEDO = prominence_color.rgb * pulse;
    ALPHA = prominence_color.a * pulse;
    EMISSION = prominence_color.rgb * (1.8 * pulse);
}
