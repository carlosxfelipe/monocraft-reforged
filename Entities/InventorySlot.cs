using MonoCraft.World;

namespace MonoCraft.Entities;

public struct InventorySlot
{
    public BlockType Type;
    public int Count;

    public InventorySlot(BlockType type, int count)
    {
        Type = type;
        Count = count;
    }
}
