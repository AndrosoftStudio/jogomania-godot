shader_type spatial;
render_mode unshaded, cull_front, depth_draw_never;

uniform sampler2D star_texture : source_color;
uniform float time_offset : hint_range(0.0, 1000.0) = 0.0;

void fragment() {
	vec4 star = texture(star_texture, UV);
	// Twinkling: cada estrela pisca numa frequência diferente baseada em UV
	float freq = 1.5 + star.r * 4.0 + UV.x * 3.7 + UV.y * 5.3;
	float twinkle = sin(time_offset * freq) * 0.25 + 0.75;
	// Estrelas pequenas: só as que têm alpha alto ficam visíveis
	float brightness = star.r * twinkle;
	ALBEDO = vec3(brightness);
	ALPHA = step(0.3, star.r) * brightness;
}
