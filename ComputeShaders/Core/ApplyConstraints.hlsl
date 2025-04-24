float3 ApplyConstraints(float3 id, float3 position)
{
    position.y = max(position.y, id.y * 0.001); // Prevent going below the ground plane

    return position;
}