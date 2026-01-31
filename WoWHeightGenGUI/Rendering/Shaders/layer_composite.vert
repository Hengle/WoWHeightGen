#version 330 core

layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;

out vec2 vTexCoord;

// View-projection matrix (transforms world coordinates to NDC)
uniform mat4 uViewProjection;

// Quad position in world space (top-left corner)
uniform vec2 uQuadWorldPos;

// Quad size in world units (1.0 for standard tiles)
uniform float uQuadWorldSize;

void main()
{
    // Convert unit quad (-1 to 1) to (0 to 1), then scale and position in world space
    // Flip Y because OpenGL quad has Y=-1 at bottom but we want Y=0 (world top) there
    vec2 localPos = vec2((aPosition.x + 1.0) * 0.5, (1.0 - aPosition.y) * 0.5);
    vec2 worldPos = uQuadWorldPos + localPos * uQuadWorldSize;

    // Apply view-projection to get NDC position
    gl_Position = uViewProjection * vec4(worldPos, 0.0, 1.0);
    vTexCoord = aTexCoord;
}
