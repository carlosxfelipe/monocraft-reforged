using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoCraft.World;

namespace MonoCraft;

public class Hand
{
    private static readonly Color SleeveColor = new(40, 90, 170); // Camisa azul
    private const float SwingDuration = 0.3f;

    private float _swing = -1f;
    private float _bobPhase;
    private float _bobAmount;
    private float _equip = 1f;
    private BlockType _heldItemType = BlockType.Air;
    private Vector2 _sway;

    private readonly List<VertexPositionColor> _arm = new();
    private readonly List<VertexPositionColor> _item = new();

    public Hand(Color skinColor)
    {
        // Braço
        AddBoxGeometry(_arm, new Vector3(-0.05f, -0.05f, -0.06f), new Vector3(0.05f, 0.05f, 0.30f), skinColor, true);
        // Manga
        AddBoxGeometry(_arm, new Vector3(-0.056f, -0.056f, 0.16f), new Vector3(0.056f, 0.056f, 0.6f), SleeveColor, true);
    }

    public void Swing()
    {
        if (_swing < 0 || _swing > SwingDuration * 0.5f)
            _swing = 0f;
    }

    public void Update(float dt, float speed, bool onGround, Vector2 look, BlockType heldItemType)
    {
        if (_swing >= 0)
        {
            _swing += dt;
            if (_swing >= SwingDuration)
                _swing = -1f;
        }

        float target = onGround ? MathHelper.Clamp(speed / 4f, 0f, 1.6f) : 0f;
        _bobAmount = MathHelper.Lerp(_bobAmount, target, 1f - MathF.Exp(-10f * dt));
        if (onGround && speed > 0.1f)
            _bobPhase += dt * (3f + speed * 1.6f);

        var swayTarget = Vector2.Clamp(look * 0.0015f, new Vector2(-0.06f), new Vector2(0.06f));
        _sway = Vector2.Lerp(_sway, swayTarget, 1f - MathF.Exp(-12f * dt));

        if (heldItemType != _heldItemType)
        {
            _heldItemType = heldItemType;
            _equip = 0f;
            _item.Clear();
            if (heldItemType != BlockType.Air)
            {
                // Um bloco genérico simples para a mão
                Color blockColor = BlockInfo.GetSideColor(heldItemType);
                AddBoxGeometry(_item, new Vector3(-0.05f, -0.05f, -0.05f), new Vector3(0.05f, 0.05f, 0.05f), blockColor, true);
            }
        }
        _equip = MathF.Min(1f, _equip + dt * 5f);
    }

    public void Draw(GraphicsDevice device, BasicEffect effect)
    {
        float s = _swing >= 0 ? _swing / SwingDuration : 0f;
        float f = MathF.Sin(s * MathHelper.Pi);
        float f2 = MathF.Sin(MathF.Sqrt(s) * MathHelper.Pi);

        Vector3 pos = new(0.24f, -0.22f, -0.42f);
        pos += new Vector3(MathF.Sin(_bobPhase) * 0.012f, -MathF.Abs(MathF.Cos(_bobPhase)) * 0.018f, 0f) * _bobAmount;
        pos += new Vector3(-_sway.X, _sway.Y, 0f);
        pos += new Vector3(-0.10f * f2, 0.05f * MathF.Sin(MathF.Sqrt(s) * MathHelper.TwoPi) - 0.06f * f, -0.10f * f2);
        pos.Y -= (1f - _equip) * (1f - _equip) * 0.4f;

        bool holding = _item.Count > 0;
        float pitch = (holding ? 0.35f : 0.55f) - f * 0.6f;
        float yaw = 0.45f + f2 * 0.35f;
        Matrix armWorld = Matrix.CreateRotationX(pitch) * Matrix.CreateRotationY(yaw) * Matrix.CreateTranslation(pos);

        var oldView = effect.View;
        var oldProj = effect.Projection;
        var oldWorld = effect.World;
        bool oldTextureEnabled = effect.TextureEnabled;
        bool oldVertexColorEnabled = effect.VertexColorEnabled;

        effect.TextureEnabled = false;
        effect.VertexColorEnabled = true;

        effect.View = Matrix.Identity;
        effect.Projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(70f),
            device.Viewport.AspectRatio,
            0.01f,
            10f
        );

        device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1f, 0);
        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullNone;

        DrawList(device, effect, _arm, armWorld);

        if (holding)
        {
            Matrix itemWorld =
                Matrix.CreateRotationY(-0.4f + f2 * 0.3f)
                * Matrix.CreateRotationX(-f * 0.4f)
                * Matrix.CreateTranslation(pos + new Vector3(-0.03f, 0.03f, -0.04f));
            DrawList(device, effect, _item, itemWorld);
        }

        effect.View = oldView;
        effect.Projection = oldProj;
        effect.World = oldWorld;
        effect.TextureEnabled = oldTextureEnabled;
        effect.VertexColorEnabled = oldVertexColorEnabled;
    }

    private static void DrawList(GraphicsDevice device, BasicEffect effect, List<VertexPositionColor> verts, Matrix world)
    {
        if (verts.Count == 0)
            return;
        effect.World = world;
        var arr = verts.ToArray();
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, arr, 0, arr.Length / 3);
        }
    }

    private static void AddBoxGeometry(List<VertexPositionColor> target, Vector3 min, Vector3 max, Color color, bool shaded)
    {
        Vector3 p000 = new(min.X, min.Y, min.Z);
        Vector3 p100 = new(max.X, min.Y, min.Z);
        Vector3 p010 = new(min.X, max.Y, min.Z);
        Vector3 p110 = new(max.X, max.Y, min.Z);
        Vector3 p001 = new(min.X, min.Y, max.Z);
        Vector3 p101 = new(max.X, min.Y, max.Z);
        Vector3 p011 = new(min.X, max.Y, max.Z);
        Vector3 p111 = new(max.X, max.Y, max.Z);

        Color Shade(float s) => shaded ? new Color(color.ToVector3() * s) * (color.A / 255f) : color;

        // Top
        Quad(target, p010, p110, p111, p011, Shade(1.0f));
        // Bottom
        Quad(target, p000, p001, p101, p100, Shade(0.55f));
        // Left (-X)
        Quad(target, p000, p010, p011, p001, Shade(0.8f));
        // Right (+X)
        Quad(target, p100, p101, p111, p110, Shade(0.8f));
        // Back (-Z)
        Quad(target, p000, p100, p110, p010, Shade(0.9f));
        // Front (+Z)
        Quad(target, p001, p011, p111, p101, Shade(0.9f));
    }

    private static void Quad(List<VertexPositionColor> target, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        target.Add(new VertexPositionColor(a, color));
        target.Add(new VertexPositionColor(b, color));
        target.Add(new VertexPositionColor(c, color));
        target.Add(new VertexPositionColor(a, color));
        target.Add(new VertexPositionColor(c, color));
        target.Add(new VertexPositionColor(d, color));
    }
}
