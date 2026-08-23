using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace MonoCraft;

public class VirtualGamepad
{
    public GamePadState PadState { get; private set; }
    public Point MouseDelta { get; private set; }
    public Microsoft.Xna.Framework.Input.Touch.TouchCollection Touches { get; private set; }

    private Texture2D _pixel;
    private Viewport _viewport;

    // Analógico de movimento
    private Vector2? _leftStickOrigin;
    private Vector2 _leftStickCurrent;
    private int? _leftStickTouchId;

    // Câmera (olhar ao redor)
    private Vector2 _lastLookPosition;
    private int? _lookTouchId;

    // Botões
    private Dictionary<Buttons, Rectangle> _buttonAreas = new();

    public VirtualGamepad(Texture2D pixel)
    {
        _pixel = pixel;
    }

    public void Update(Viewport viewport)
    {
        _viewport = viewport;
        Touches = TouchPanel.GetState();
        var touches = Touches;
        int width = viewport.Width;
        int height = viewport.Height;

        MouseDelta = Point.Zero;
        float leftThumbX = 0,
            leftThumbY = 0;
        Buttons pressedButtons = 0;

        int btnSize = Math.Min(width, height) / 8;
        int padding = 20;

        _buttonAreas.Clear();

        // Canto Superior Esquerdo: ESC (Start)
        _buttonAreas[Buttons.Start] = new Rectangle(padding, padding, btnSize, btnSize);

        // Canto Superior Direito: Voar (RightStick)
        _buttonAreas[Buttons.RightStick] = new Rectangle(
            width - btnSize - padding,
            padding,
            btnSize,
            btnSize
        );

        // Canto Inferior Direito: Pular (A), Inventário (Y)
        _buttonAreas[Buttons.A] = new Rectangle(
            width - btnSize - padding,
            height - btnSize - padding,
            btnSize,
            btnSize
        );
        _buttonAreas[Buttons.Y] = new Rectangle(
            width - btnSize * 2 - padding * 2,
            height - btnSize - padding,
            btnSize,
            btnSize
        );

        // Centro Direito: Colocar (LeftTrigger), Quebrar (RightTrigger)
        _buttonAreas[Buttons.LeftTrigger] = new Rectangle(
            width - btnSize - padding,
            height - btnSize * 2 - padding * 2,
            btnSize,
            btnSize
        );
        _buttonAreas[Buttons.RightTrigger] = new Rectangle(
            width - btnSize * 2 - padding * 2,
            height - btnSize * 2 - padding * 2,
            btnSize,
            btnSize
        );

        // Setas do Inventário (DPad Esquerda/Direita)
        int cx = width / 2;
        int slotSize = Math.Max(36, height / 15);
        int slotPad = Math.Max(4, slotSize / 9);
        int totalHotbarWidth = 9 * (slotSize + slotPad) - slotPad;
        int hudPad = totalHotbarWidth / 2 + padding;

        int bottomRowY = height - btnSize - padding;

        _buttonAreas[Buttons.DPadLeft] = new Rectangle(
            cx - hudPad - btnSize,
            bottomRowY,
            btnSize,
            btnSize
        );
        _buttonAreas[Buttons.DPadRight] = new Rectangle(cx + hudPad, bottomRowY, btnSize, btnSize);

        bool leftStickActive = false;

        foreach (var touch in touches)
        {
            if (touch.State == TouchLocationState.Released)
            {
                if (_leftStickTouchId == touch.Id)
                    _leftStickTouchId = null;
                if (_lookTouchId == touch.Id)
                    _lookTouchId = null;
                continue;
            }

            bool hitButton = false;
            foreach (var kvp in _buttonAreas)
            {
                if (kvp.Value.Contains(touch.Position))
                {
                    pressedButtons |= kvp.Key;
                    hitButton = true;
                }
            }

            if (hitButton)
                continue;

            if (touch.Position.X < width / 2)
            {
                if (_leftStickTouchId == null && touch.State == TouchLocationState.Pressed)
                {
                    _leftStickTouchId = touch.Id;
                    _leftStickOrigin = touch.Position;
                }

                if (_leftStickTouchId == touch.Id)
                {
                    leftStickActive = true;
                    _leftStickCurrent = touch.Position;
                    Vector2 delta = _leftStickCurrent - _leftStickOrigin.Value;
                    float maxDist = btnSize * 1.5f;
                    if (delta.Length() > maxDist)
                    {
                        delta.Normalize();
                        delta *= maxDist;
                    }
                    leftThumbX = delta.X / maxDist;
                    leftThumbY = -delta.Y / maxDist;
                }
            }
            else if (touch.Position.X >= width / 2)
            {
                if (_lookTouchId == null && touch.State == TouchLocationState.Pressed)
                {
                    _lookTouchId = touch.Id;
                    _lastLookPosition = touch.Position;
                }

                if (_lookTouchId == touch.Id)
                {
                    if (touch.State == TouchLocationState.Moved)
                    {
                        Vector2 delta = touch.Position - _lastLookPosition;
                        MouseDelta = new Point((int)delta.X, (int)delta.Y);
                        _lastLookPosition = touch.Position;
                    }
                }
            }
        }

        if (!leftStickActive)
            _leftStickOrigin = null;

        PadState = new GamePadState(
            new GamePadThumbSticks(new Vector2(leftThumbX, leftThumbY), Vector2.Zero),
            new GamePadTriggers(
                (pressedButtons & Buttons.LeftTrigger) != 0 ? 1f : 0f,
                (pressedButtons & Buttons.RightTrigger) != 0 ? 1f : 0f
            ),
            new GamePadButtons(
                pressedButtons
                    | ((pressedButtons & Buttons.RightStick) != 0 ? Buttons.RightStick : 0)
            ),
            new GamePadDPad(
                ButtonState.Released,
                ButtonState.Released,
                (pressedButtons & Buttons.DPadLeft) != 0
                    ? ButtonState.Pressed
                    : ButtonState.Released,
                (pressedButtons & Buttons.DPadRight) != 0
                    ? ButtonState.Pressed
                    : ButtonState.Released
            )
        );
    }

    public void Draw(SpriteBatch sb, TextRenderer textRenderer)
    {
        foreach (var kvp in _buttonAreas)
        {
            Rectangle r = kvp.Value;
            sb.Draw(_pixel, r, Color.White * 0.2f);
            sb.Draw(
                _pixel,
                new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4),
                Color.Black * 0.3f
            );

            // Desenha um texto para indicar o botão
            string label = kvp.Key.ToString();
            if (kvp.Key == Buttons.LeftTrigger)
                label = "PLACE";
            if (kvp.Key == Buttons.RightTrigger)
                label = "BREAK";
            if (kvp.Key == Buttons.Y)
                label = "INV";
            if (kvp.Key == Buttons.RightStick)
                label = "FLY";
            if (kvp.Key == Buttons.A)
                label = "JUMP";
            if (kvp.Key == Buttons.DPadLeft)
                label = "<";
            if (kvp.Key == Buttons.DPadRight)
                label = ">";
            if (kvp.Key == Buttons.Start)
                label = "ESC";

            int scale = Math.Max(1, r.Width / (label.Length * 4 + 4));
            int textWidth = label.Length * 4 * scale;
            int textHeight = 5 * scale;
            int tx = r.X + (r.Width - textWidth) / 2;
            int ty = r.Y + (r.Height - textHeight) / 2;
            textRenderer.DrawString(sb, label, tx, ty, scale, Color.LightGray, drawShadow: false);
        }

        if (_leftStickOrigin.HasValue)
        {
            var origin = _leftStickOrigin.Value;
            var current = _leftStickCurrent;
            int r1 = _viewport.Width / 12;
            int r2 = r1 / 2;

            sb.Draw(
                _pixel,
                new Rectangle((int)origin.X - r1, (int)origin.Y - r1, r1 * 2, r1 * 2),
                Color.Gray * 0.2f
            );
            sb.Draw(
                _pixel,
                new Rectangle((int)current.X - r2, (int)current.Y - r2, r2 * 2, r2 * 2),
                Color.White * 0.4f
            );
        }
    }
}
