using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoCraft.Entities.AI;
using MonoCraft.World;

namespace MonoCraft.Entities;

public class Mob
{
    private const float Gravity = -24f;

    // Dimensões padrão (estilo quadrúpede genérico)
    public const float DefaultWidth = 0.9f;
    public const float DefaultHeight = 1.4f;

    public string Identifier { get; }
    public float Width { get; }
    public float Height { get; }
    public float MaxHealth { get; }
    public bool IsAquatic { get; }

    public Vector3 Position;
    public Vector3 Velocity;
    public float Yaw;     // rotação horizontal (radianos)
    public float Health;
    public bool IsAlive => Health > 0;

    // Tempo acumulado de animação
    public float AnimTime;
    public string ActiveAnimRole = "walk";

    private readonly VoxelWorld _world;
    private readonly List<IBehaviorComponent> _behaviors;

    public Mob(string identifier, Vector3 spawnPosition, VoxelWorld world,
               float width = DefaultWidth, float height = DefaultHeight,
               float maxHealth = 10f, bool isAquatic = false,
               IEnumerable<IBehaviorComponent> behaviors = null)
    {
        Identifier = identifier;
        Width = width;
        Height = height;
        MaxHealth = maxHealth;
        IsAquatic = isAquatic;
        Position = spawnPosition;
        Health = maxHealth;
        _world = world;
        _behaviors = new List<IBehaviorComponent>(behaviors ?? Array.Empty<IBehaviorComponent>());
        _behaviors.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public void Update(float dt)
    {
        foreach (var b in _behaviors)
            b.Update(this, dt);

        bool inWater = IsInWater();

        if (inWater)
        {
            Velocity.Y += Gravity * 0.3f * dt;
            Velocity.Y *= 0.9f;

            if (!IsAquatic)
                Velocity.Y += 15.0f * dt;
        }
        else
        {
            Velocity.Y += Gravity * dt;
        }

        Velocity.Y = MathF.Max(Velocity.Y, -50f);

        float speedMod = inWater ? 0.4f : 1f;

        MoveAxis(Velocity.X * speedMod * dt, 0);
        MoveAxis(Velocity.Y * dt, 1);
        MoveAxis(Velocity.Z * speedMod * dt, 2);

        if (Velocity.X != 0 || Velocity.Z != 0)
            Yaw = MathF.Atan2(Velocity.X, Velocity.Z);

        if (Position.Y < -10)
        {
            int h = _world.Generator.GetHeight((int)Position.X, (int)Position.Z);
            Position = new Vector3(Position.X, h + 2, Position.Z);
            Velocity = Vector3.Zero;
        }

        float horizSpeed = MathF.Sqrt(Velocity.X * Velocity.X + Velocity.Z * Velocity.Z);
        if (horizSpeed > 0.05f || inWater)
            AnimTime += dt;
    }

    private bool IsInWater()
    {
        int x = (int)MathF.Floor(Position.X);
        int y = (int)MathF.Floor(Position.Y + (Height * 0.8f));
        int z = (int)MathF.Floor(Position.Z);
        return _world.GetBlock(x, y, z) == BlockType.Water;
    }

    public void Damage(float amount)
    {
        Health = MathF.Max(0, Health - amount);
    }

    private void MoveAxis(float amount, int axis)
    {
        if (amount == 0) return;

        Vector3 newPos = Position;
        switch (axis)
        {
            case 0: newPos.X += amount; break;
            case 1: newPos.Y += amount; break;
            case 2: newPos.Z += amount; break;
        }

        if (!CollidesWithWorld(newPos))
        {
            Position = newPos;
        }
        else
        {
            if (axis == 1)
                Velocity.Y = 0;
            else
            {
                if (axis == 0) Velocity.X = 0;
                else Velocity.Z = 0;
            }
        }
    }

    private bool CollidesWithWorld(Vector3 pos)
    {
        float half = Width / 2f;
        int minX = (int)MathF.Floor(pos.X - half);
        int maxX = (int)MathF.Floor(pos.X + half);
        int minY = (int)MathF.Floor(pos.Y);
        int maxY = (int)MathF.Floor(pos.Y + Height);
        int minZ = (int)MathF.Floor(pos.Z - half);
        int maxZ = (int)MathF.Floor(pos.Z + half);

        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                for (int z = minZ; z <= maxZ; z++)
                    if (_world.IsSolid(x, y, z))
                        return true;

        return false;
    }
}
