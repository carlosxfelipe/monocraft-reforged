using Microsoft.Xna.Framework;
using MonoCraft.Entities.AI;
using MonoCraft.World;

namespace MonoCraft.Entities;

/// <summary>
/// Uma implementação 100% C# (sem JSON proprietário) inspirada no porco do mod 'mobs_animal' do Minetest.
/// </summary>
public class PigMob : Mob
{
    public PigMob(Vector3 spawnPosition, VoxelWorld world)
        : base(
            identifier: "mobs:pig",
            spawnPosition: spawnPosition,
            world: world,
            // As colisões exatas do porco no Minetest: {-0.4, -0.01, -0.4, 0.4, 0.8, 0.4}
            width: 0.8f,
            height: 0.8f,
            // HP Máximo padrão do porco no Minetest é 15
            maxHealth: 15f,
            isAquatic: false,
            behaviors: new IBehaviorComponent[]
            {
                // Reduzido para 0.15f (0.6 blocos por segundo), simulando um porco pastando calmamente.
                new WanderBehavior(priority: 4, speedMultiplier: 0.15f),
            }
        )
    {
        // Aqui você pode adicionar lógica adicional no construtor que antes ficava presa em um Behavior Pack.
    }
}
