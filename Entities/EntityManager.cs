using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoCraft.Entities.AI;
using MonoCraft.World;

namespace MonoCraft.Entities;

public class EntityManager
{
    // TODO: adicione aqui identificadores de mobs com assets livres/próprios.
    // Exemplo: { "custom:cow", 0.9f, 1.4f, 10f }
    private static readonly string[] PassiveSpawns = { };

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
        var mob = new Mob(
            identifier, position, _world,
            behaviors: new[] { new WanderBehavior(priority: 4, speedMultiplier: 1f) }
        );
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
                    string soundName = mob.Identifier.Replace("custom:", "") + "_say";
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

    // Mobs não são desenhados até que um IModelRenderer livre seja implementado.
    // TODO: implementar renderização com assets livres (ex: glTF/Luanti).
    public void Draw(BasicEffect effect) { }

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
                _world.GetBlock(bx, by, bz) == BlockType.Grass &&
                _world.GetBlock(bx, by + 1, bz) == BlockType.Air
            )
            {
                Spawn(id, new Vector3(bx + 0.5f, by + 1f, bz + 0.5f));
                return;
            }
        }
    }
}
