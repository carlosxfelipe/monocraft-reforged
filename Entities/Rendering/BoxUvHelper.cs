using Microsoft.Xna.Framework;

namespace MonoCraft.Entities.Rendering;

// Calcula as UVs normalizadas [0..1] de cada face de um cubo Bedrock
// usando o layout "Box UV" padrão do Minecraft.
//
// Layout no atlas de textura para cubo de tamanho W x H x D, offset (u,v):
//
//      +---+-------+---+-------+
//      |top|        |bot|       |
//  row0|D,D| W,D   |W,D|       |  ← y=v
//      +---+-------+---+-------+
//      |lft| front |rgt| back  |
//  row1|D,H| W,H   |D,H| W,H   |  ← y=v+D
//      +---+-------+---+-------+
//        ↑   ↑       ↑   ↑
//        u  u+D    u+D+W u+D+W+D
public static class BoxUvHelper
{
    // Retorna retângulos UV normalizados (x0,y0,x1,y1) para cada uma das 6 faces.
    // Convenção de face igual à do Bedrock: Right=+X, Left=-X, Top=+Y, Bottom=-Y, Front=+Z, Back=-Z
    public static void Compute(
        float[] boxUv, // [u, v] em pixels no atlas
        float sw,
        float sh,
        float sd,
        float texW,
        float texH,
        out Vector4 right,
        out Vector4 left,
        out Vector4 top,
        out Vector4 bottom,
        out Vector4 front,
        out Vector4 back
    )
    {
        float u = boxUv[0];
        float v = boxUv[1];
        float iw = 1f / texW;
        float ih = 1f / texH;

        right = R(u, v + sd, sd, sh, iw, ih);
        front = R(u + sd, v + sd, sw, sh, iw, ih);
        left = R(u + sd + sw, v + sd, sd, sh, iw, ih);
        back = R(u + sd + sw + sd, v + sd, sw, sh, iw, ih);
        top = R(u + sd, v, sw, sd, iw, ih);
        bottom = R(u + sd + sw, v, sw, sd, iw, ih);
    }

    private static Vector4 R(float u, float v, float w, float h, float iw, float ih) =>
        new(u * iw, v * ih, (u + w) * iw, (v + h) * ih);

    // Converte um Vector4 (x0,y0,x1,y1) para os 4 UVs de um quad (bl, br, tr, tl)
    public static Vector2[] ToQuad(Vector4 rect) =>
        new[]
        {
            new Vector2(rect.X, rect.W), // bottom-left
            new Vector2(rect.Z, rect.W), // bottom-right
            new Vector2(rect.Z, rect.Y), // top-right
            new Vector2(rect.X, rect.Y), // top-left
        };
}
