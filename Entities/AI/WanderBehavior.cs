using System;
using Microsoft.Xna.Framework;
using MonoCraft.Entities;

namespace MonoCraft.Entities.AI;

// Equivale ao "minecraft:behavior.wander" / "minecraft:behavior.random_stroll" do Bedrock.
// O mob escolhe um ponto aleatório nas redondezas e caminha até ele.
public class WanderBehavior : IBehaviorComponent
{
    public int Priority { get; }

    private Vector3 _targetOffset;
    private float _wanderTimer;
    private readonly Random _rng;
    private const float WanderInterval = 5f; // tempo máximo caminhando antes de repick
    private const float IdleMin = 2f; // pausa mínima ao ficar parado
    private const float IdleMax = 4f; // pausa máxima ao ficar parado
    private const float IdleChance = 0.30f;

    private Vector3 _lastPos;
    private float _stuckTimer;
    private const float StuckCheckInterval = 1.5f;
    private const float StuckMinMove = 0.3f;

    private readonly float _speedMultiplier;

    public WanderBehavior(int priority = 4, float speedMultiplier = 1f)
    {
        Priority = priority;
        _speedMultiplier = speedMultiplier;
        _rng = new Random();
        PickNewTarget();
    }

    public void Update(Mob mob, float dt)
    {
        _wanderTimer -= dt;
        if (_wanderTimer <= 0)
            PickNewTarget();

        var dir = _targetOffset;
        if (dir.LengthSquared() < 0.25f)
        {
            mob.Velocity = new Vector3(0, mob.Velocity.Y, 0);
            // Chegou ao destino — não espera o timer; repick imediato
            if (_wanderTimer > 0.5f)
                _wanderTimer = 0;
            return;
        }

        dir.Normalize();
        // Velocidade padrão de caminhada (era vinda do JSON Bedrock; fixa até termos assets livres)
        float speed = 1.0f * 4f * _speedMultiplier;
        mob.Velocity = new Vector3(dir.X * speed, mob.Velocity.Y, dir.Z * speed);

        // Reduz o offset conforme o mob avança
        _targetOffset -= dir * speed * dt;

        // Detecção de stuck: se não se moveu o suficiente em StuckCheckInterval segundos, escolhe novo destino
        _stuckTimer += dt;
        if (_stuckTimer >= StuckCheckInterval)
        {
            float moved = Vector3.Distance(
                new Vector3(mob.Position.X, 0, mob.Position.Z),
                new Vector3(_lastPos.X, 0, _lastPos.Z)
            );

            if (moved < StuckMinMove)
                PickNewTarget();

            _lastPos = mob.Position;
            _stuckTimer = 0;
        }
    }

    private void PickNewTarget()
    {
        if (_rng.NextDouble() < IdleChance)
        {
            _targetOffset = Vector3.Zero;
            _wanderTimer = IdleMin + (float)_rng.NextDouble() * (IdleMax - IdleMin);
            return;
        }

        _wanderTimer = WanderInterval * (0.6f + (float)_rng.NextDouble() * 0.8f);

        float angle = (float)(_rng.NextDouble() * MathHelper.TwoPi);
        float dist = 3f + (float)(_rng.NextDouble() * 7f);
        _targetOffset = new Vector3(MathF.Cos(angle) * dist, 0, MathF.Sin(angle) * dist);
    }
}
