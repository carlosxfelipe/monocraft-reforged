using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoCraft.Entities.Rendering;

public class FallbackCubeRenderer
{
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
            0,1,2, 0,2,3,
            4,6,5, 4,7,6,
            0,5,1, 0,4,5,
            1,6,2, 1,5,6,
            2,7,3, 2,6,7,
            3,4,0, 3,7,4
        };
        _buffersInitialized = true;
    }

    public void Draw(BasicEffect effect, IReadOnlyList<Mob> mobs)
    {
        if (mobs.Count == 0) return;
        if (!_buffersInitialized) InitPlaceholder();

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

        foreach (var mob in mobs)
        {
            // O cubo vai de 0 a 1 no Y e -0.5 a 0.5 em X e Z. Multiplicamos pelo tamanho do mob.
            effect.World = Matrix.CreateScale(mob.Width, mob.Height, mob.Width) *
                           Matrix.CreateRotationY(mob.Yaw) *
                           Matrix.CreateTranslation(mob.Position);

            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertices, 0, 8,
                    _indices, 0, 12
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
}
