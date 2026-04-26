shader_type spatial;
render_mode unshaded, cull_front, depth_draw_opaque;

uniform sampler2D star_texture : source_color;
uniform float time_offset : hint_range(0.0, 1000.0) = 0.0;

void fragment() {
    vec4 star = texture(star_texture, UV);
    // O starfield e opaco para o planeta bloquear estrelas pelo depth buffer.
    float freq = 1.5 + star.r * 4.0 + UV.x * 3.7 + UV.y * 5.3;
    float twinkle = sin(time_offset * freq) * 0.25 + 0.75;
    float brightness = step(0.3, star.r) * star.r * twinkle;
    ALBEDO = vec3(brightness);
}
