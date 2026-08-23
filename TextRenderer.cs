using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoCraft;

public class TextRenderer
{
    private Texture2D _pixel;

    // Dígitos e Letras 3x5 desenhados com pixels (sem precisar de SpriteFont)
    private static readonly Dictionary<char, byte[]> CharRows = new()
    {
        { '0', new byte[] { 0b111, 0b101, 0b101, 0b101, 0b111 } },
        { '1', new byte[] { 0b010, 0b110, 0b010, 0b010, 0b111 } },
        { '2', new byte[] { 0b111, 0b001, 0b111, 0b100, 0b111 } },
        { '3', new byte[] { 0b111, 0b001, 0b111, 0b001, 0b111 } },
        { '4', new byte[] { 0b101, 0b101, 0b111, 0b001, 0b001 } },
        { '5', new byte[] { 0b111, 0b100, 0b111, 0b001, 0b111 } },
        { '6', new byte[] { 0b111, 0b100, 0b111, 0b101, 0b111 } },
        { '7', new byte[] { 0b111, 0b001, 0b010, 0b010, 0b010 } },
        { '8', new byte[] { 0b111, 0b101, 0b111, 0b101, 0b111 } },
        { '9', new byte[] { 0b111, 0b101, 0b111, 0b001, 0b111 } },
        { 'A', new byte[] { 0b111, 0b101, 0b111, 0b101, 0b101 } },
        { 'B', new byte[] { 0b110, 0b101, 0b110, 0b101, 0b110 } },
        { 'C', new byte[] { 0b111, 0b100, 0b100, 0b100, 0b111 } },
        { 'D', new byte[] { 0b110, 0b101, 0b101, 0b101, 0b110 } },
        { 'E', new byte[] { 0b111, 0b100, 0b110, 0b100, 0b111 } },
        { 'F', new byte[] { 0b111, 0b100, 0b110, 0b100, 0b100 } },
        { 'G', new byte[] { 0b111, 0b100, 0b101, 0b101, 0b111 } },
        { 'H', new byte[] { 0b101, 0b101, 0b111, 0b101, 0b101 } },
        { 'I', new byte[] { 0b111, 0b010, 0b010, 0b010, 0b111 } },
        { 'J', new byte[] { 0b001, 0b001, 0b001, 0b101, 0b111 } },
        { 'K', new byte[] { 0b101, 0b110, 0b100, 0b110, 0b101 } },
        { 'L', new byte[] { 0b100, 0b100, 0b100, 0b100, 0b111 } },
        { 'M', new byte[] { 0b101, 0b111, 0b101, 0b101, 0b101 } },
        { 'N', new byte[] { 0b111, 0b101, 0b101, 0b101, 0b101 } },
        { 'O', new byte[] { 0b111, 0b101, 0b101, 0b101, 0b111 } },
        { 'P', new byte[] { 0b111, 0b101, 0b111, 0b100, 0b100 } },
        { 'Q', new byte[] { 0b111, 0b101, 0b101, 0b111, 0b011 } },
        { 'R', new byte[] { 0b111, 0b101, 0b110, 0b101, 0b101 } },
        { 'S', new byte[] { 0b111, 0b100, 0b111, 0b001, 0b111 } },
        { 'T', new byte[] { 0b111, 0b010, 0b010, 0b010, 0b010 } },
        { 'U', new byte[] { 0b101, 0b101, 0b101, 0b101, 0b111 } },
        { 'V', new byte[] { 0b101, 0b101, 0b101, 0b101, 0b010 } },
        { 'W', new byte[] { 0b101, 0b101, 0b101, 0b111, 0b101 } },
        { 'X', new byte[] { 0b101, 0b101, 0b010, 0b101, 0b101 } },
        { 'Y', new byte[] { 0b101, 0b101, 0b010, 0b010, 0b010 } },
        { 'Z', new byte[] { 0b111, 0b001, 0b010, 0b100, 0b111 } },
        { ':', new byte[] { 0b000, 0b010, 0b000, 0b010, 0b000 } },
        { '?', new byte[] { 0b111, 0b001, 0b011, 0b000, 0b010 } },
        { '/', new byte[] { 0b001, 0b001, 0b010, 0b100, 0b100 } },
        { '>', new byte[] { 0b100, 0b010, 0b001, 0b010, 0b100 } },
        { '<', new byte[] { 0b001, 0b010, 0b100, 0b010, 0b001 } },
        { ' ', new byte[] { 0b000, 0b000, 0b000, 0b000, 0b000 } },
    };

    public TextRenderer(Texture2D pixel)
    {
        _pixel = pixel;
    }

    public void DrawString(
        SpriteBatch spriteBatch,
        string text,
        int startX,
        int y,
        int scale,
        Color color,
        bool drawShadow = true
    )
    {
        int x = startX;
        foreach (char ch in text)
        {
            if (CharRows.TryGetValue(char.ToUpper(ch), out var rows))
            {
                for (int r = 0; r < 5; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        if ((rows[r] >> (2 - c) & 1) != 0)
                        {
                            if (drawShadow)
                            {
                                spriteBatch.Draw(
                                    _pixel,
                                    new Rectangle(
                                        x + c * scale + 1,
                                        y + r * scale + 1,
                                        scale,
                                        scale
                                    ),
                                    Color.Black * 0.7f
                                );
                            }
                            spriteBatch.Draw(
                                _pixel,
                                new Rectangle(x + c * scale, y + r * scale, scale, scale),
                                color
                            );
                        }
                    }
                }
                x += 4 * scale; // 3 columns + 1 space
            }
        }
    }

    public void DrawNumber(SpriteBatch spriteBatch, int value, int rightX, int y, int scale = 2)
    {
        int digitWidth = 3 * scale + scale; // 3 colunas + espaço

        string text = value.ToString();
        int x = rightX - text.Length * digitWidth;
        DrawString(spriteBatch, text, x, y, scale, Color.White);
    }
}
