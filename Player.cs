using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoCraft.World;

namespace MonoCraft;

public class Player
{
    // Dimensões do jogador (estilo Minecraft: 0.6 x 1.8)
    private const float Width = 0.6f;
    private const float PlayerHeight = 1.8f;
    private const float EyeHeight = 1.62f;

    private const float Gravity = -24f;
    private const float JumpSpeed = 8.2f;
    private const float WalkSpeed = 4.5f;
    private const float SprintSpeed = 7.5f;
    private const float FlySpeed = 12f;
    private const float MouseSensitivity = 0.0025f;
    private const float StickLookSensitivity = 2.8f; // rad/s

    private readonly VoxelWorld _world;

    public Vector3 Position; // pé do jogador
    public float Yaw;
    public float Pitch;
    public bool IsFlying;

    public bool OnGround => _onGround;
    public float HorizontalSpeed => new Vector2(_velocity.X, _velocity.Z).Length();

    private Vector3 _velocity;
    private bool _onGround;
    private KeyboardState _prevKeyboard;
    private bool _wasInWater;

    private float _distanceMoved;
    private GamePadState _prevPad;

    public Vector3 EyePosition => Position + new Vector3(0, EyeHeight, 0);

    public bool IsHeadInWater
    {
        get
        {
            int x = (int)MathF.Floor(EyePosition.X);
            int y = (int)MathF.Floor(EyePosition.Y);
            int z = (int)MathF.Floor(EyePosition.Z);
            return _world.GetBlock(x, y, z) == BlockType.Water;
        }
    }

    public Vector3 Forward =>
        new(
            MathF.Sin(Yaw) * MathF.Cos(Pitch),
            MathF.Sin(Pitch),
            -MathF.Cos(Yaw) * MathF.Cos(Pitch)
        );

    public Player(VoxelWorld world, Vector3 spawnPosition)
    {
        _world = world;
        Position = spawnPosition;
    }

    public void Update(
        float dt,
        KeyboardState keyboard,
        int mouseDeltaX,
        int mouseDeltaY,
        GamePadState pad
    )
    {
        // Rotação da câmera: mouse + right stick
        Yaw += mouseDeltaX * MouseSensitivity + pad.ThumbSticks.Right.X * StickLookSensitivity * dt;
        Pitch -= mouseDeltaY * MouseSensitivity;
        Pitch += pad.ThumbSticks.Right.Y * StickLookSensitivity * dt;
        Pitch = MathHelper.Clamp(Pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);

        // Alternar voo com F (gamepad: RightStick)
        bool toggleFly =
            (keyboard.IsKeyDown(Keys.F) && _prevKeyboard.IsKeyUp(Keys.F))
            || (
                pad.Buttons.RightStick == ButtonState.Pressed
                && _prevPad.Buttons.RightStick == ButtonState.Released
            );
        if (toggleFly)
        {
            IsFlying = !IsFlying;
            _velocity.Y = 0;
        }

        // Direções no plano XZ
        var forward = new Vector3(MathF.Sin(Yaw), 0, -MathF.Cos(Yaw));
        var right = new Vector3(MathF.Cos(Yaw), 0, MathF.Sin(Yaw));

        Vector3 move = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W))
            move += forward;
        if (keyboard.IsKeyDown(Keys.S))
            move -= forward;
        if (keyboard.IsKeyDown(Keys.D))
            move += right;
        if (keyboard.IsKeyDown(Keys.A))
            move -= right;

        // Left stick: aplica sobre o vetor de movimento
        float lx = pad.ThumbSticks.Left.X;
        float ly = pad.ThumbSticks.Left.Y;
        if (MathF.Abs(lx) > 0.15f || MathF.Abs(ly) > 0.15f)
        {
            move += forward * ly;
            move += right * lx;
        }

        if (move != Vector3.Zero)
            move.Normalize();

        var waterState = GetWaterState();
        bool inWater = waterState.inWater;
        bool upperWater = waterState.upperWater;

        // Som de entrar na água
        if (inWater && !_wasInWater)
            MonoCraft.Entities.Rendering.SoundManager.Play("default_water_footstep.1", 0.5f);
        _wasInWater = inWater;

        bool sprint =
            keyboard.IsKeyDown(Keys.LeftControl) || pad.Buttons.LeftStick == ButtonState.Pressed;

        float speed =
            IsFlying ? FlySpeed
            : inWater ? WalkSpeed * 0.5f
            : sprint ? SprintSpeed
            : WalkSpeed;

        _velocity.X = move.X * speed;
        _velocity.Z = move.Z * speed;

        bool wantUp = keyboard.IsKeyDown(Keys.Space) || pad.Buttons.A == ButtonState.Pressed;
        bool wantDown = keyboard.IsKeyDown(Keys.LeftShift); // B reservado para agachar (não impl.)

        if (IsFlying)
        {
            _velocity.Y = 0;
            if (wantUp)
                _velocity.Y = FlySpeed;
            if (wantDown)
                _velocity.Y = -FlySpeed;
        }
        else if (inWater)
        {
            _velocity.Y += Gravity * 0.3f * dt;
            _velocity.Y *= 0.9f;

            if (wantUp)
            {
                _velocity.Y += 20.0f * dt;
                if (!upperWater && _velocity.Y < JumpSpeed * 0.6f)
                    _velocity.Y = JumpSpeed * 0.6f;
            }
        }
        else
        {
            _velocity.Y += Gravity * dt;
            _velocity.Y = MathF.Max(_velocity.Y, -50f);

            if (wantUp && _onGround)
            {
                _velocity.Y = JumpSpeed;
                _onGround = false;
            }
        }

        // Movimento com colisão (eixo por eixo)
        Vector3 oldPos = Position;
        MoveAxis(_velocity.X * dt, 0);
        MoveAxis(_velocity.Y * dt, 1);
        MoveAxis(_velocity.Z * dt, 2);

        // Som de passos
        if (_onGround && !IsFlying && !inWater)
        {
            Vector2 movedXZ = new Vector2(Position.X - oldPos.X, Position.Z - oldPos.Z);
            _distanceMoved += movedXZ.Length();
            if (_distanceMoved > 1.5f)
            {
                _distanceMoved = 0;

                int bX = (int)MathF.Floor(Position.X);
                int bY = (int)MathF.Floor(Position.Y - 0.1f);
                int bZ = (int)MathF.Floor(Position.Z);
                var blockUnder = _world.GetBlock(bX, bY, bZ);

                string soundName = GetFootstepSound(blockUnder);

                // Varia levemente o pitch para não ficar monótono
                float pitch = (float)(new Random().NextDouble() * 0.2 - 0.1);
                MonoCraft.Entities.Rendering.SoundManager.Play(soundName, 0.4f, pitch);
            }
        }
        else
        {
            _distanceMoved = 0;
        }

        // Respawn se cair do mundo
        if (Position.Y < -10)
        {
            int h = _world.Generator.GetHeight((int)Position.X, (int)Position.Z);
            Position = new Vector3(Position.X, h + 2, Position.Z);
            _velocity = Vector3.Zero;
        }

        _prevKeyboard = keyboard;
        _prevPad = pad;
    }

    private void MoveAxis(float amount, int axis)
    {
        if (amount == 0)
            return;

        Vector3 newPos = Position;
        switch (axis)
        {
            case 0:
                newPos.X += amount;
                break;
            case 1:
                newPos.Y += amount;
                break;
            case 2:
                newPos.Z += amount;
                break;
        }

        if (!CollidesWithWorld(newPos))
        {
            Position = newPos;
            if (axis == 1)
                _onGround = false;
        }
        else
        {
            // Auto-jump para subir automaticamente blocos de 1 de altura (como degraus)
            var waterState = GetWaterState();
            bool canAutoJump = _onGround || waterState.inWater;

            if ((axis == 0 || axis == 2) && canAutoJump && !IsFlying)
            {
                Vector3 stepPos = newPos;
                stepPos.Y += 1.001f;
                Vector3 headCheck = Position;
                headCheck.Y += 1.001f;

                // Se houver espaço para pular sem bater a cabeça, simulamos um pulo
                if (!CollidesWithWorld(headCheck) && !CollidesWithWorld(stepPos))
                {
                    if (waterState.inWater)
                    {
                        _velocity.Y = JumpSpeed * 0.6f;
                    }
                    else
                    {
                        _velocity.Y = JumpSpeed;
                        _onGround = false;
                    }
                    return;
                }
            }

            if (axis == 1)
            {
                if (amount < 0)
                    _onGround = true;
                _velocity.Y = 0;
            }
        }
    }

    private bool CollidesWithWorld(Vector3 pos)
    {
        float half = Width / 2f;
        int minX = (int)MathF.Floor(pos.X - half);
        int maxX = (int)MathF.Floor(pos.X + half);
        int minY = (int)MathF.Floor(pos.Y);
        int maxY = (int)MathF.Floor(pos.Y + PlayerHeight);
        int minZ = (int)MathF.Floor(pos.Z - half);
        int maxZ = (int)MathF.Floor(pos.Z + half);

        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                for (int z = minZ; z <= maxZ; z++)
                    if (_world.IsSolid(x, y, z))
                        return true;

        return false;
    }

    /// <summary>Verifica se colocar um bloco em (x,y,z) intersectaria o jogador.</summary>
    public bool IntersectsBlock(int x, int y, int z)
    {
        float half = Width / 2f;
        return x + 1 > Position.X - half
            && x < Position.X + half
            && y + 1 > Position.Y
            && y < Position.Y + PlayerHeight
            && z + 1 > Position.Z - half
            && z < Position.Z + half;
    }

    private string GetFootstepSound(BlockType blockType)
    {
        return blockType switch
        {
            BlockType.Sand => "default_sand_footstep.1",
            BlockType.Stone or BlockType.Bedrock => "default_stone_footstep.1",
            BlockType.Wood => "default_wood_footstep.1",
            BlockType.Snow => "default_snow_footstep.1",
            BlockType.Dirt => "default_dirt_footstep.1",
            BlockType.Leaves => "default_grass_footstep.1",
            _ => "default_grass_footstep.1", // Padrao (grass, air, unknown)
        };
    }

    private (bool inWater, bool upperWater) GetWaterState()
    {
        int x = (int)MathF.Floor(Position.X);
        int z = (int)MathF.Floor(Position.Z);

        // Verifica a metade inferior e superior do corpo do jogador
        int y1 = (int)MathF.Floor(Position.Y + 0.2f);
        int y2 = (int)MathF.Floor(Position.Y + 1.2f);

        bool lw = _world.GetBlock(x, y1, z) == BlockType.Water;
        bool uw = _world.GetBlock(x, y2, z) == BlockType.Water;

        return (lw || uw, uw);
    }
}
