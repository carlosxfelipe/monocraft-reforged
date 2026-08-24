using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoCraft.Entities.AI;
using MonoCraft.World;

namespace MonoCraft.Entities;

public class EntityManager
{
    private static readonly string[] PassiveSpawns = { "mobs:pig" };

    private const int MaxPassiveMobs = 25;
    private const float SpawnInterval = 8f;
    private const int SpawnMinDist = 24;
    private const int SpawnMaxDist = 48;

    private readonly VoxelWorld _world;
    private readonly Dictionary<Mob, float> _ambientTimers = new();
    private readonly List<Mob> _mobs = new();
    private readonly Random _rng = new();
    private float _spawnTimer;

    public IReadOnlyList<Mob> Mobs => _mobs;

    public EntityManager(VoxelWorld world)
    {
        _world = world;
    }

    // Não precisa mais carregar conteúdo gráfico (EntityRenderer removido)
    public void LoadContent(GraphicsDevice gd) { }

    /// <summary>Cria um mob pelo identificador, ex: "custom:cow"</summary>
    public Mob Spawn(string identifier, Vector3 position)
    {
        Mob mob;
        if (identifier == "mobs:pig")
        {
            mob = new PigMob(position, _world);
        }
        else
        {
            mob = new Mob(
                identifier,
                position,
                _world,
                behaviors: new[] { new WanderBehavior(priority: 4, speedMultiplier: 1f) }
            );
        }

        _mobs.Add(mob);
        return mob;
    }

    public void Update(float dt, Vector3 playerPos)
    {
        foreach (var mob in _mobs)
        {
            mob.Update(dt);

            // Som ambiente (funcionará quando sons livres forem adicionados)
            if (!_ambientTimers.TryGetValue(mob, out float timer))
                timer = 5f + (float)_rng.NextDouble() * 10f;

            timer -= dt;
            if (timer <= 0)
            {
                timer = 5f + (float)_rng.NextDouble() * 15f;

                float dist = Vector3.Distance(mob.Position, playerPos);
                if (dist < 32f)
                {
                    float volume = 1f - (dist / 32f);
                    float pitch = (float)(_rng.NextDouble() * 0.2 - 0.1);

                    string soundName = mob.Identifier.Replace(":", "_");
                    if (mob.Identifier.StartsWith("custom:"))
                        soundName = mob.Identifier.Replace("custom:", "") + "_say";

                    Rendering.SoundManager.Play(soundName, volume, pitch);
                }
            }
            _ambientTimers[mob] = timer;
        }

        _mobs.RemoveAll(m =>
        {
            if (!m.IsAlive)
            {
                _ambientTimers.Remove(m);
                return true;
            }
            return false;
        });

        _spawnTimer -= dt;
        if (_spawnTimer <= 0)
        {
            _spawnTimer = SpawnInterval;
            TryPassiveSpawn(playerPos);
        }
    }

    private VertexPositionColor[] _vertices;
    private short[] _indices;
    private bool _buffersInitialized;

    private void InitPlaceholder()
    {
        _vertices = new VertexPositionColor[8];
        Color c = Color.HotPink; // Placeholder box color
        _vertices[0] = new VertexPositionColor(new Vector3(-0.5f, 0, -0.5f), c);
        _vertices[1] = new VertexPositionColor(new Vector3(0.5f, 0, -0.5f), c);
        _vertices[2] = new VertexPositionColor(new Vector3(0.5f, 0, 0.5f), c);
        _vertices[3] = new VertexPositionColor(new Vector3(-0.5f, 0, 0.5f), c);
        _vertices[4] = new VertexPositionColor(new Vector3(-0.5f, 1, -0.5f), c);
        _vertices[5] = new VertexPositionColor(new Vector3(0.5f, 1, -0.5f), c);
        _vertices[6] = new VertexPositionColor(new Vector3(0.5f, 1, 0.5f), c);
        _vertices[7] = new VertexPositionColor(new Vector3(-0.5f, 1, 0.5f), c);

        _indices = new short[]
        {
            0,
            1,
            2,
            0,
            2,
            3,
            4,
            6,
            5,
            4,
            7,
            6,
            0,
            5,
            1,
            0,
            4,
            5,
            1,
            6,
            2,
            1,
            5,
            6,
            2,
            7,
            3,
            2,
            6,
            7,
            3,
            4,
            0,
            3,
            7,
            4,
        };
        _buffersInitialized = true;
    }

    // Mobs não são desenhados até que um IModelRenderer livre seja implementado.
    // Usando um cubo rosa temporário para representar a hitbox do mob.
    public void Draw(BasicEffect effect)
    {
        if (_mobs.Count == 0)
            return;
        if (!_buffersInitialized)
            InitPlaceholder();

        var gd = effect.GraphicsDevice;
        bool wasTextureEnabled = effect.TextureEnabled;
        bool wasVertexColorEnabled = effect.VertexColorEnabled;
        var oldRasterizer = gd.RasterizerState;
        var oldDepth = gd.DepthStencilState;

        effect.TextureEnabled = false;
        effect.VertexColorEnabled = true;

        // Desativa o Culling para garantir que vejamos as faces independente da ordem dos vértices
        // e ativa o Depth Test para o cubo não parecer estar do avesso.
        gd.RasterizerState = RasterizerState.CullNone;
        gd.DepthStencilState = DepthStencilState.Default;

        foreach (var mob in _mobs)
        {
            // O cubo vai de 0 a 1 no Y e -0.5 a 0.5 em X e Z. Multiplicamos pelo tamanho do mob.
            effect.World =
                Matrix.CreateScale(mob.Width, mob.Height, mob.Width)
                * Matrix.CreateRotationY(mob.Yaw)
                * Matrix.CreateTranslation(mob.Position);

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertices,
                    0,
                    8,
                    _indices,
                    0,
                    12
                );
            }
        }

        // Restaura as configurações do shader
        effect.World = Matrix.Identity;
        effect.TextureEnabled = wasTextureEnabled;
        effect.VertexColorEnabled = wasVertexColorEnabled;
        gd.RasterizerState = oldRasterizer;
        gd.DepthStencilState = oldDepth;
    }

    private void TryPassiveSpawn(Vector3 origin)
    {
        if (_mobs.Count >= MaxPassiveMobs)
            return;

        if (PassiveSpawns.Length == 0)
            return;

        string id = PassiveSpawns[_rng.Next(PassiveSpawns.Length)];

        for (int attempt = 0; attempt < 8; attempt++)
        {
            double angle = _rng.NextDouble() * Math.PI * 2;
            int dist = SpawnMinDist + _rng.Next(SpawnMaxDist - SpawnMinDist);
            int bx = (int)origin.X + (int)(Math.Cos(angle) * dist);
            int bz = (int)origin.Z + (int)(Math.Sin(angle) * dist);
            int by = _world.Generator.GetHeight(bx, bz);

            if (
                _world.GetBlock(bx, by, bz) == BlockType.Grass
                && _world.GetBlock(bx, by + 1, bz) == BlockType.Air
            )
            {
                Spawn(id, new Vector3(bx + 0.5f, by + 1f, bz + 0.5f));
                return;
            }
        }
    }
}
