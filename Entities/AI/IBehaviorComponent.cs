using MonoCraft.Entities;

namespace MonoCraft.Entities.AI;

public interface IBehaviorComponent
{
    // Menor valor = maior prioridade (igual ao Bedrock)
    int Priority { get; }

    void Update(Mob mob, float dt);
}
