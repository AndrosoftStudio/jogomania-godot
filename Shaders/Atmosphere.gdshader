shader_type spatial;
render_mode blend_add, unshaded, cull_back, depth_draw_never;

uniform vec4 atmosphere_color : source_color = vec4(0.2, 0.5, 1.0, 1.0);
uniform float falloff : hint_range(0.1, 10.0) = 3.0;
uniform float intensity : hint_range(0.0, 5.0) = 1.2;

void fragment() {
    // Calcula o efeito de borda (Fresnel)
    float fresnel = sqrt(1.0 - dot(NORMAL, VIEW));
    fresnel = pow(fresnel, falloff);
    
    ALBEDO = atmosphere_color.rgb * intensity;
    ALPHA = fresnel * atmosphere_color.a;
}
