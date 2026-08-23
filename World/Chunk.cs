using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoCraft.World;

public class Chunk
{
    public const int Size = 16; // X e Z
    public const int Height = 64; // Y

    public int ChunkX { get; }
    public int ChunkZ { get; }

    private readonly BlockType[] _blocks = new BlockType[Size * Height * Size];
    private readonly VoxelWorld _world;

    private VertexBuffer _opaqueBuffer;
    private VertexBuffer _waterBuffer;
    private int _opaqueVertexCount;
    private int _waterVertexCount;

    public bool IsDirty { get; set; } = true;

    public void MarkDirty() => IsDirty = true;

    public Chunk(VoxelWorld world, int chunkX, int chunkZ)
    {
        _world = world;
        ChunkX = chunkX;
        ChunkZ = chunkZ;
    }

    private static int Index(int x, int y, int z) => (y * Size + z) * Size + x;

    public BlockType GetBlock(int x, int y, int z)
    {
        if (y < 0 || y >= Height)
            return BlockType.Air;
        if (x < 0 || x >= Size || z < 0 || z >= Size)
            return _world.GetBlock(ChunkX * Size + x, y, ChunkZ * Size + z);
        return _blocks[Index(x, y, z)];
    }

    public void SetBlock(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= Size || y < 0 || y >= Height || z < 0 || z >= Size)
            return;
        _blocks[Index(x, y, z)] = type;
        IsDirty = true;
    }

    public void Generate(TerrainGenerator generator)
    {
        int baseX = ChunkX * Size;
        int baseZ = ChunkZ * Size;

        for (int x = 0; x < Size; x++)
            for (int z = 0; z < Size; z++)
                for (int y = 0; y < Height; y++)
                    _blocks[Index(x, y, z)] = generator.GetBlock(baseX + x, y, baseZ + z);

        // Árvores (geramos folhas de árvores vizinhas que caem dentro deste chunk para evitar que sejam cortadas nas bordas)
        for (int x = -2; x < Size + 2; x++)
            for (int z = -2; z < Size + 2; z++)
            {
                int wx = baseX + x;
                int wz = baseZ + z;
                if (!generator.HasTree(wx, wz))
                    continue;

                int ground = generator.GetHeight(wx, wz);
                int trunkH = 4 + (wx * 31 + wz * 17 & 1);

                // Desenha o tronco se ele estiver estritamente dentro deste chunk
                if (x >= 0 && x < Size && z >= 0 && z < Size)
                {
                    for (int t = 1; t <= trunkH; t++)
                        TrySet(x, ground + t, z, BlockType.Wood);
                }

                // Desenha as folhas se elas caírem dentro dos limites deste chunk
                int top = ground + trunkH;
                for (int lx = -2; lx <= 2; lx++)
                    for (int lz = -2; lz <= 2; lz++)
                        for (int ly = 0; ly <= 2; ly++)
                        {
                            int dist = Math.Abs(lx) + Math.Abs(lz) + ly;
                            if (dist > 3 || (lx == 0 && lz == 0 && ly <= 0))
                                continue;

                            int leafLocalX = x + lx;
                            int leafLocalZ = z + lz;

                            if (leafLocalX >= 0 && leafLocalX < Size && leafLocalZ >= 0 && leafLocalZ < Size)
                            {
                                TrySet(leafLocalX, top + ly, leafLocalZ, BlockType.Leaves);
                            }
                        }
            }

        IsDirty = true;
    }

    private void TrySet(int x, int y, int z, BlockType type)
    {
        if (x < 0 || x >= Size || y < 0 || y >= Height || z < 0 || z >= Size)
            return;
        if (_blocks[Index(x, y, z)] == BlockType.Air)
            _blocks[Index(x, y, z)] = type;
    }

    public void RebuildMesh(GraphicsDevice device)
    {
        var opaque = new List<VertexPositionColor>();
        var water = new List<VertexPositionColor>();

        int baseX = ChunkX * Size;
        int baseZ = ChunkZ * Size;

        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Height; y++)
                for (int z = 0; z < Size; z++)
                {
                    BlockType block = _blocks[Index(x, y, z)];
                    if (block == BlockType.Air)
                        continue;

                    var list = block == BlockType.Water ? water : opaque;
                    var pos = new Vector3(baseX + x, y, baseZ + z);
                    bool shaded = block != BlockType.Water;

                    // Para cada face, só desenha se o vizinho não for opaco
                    // +Y (topo)
                    if (ShouldDrawFace(block, GetBlock(x, y + 1, z)))
                        AddFace(
                            list,
                            pos,
                            x,
                            y,
                            z,
                            FaceTop,
                            Shade(BlockInfo.GetTopColor(block), 1.0f),
                            shaded
                        );
                    // -Y (fundo)
                    if (ShouldDrawFace(block, GetBlock(x, y - 1, z)))
                        AddFace(
                            list,
                            pos,
                            x,
                            y,
                            z,
                            FaceBottom,
                            Shade(BlockInfo.GetBottomColor(block), 0.5f),
                            shaded
                        );
                    // +X
                    if (ShouldDrawFace(block, GetBlock(x + 1, y, z)))
                        AddFace(
                            list,
                            pos,
                            x,
                            y,
                            z,
                            FaceEast,
                            Shade(BlockInfo.GetSideColor(block), 0.8f),
                            shaded
                        );
                    // -X
                    if (ShouldDrawFace(block, GetBlock(x - 1, y, z)))
                        AddFace(
                            list,
                            pos,
                            x,
                            y,
                            z,
                            FaceWest,
                            Shade(BlockInfo.GetSideColor(block), 0.8f),
                            shaded
                        );
                    // +Z
                    if (ShouldDrawFace(block, GetBlock(x, y, z + 1)))
                        AddFace(
                            list,
                            pos,
                            x,
                            y,
                            z,
                            FaceSouth,
                            Shade(BlockInfo.GetSideColor(block), 0.65f),
                            shaded
                        );
                    // -Z
                    if (ShouldDrawFace(block, GetBlock(x, y, z - 1)))
                        AddFace(
                            list,
                            pos,
                            x,
                            y,
                            z,
                            FaceNorth,
                            Shade(BlockInfo.GetSideColor(block), 0.65f),
                            shaded
                        );
                }

        _opaqueBuffer?.Dispose();
        _waterBuffer?.Dispose();
        _opaqueBuffer = null;
        _waterBuffer = null;

        _opaqueVertexCount = opaque.Count;
        if (opaque.Count > 0)
        {
            _opaqueBuffer = new VertexBuffer(
                device,
                typeof(VertexPositionColor),
                opaque.Count,
                BufferUsage.WriteOnly
            );
            _opaqueBuffer.SetData(opaque.ToArray());
        }

        _waterVertexCount = water.Count;
        if (water.Count > 0)
        {
            _waterBuffer = new VertexBuffer(
                device,
                typeof(VertexPositionColor),
                water.Count,
                BufferUsage.WriteOnly
            );
            _waterBuffer.SetData(water.ToArray());
        }

        IsDirty = false;
    }

    private static bool ShouldDrawFace(BlockType current, BlockType neighbor)
    {
        if (current == BlockType.Water)
            return neighbor == BlockType.Air;
        return !BlockInfo.IsOpaque(neighbor) && neighbor != current;
    }

    private static Color Shade(Color c, float factor) =>
        new Color((int)(c.R * factor), (int)(c.G * factor), (int)(c.B * factor), c.A);

    // Sombreamento: ambient occlusion por vértice + sombra de céu por face
    private static readonly float[] AoFactors = { 0.55f, 0.72f, 0.86f, 1f };
    private const float SkyShadowFactor = 0.72f;

    private readonly record struct FaceDef(int Nx, int Ny, int Nz, (int x, int y, int z)[] Corners);

    // Cantos na ordem a, b, c, d (winding clockwise para CullCounterClockwise)
    private static readonly FaceDef FaceTop = new(
        0,
        1,
        0,
        new[] { (0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0) }
    );
    private static readonly FaceDef FaceBottom = new(
        0,
        -1,
        0,
        new[] { (0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1) }
    );
    private static readonly FaceDef FaceEast = new(
        1,
        0,
        0,
        new[] { (1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1) }
    );
    private static readonly FaceDef FaceWest = new(
        -1,
        0,
        0,
        new[] { (0, 0, 1), (0, 1, 1), (0, 1, 0), (0, 0, 0) }
    );
    private static readonly FaceDef FaceSouth = new(
        0,
        0,
        1,
        new[] { (1, 0, 1), (1, 1, 1), (0, 1, 1), (0, 0, 1) }
    );
    private static readonly FaceDef FaceNorth = new(
        0,
        0,
        -1,
        new[] { (0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0) }
    );

    private void AddFace(
        List<VertexPositionColor> list,
        Vector3 p,
        int x,
        int y,
        int z,
        in FaceDef face,
        Color baseColor,
        bool shaded
    )
    {
        float skyShadow = 1f;
        if (shaded && IsInShadow(x + face.Nx, y + face.Ny, z + face.Nz))
            skyShadow = SkyShadowFactor;

        var positions = new Vector3[4];
        var colors = new Color[4];
        for (int i = 0; i < 4; i++)
        {
            var (ox, oy, oz) = face.Corners[i];
            positions[i] = p + new Vector3(ox, oy, oz);
            float f = skyShadow;
            if (shaded)
                f *= AoFactors[VertexAo(x, y, z, face, i)];
            colors[i] = Shade(baseColor, f);
        }

        AddQuad(list, positions, colors);
    }

    /// <summary>Ambient occlusion clássico (0 = mais escuro, 3 = sem oclusão).</summary>
    private int VertexAo(int x, int y, int z, in FaceDef face, int cornerIndex)
    {
        var (ox, oy, oz) = face.Corners[cornerIndex];
        int bx = x + face.Nx;
        int by = y + face.Ny;
        int bz = z + face.Nz;

        int a1x = 0,
            a1y = 0,
            a1z = 0;
        int a2x = 0,
            a2y = 0,
            a2z = 0;
        if (face.Nx != 0)
        {
            a1y = oy == 1 ? 1 : -1;
            a2z = oz == 1 ? 1 : -1;
        }
        else if (face.Ny != 0)
        {
            a1x = ox == 1 ? 1 : -1;
            a2z = oz == 1 ? 1 : -1;
        }
        else
        {
            a1x = ox == 1 ? 1 : -1;
            a2y = oy == 1 ? 1 : -1;
        }

        bool s1 = Occludes(bx + a1x, by + a1y, bz + a1z);
        bool s2 = Occludes(bx + a2x, by + a2y, bz + a2z);
        bool corner = Occludes(bx + a1x + a2x, by + a1y + a2y, bz + a1z + a2z);

        if (s1 && s2)
            return 0;
        return 3 - (s1 ? 1 : 0) - (s2 ? 1 : 0) - (corner ? 1 : 0);
    }

    private bool Occludes(int x, int y, int z) => BlockInfo.IsOpaque(GetBlock(x, y, z));

    /// <summary>Sombra de céu: há algum bloco sólido acima desta célula?</summary>
    private bool IsInShadow(int x, int y, int z)
    {
        for (int yy = y + 1; yy < Height; yy++)
            if (BlockInfo.IsSolid(GetBlock(x, yy, z)))
                return true;
        return false;
    }

    // Cada face = 2 triângulos (6 vértices)
    private static void AddQuad(List<VertexPositionColor> list, Vector3[] pos, Color[] colors)
    {
        list.Add(new VertexPositionColor(pos[0], colors[0]));
        list.Add(new VertexPositionColor(pos[2], colors[2]));
        list.Add(new VertexPositionColor(pos[1], colors[1]));
        list.Add(new VertexPositionColor(pos[0], colors[0]));
        list.Add(new VertexPositionColor(pos[3], colors[3]));
        list.Add(new VertexPositionColor(pos[2], colors[2]));
    }

    public void DrawOpaque(GraphicsDevice device)
    {
        if (_opaqueBuffer == null || _opaqueVertexCount == 0)
            return;
        device.SetVertexBuffer(_opaqueBuffer);
        device.DrawPrimitives(PrimitiveType.TriangleList, 0, _opaqueVertexCount / 3);
    }

    public void DrawWater(GraphicsDevice device)
    {
        if (_waterBuffer == null || _waterVertexCount == 0)
            return;
        device.SetVertexBuffer(_waterBuffer);
        device.DrawPrimitives(PrimitiveType.TriangleList, 0, _waterVertexCount / 3);
    }

    public void Dispose()
    {
        _opaqueBuffer?.Dispose();
        _waterBuffer?.Dispose();
    }
}
