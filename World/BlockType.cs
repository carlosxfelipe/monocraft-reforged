using Microsoft.Xna.Framework;

namespace MonoCraft.World;

public enum BlockType : byte
{
    Air = 0,
    Grass,
    Dirt,
    Stone,
    Sand,
    Water,
    Wood,
    Leaves,
    Bedrock,
    Snow,
}

public static class BlockInfo
{
    public static bool IsSolid(BlockType type) => type != BlockType.Air && type != BlockType.Water;

    public static bool IsOpaque(BlockType type) =>
        type != BlockType.Air && type != BlockType.Water && type != BlockType.Leaves;

    // Blocos que podem ser substituídos ao colocar outro bloco (água some, como no Minecraft).
    // Quando o balde for implementado, ele será a única forma de coletar/colocar água.
    public static bool IsReplaceable(BlockType type) =>
        type == BlockType.Air || type == BlockType.Water;

    // O que o bloco vira ao ser quebrado (grama dropa terra, como no Minecraft)
    public static BlockType GetDrop(BlockType type) =>
        type switch
        {
            BlockType.Grass => BlockType.Dirt,
            _ => type,
        };

    // Cor do topo e cor lateral (estilo Minecraft: grama verde no topo, terra dos lados)
    public static Color GetTopColor(BlockType type) =>
        type switch
        {
            BlockType.Grass => new Color(95, 159, 53),
            BlockType.Dirt => new Color(134, 96, 67),
            BlockType.Stone => new Color(125, 125, 125),
            BlockType.Sand => new Color(219, 207, 163),
            BlockType.Water => new Color(52, 95, 218),
            BlockType.Wood => new Color(102, 81, 50),
            BlockType.Leaves => new Color(60, 143, 47),
            BlockType.Bedrock => new Color(60, 60, 60),
            BlockType.Snow => new Color(240, 240, 245),
            _ => Color.Magenta,
        };

    public static Color GetSideColor(BlockType type) =>
        type switch
        {
            BlockType.Grass => new Color(116, 92, 64),
            BlockType.Wood => new Color(110, 88, 56),
            _ => GetTopColor(type),
        };

    public static Color GetBottomColor(BlockType type) =>
        type switch
        {
            BlockType.Grass => new Color(134, 96, 67),
            _ => GetTopColor(type),
        };
}
