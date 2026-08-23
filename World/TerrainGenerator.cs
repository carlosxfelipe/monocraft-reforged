using System;

namespace MonoCraft.World;

/// <summary>
/// Gera o terreno usando ruído de valor (value noise) com várias oitavas.
/// </summary>
public class TerrainGenerator
{
    private readonly int _seed;

    public const int SeaLevel = 28;

    public TerrainGenerator(int seed)
    {
        _seed = seed;
    }

    // Hash determinístico -> [0,1)
    private float Hash(int x, int z)
    {
        unchecked
        {
            int h = _seed;
            h = h * 374761393 + x * 668265263;
            h = h * 374761393 + z * 2147483647;
            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;
            return (h & 0x7FFFFFFF) / (float)int.MaxValue;
        }
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private float ValueNoise(float x, float z)
    {
        int x0 = (int)MathF.Floor(x);
        int z0 = (int)MathF.Floor(z);
        float tx = Smooth(x - x0);
        float tz = Smooth(z - z0);

        float a = Hash(x0, z0);
        float b = Hash(x0 + 1, z0);
        float c = Hash(x0, z0 + 1);
        float d = Hash(x0 + 1, z0 + 1);

        float ab = a + (b - a) * tx;
        float cd = c + (d - c) * tx;
        return ab + (cd - ab) * tz;
    }

    private float FractalNoise(float x, float z, int octaves, float frequency, float persistence)
    {
        float total = 0f;
        float amplitude = 1f;
        float maxValue = 0f;
        for (int i = 0; i < octaves; i++)
        {
            total += ValueNoise(x * frequency, z * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2f;
        }
        return total / maxValue;
    }

    public int GetHeight(int worldX, int worldZ)
    {
        float n = FractalNoise(worldX, worldZ, octaves: 4, frequency: 0.008f, persistence: 0.5f);
        // Acentua montanhas
        n = MathF.Pow(n, 1.6f);
        return 12 + (int)(n * 44);
    }

    public BlockType GetBlock(int worldX, int worldY, int worldZ)
    {
        int height = GetHeight(worldX, worldZ);

        if (worldY == 0)
            return BlockType.Bedrock;

        if (worldY > height)
            return worldY <= SeaLevel ? BlockType.Water : BlockType.Air;

        // Coluna sólida
        if (worldY == height)
        {
            if (height >= 48)
                return BlockType.Snow;
            if (height <= SeaLevel + 1)
                return BlockType.Sand;
            return BlockType.Grass;
        }

        if (worldY >= height - 3)
            return height <= SeaLevel + 1 ? BlockType.Sand : BlockType.Dirt;

        return BlockType.Stone;
    }

    /// <summary>Decide se uma árvore nasce nesta coluna (espaçadas e determinísticas).</summary>
    public bool HasTree(int worldX, int worldZ)
    {
        int height = GetHeight(worldX, worldZ);
        if (height <= SeaLevel + 1 || height >= 48)
            return false;
        return Hash(worldX * 7 + 13, worldZ * 11 + 17) > 0.985f;
    }
}
