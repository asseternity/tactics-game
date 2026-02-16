shader_type spatial;

uniform vec3 grid_color : source_color = vec3(0.0, 0.0, 0.0); // Black lines
uniform vec3 tile_color : source_color = vec3(1.0, 1.0, 1.0); // White tiles
uniform float grid_size = 2.0; // Matches your C# GridSize
uniform float line_thickness = 0.05;

varying vec3 world_pos;

void vertex() {
    // Get the world position of the vertex
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    // Calculate grid lines based on world position
    vec2 pos = world_pos.xz;
    
    // Simple modulo math to draw lines every 'grid_size' units
    float x_line = step(mod(pos.x, grid_size), line_thickness);
    float z_line = step(mod(pos.z, grid_size), line_thickness);
    
    if (x_line > 0.5 || z_line > 0.5) {
        ALBEDO = grid_color;
    } else {
        ALBEDO = tile_color;
    }
}