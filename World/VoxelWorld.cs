using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoCraft.World;

public class VoxelWorld
{
    public const int RenderDistance = 6; // em chunks

    private readonly Dictionary<(int, int), Chunk> _chunks = new();
    private readonly TerrainGenerator _generator;

    public TerrainGenerator Generator => _generator;

    public VoxelWorld(int seed)
    {
        _generator = new TerrainGenerator(seed);
    }

    private static int FloorDiv(int a, int b) => (int)MathF.Floor(a / (float)b);

    private static int Mod(int a, int b)
    {
        int m = a % b;
        return m < 0 ? m + b : m;
    }

    public Chunk GetOrCreateChunk(int chunkX, int chunkZ)
    {
        if (_chunks.TryGetValue((chunkX, chunkZ), out var chunk))
            return chunk;

        chunk = new Chunk(this, chunkX, chunkZ);
        chunk.Generate(_generator);
        _chunks[(chunkX, chunkZ)] = chunk;
        return chunk;
    }

    public Chunk GetChunk(int chunkX, int chunkZ)
    {
        _chunks.TryGetValue((chunkX, chunkZ), out var chunk);
        return chunk;
    }

    public BlockType GetBlock(int x, int y, int z)
    {
        if (y < 0 || y >= Chunk.Height)
            return BlockType.Air;

        int cx = FloorDiv(x, Chunk.Size);
        int cz = FloorDiv(z, Chunk.Size);
        var chunk = GetChunk(cx, cz);
        if (chunk == null)
            return _generator.GetBlock(x, y, z); // fora dos chunks carregados: amostra do gerador

        return chunk.GetBlock(Mod(x, Chunk.Size), y, Mod(z, Chunk.Size));
    }

    public void SetBlock(int x, int y, int z, BlockType type)
    {
        if (y < 0 || y >= Chunk.Height)
            return;

        int cx = FloorDiv(x, Chunk.Size);
        int cz = FloorDiv(z, Chunk.Size);
        var chunk = GetOrCreateChunk(cx, cz);
        int lx = Mod(x, Chunk.Size);
        int lz = Mod(z, Chunk.Size);
        chunk.SetBlock(lx, y, lz, type);

        // Marca vizinhos como sujos se o bloco está na borda
        if (lx == 0)
            GetChunk(cx - 1, cz)?.MarkDirty();
        if (lx == Chunk.Size - 1)
            GetChunk(cx + 1, cz)?.MarkDirty();
        if (lz == 0)
            GetChunk(cx, cz - 1)?.MarkDirty();
        if (lz == Chunk.Size - 1)
            GetChunk(cx, cz + 1)?.MarkDirty();
    }

    public bool IsSolid(int x, int y, int z) => BlockInfo.IsSolid(GetBlock(x, y, z));

    /// <summary>
    /// Garante que os chunks ao redor da posição estão carregados (no máx. N novos por frame).
    /// </summary>
    public void EnsureChunksAround(Vector3 position, int maxNewPerFrame = 2)
    {
        int centerX = FloorDiv((int)MathF.Floor(position.X), Chunk.Size);
        int centerZ = FloorDiv((int)MathF.Floor(position.Z), Chunk.Size);

        int created = 0;
        // Espiral simples: do centro para fora
        for (int r = 0; r <= RenderDistance && created < maxNewPerFrame; r++)
        {
            for (int dx = -r; dx <= r && created < maxNewPerFrame; dx++)
            {
                for (int dz = -r; dz <= r && created < maxNewPerFrame; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r)
                        continue;
                    if (!_chunks.ContainsKey((centerX + dx, centerZ + dz)))
                    {
                        GetOrCreateChunk(centerX + dx, centerZ + dz);
                        created++;
                    }
                }
            }
        }
    }

    public void RebuildDirtyMeshes(GraphicsDevice device, int maxPerFrame = 4)
    {
        int rebuilt = 0;
        foreach (var chunk in _chunks.Values)
        {
            if (!chunk.IsDirty)
                continue;
            chunk.RebuildMesh(device);
            if (++rebuilt >= maxPerFrame)
                break;
        }
    }

    public void Draw(GraphicsDevice device, BasicEffect effect, Vector3 cameraPos, Action drawEntities = null)
    {
        int centerX = FloorDiv((int)MathF.Floor(cameraPos.X), Chunk.Size);
        int centerZ = FloorDiv((int)MathF.Floor(cameraPos.Z), Chunk.Size);

        // Passo 1: opacos
        foreach (var chunk in _chunks.Values)
        {
            if (!InRange(chunk, centerX, centerZ))
                continue;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                chunk.DrawOpaque(device);
            }
        }

        // Intercala o desenho das entidades (opacas) antes da água transparente
        drawEntities?.Invoke();

        // Passo 2: água (transparente)
        device.BlendState = BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.DepthRead;
        effect.Alpha = 0.6f;
        foreach (var chunk in _chunks.Values)
        {
            if (!InRange(chunk, centerX, centerZ))
                continue;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                chunk.DrawWater(device);
            }
        }
        effect.Alpha = 1f;
        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;
    }

    private static bool InRange(Chunk chunk, int centerX, int centerZ) =>
        Math.Max(Math.Abs(chunk.ChunkX - centerX), Math.Abs(chunk.ChunkZ - centerZ))
        <= RenderDistance;

    /// <summary>
    /// Raycast estilo DDA (Amanatides &amp; Woo). Retorna o bloco atingido e a face (normal).
    /// </summary>
    public bool Raycast(
        Vector3 origin,
        Vector3 direction,
        float maxDistance,
        out Point3 hitBlock,
        out Point3 faceNormal
    )
    {
        hitBlock = default;
        faceNormal = default;

        direction.Normalize();

        int x = (int)MathF.Floor(origin.X);
        int y = (int)MathF.Floor(origin.Y);
        int z = (int)MathF.Floor(origin.Z);

        int stepX = MathF.Sign(direction.X) >= 0 ? 1 : -1;
        int stepY = MathF.Sign(direction.Y) >= 0 ? 1 : -1;
        int stepZ = MathF.Sign(direction.Z) >= 0 ? 1 : -1;

        float tDeltaX = direction.X == 0 ? float.MaxValue : MathF.Abs(1f / direction.X);
        float tDeltaY = direction.Y == 0 ? float.MaxValue : MathF.Abs(1f / direction.Y);
        float tDeltaZ = direction.Z == 0 ? float.MaxValue : MathF.Abs(1f / direction.Z);

        float tMaxX =
            direction.X == 0
                ? float.MaxValue
                : (stepX > 0 ? (x + 1 - origin.X) : (origin.X - x)) * tDeltaX;
        float tMaxY =
            direction.Y == 0
                ? float.MaxValue
                : (stepY > 0 ? (y + 1 - origin.Y) : (origin.Y - y)) * tDeltaY;
        float tMaxZ =
            direction.Z == 0
                ? float.MaxValue
                : (stepZ > 0 ? (z + 1 - origin.Z) : (origin.Z - z)) * tDeltaZ;

        float t = 0;
        Point3 normal = default;

        while (t <= maxDistance)
        {
            var block = GetBlock(x, y, z);
            if (BlockInfo.IsSolid(block))
            {
                hitBlock = new Point3(x, y, z);
                faceNormal = normal;
                return true;
            }

            if (tMaxX < tMaxY && tMaxX < tMaxZ)
            {
                x += stepX;
                t = tMaxX;
                tMaxX += tDeltaX;
                normal = new Point3(-stepX, 0, 0);
            }
            else if (tMaxY < tMaxZ)
            {
                y += stepY;
                t = tMaxY;
                tMaxY += tDeltaY;
                normal = new Point3(0, -stepY, 0);
            }
            else
            {
                z += stepZ;
                t = tMaxZ;
                tMaxZ += tDeltaZ;
                normal = new Point3(0, 0, -stepZ);
            }
        }

        return false;
    }
}

public readonly struct Point3
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z;

    public Point3(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}
