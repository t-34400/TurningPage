float3 ApplyConstraints(float3 id, float3 position)
{
    position.y = max(position.y, id.y * 1e-5); // Prevent going below the ground plane

    return position;
}