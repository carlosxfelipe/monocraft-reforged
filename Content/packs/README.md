# Como adicionar mobs do Bedrock

Coloque packs da comunidade aqui. Cada pack deve seguir a estrutura abaixo.

## Estrutura de pasta por pack

```
Content/packs/<nome-do-pack>/
  behaviors/
    entities/
      cow.json               ← comportamento (BP)
  resources/
    entity/
      cow.entity.json        ← cliente/visual (RP)
    models/
      entity/
        cow.geo.json         ← geometria de cubo hierárquico
    textures/
      entity/
        cow/
          cow.png            ← atlas de textura
    animations/
      cow.animation.json     ← animações de ossos
```

## Onde encontrar packs prontos

- **Blockbench** (blockbench.net) — modelador gratuito que exporta exatamente esse formato
- **MCPEDL** (mcpedl.com) — packs da comunidade para Bedrock Edition
- **Minecraft Wiki** — documentação completa dos formatos JSON

## Componentes de comportamento suportados

| Componente Bedrock | Comportamento no MonoCraft |
|--------------------|---------------------------|
| `minecraft:behavior.wander` | Caminhada aleatória |
| `minecraft:behavior.random_stroll` | Caminhada aleatória |

Novos comportamentos podem ser adicionados em `Entities/AI/` implementando
`IBehaviorComponent` e registrando-os em `EntityManager.BuildBehaviors()`.
