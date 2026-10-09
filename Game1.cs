using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using MonoCraft.Entities;
using MonoCraft.Entities.Rendering;
using MonoCraft.World;
#if ANDROID && !IOS
using Android.App;
#endif

namespace MonoCraft;

public class Game1 : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private VoxelWorld _world;
    private Player _player;
    private EntityManager _entities;
    private BasicEffect _effect;
    private Texture2D _pixel;
    private VirtualGamepad _virtualGamepad;

    private MouseState _prevMouse;
    private KeyboardState _prevKeyboard;
    private GamePadState _prevPad;
    private bool _mouseCaptured = false;
    private bool _takeScreenshot = false;
    private bool _isMainMenuOpen = true;
    private bool _isExitMenuOpen = false;
    private Hand _hand;
    private bool _hasSelectedSkin = false;
    private bool _skinMenuOpen = false;
    private TextRenderer _textRenderer;

    private int _selectedHotbarIndex = 0;

    private float _timeOfDay = 0.4f; // 0.0=Meia-noite, 0.5=Meio-dia

    // Inventário survival: 36 slots físicos (0-8 hotbar, 9-35 mochila)
    private readonly InventorySlot[] _inventory = new InventorySlot[36];

    private void AddToInventory(BlockType type, int amount = 1)
    {
        // 1. Tentar agrupar em stacks que não estejam cheios
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i].Type == type && _inventory[i].Count < 64)
            {
                int space = 64 - _inventory[i].Count;
                int toAdd = Math.Min(space, amount);
                _inventory[i].Count += toAdd;
                amount -= toAdd;
                if (amount <= 0)
                    return;
            }
        }

        // 2. Se sobrar, colocar no primeiro slot vazio
        for (int i = 0; i < _inventory.Length; i++)
        {
            if (_inventory[i].Type == BlockType.Air || _inventory[i].Count == 0)
            {
                int toAdd = Math.Min(64, amount);
                _inventory[i] = new InventorySlot(type, toAdd);
                amount -= toAdd;
                if (amount <= 0)
                    return;
            }
        }
    }

    private bool TryConsumeSelectedHotbarSlot(out BlockType placedType)
    {
        placedType = _inventory[_selectedHotbarIndex].Type;
        if (placedType == BlockType.Air || _inventory[_selectedHotbarIndex].Count <= 0)
            return false;

        _inventory[_selectedHotbarIndex].Count--;
        if (_inventory[_selectedHotbarIndex].Count <= 0)
            _inventory[_selectedHotbarIndex].Type = BlockType.Air;

        return true;
    }

    private bool _isInventoryOpen = false;
    private InventorySlot? _draggedSlot = null;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        _graphics.SynchronizeWithVerticalRetrace = true;
        IsFixedTimeStep = false;

#if ANDROID
        _graphics.IsFullScreen = true;
        _graphics.PreferredBackBufferWidth = GraphicsAdapter
            .DefaultAdapter
            .CurrentDisplayMode
            .Width;
        _graphics.PreferredBackBufferHeight = GraphicsAdapter
            .DefaultAdapter
            .CurrentDisplayMode
            .Height;
        _graphics.SupportedOrientations =
            DisplayOrientation.LandscapeLeft | DisplayOrientation.LandscapeRight;
#else
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += OnWindowClientSizeChanged;
#endif
    }

    protected override void Initialize()
    {
        _world = new VoxelWorld(seed: GameSettings.Seed);

        // Spawn no centro do mundo, em cima do terreno
        int spawnHeight = _world.Generator.GetHeight(8, 8);
        _player = new Player(_world, new Vector3(8.5f, spawnHeight + 2, 8.5f));

        _world.EnsureChunksAround(_player.Position, maxNewPerFrame: 200);

        _entities = new EntityManager(_world);

#if !ANDROID
        _prevMouse = Mouse.GetState();
#endif
        IsMouseVisible = true;

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _hand = new Hand(new Color(222, 170, 128));
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _entities.LoadContent(GraphicsDevice);

        _effect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            LightingEnabled = false,
            FogEnabled = true,
            FogColor = new Vector3(0.62f, 0.76f, 0.95f),
            FogStart = (VoxelWorld.RenderDistance - 2) * Chunk.Size,
            FogEnd = VoxelWorld.RenderDistance * Chunk.Size,
        };

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _virtualGamepad = new VirtualGamepad(_pixel);

#if ANDROID && !IOS
        SoundManager.Initialize(
            Path.Combine(Application.Context.FilesDir.AbsolutePath, "Content", "sounds")
        );
#elif IOS
        // No iOS os BundleResources ficam no diretório do bundle, acessível diretamente
        SoundManager.Initialize(
            Path.Combine(Foundation.NSBundle.MainBundle.BundlePath, "Content", "sounds")
        );
#else
        SoundManager.Initialize("Content/sounds");
#endif
        _textRenderer = new TextRenderer(_pixel);
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        var pad = GamePad.GetState(PlayerIndex.One);

#if ANDROID
        _virtualGamepad.Update(GraphicsDevice.Viewport);
        var vPad = _virtualGamepad.PadState;
        if (
            vPad.ThumbSticks.Left != Vector2.Zero
            || vPad.Triggers.Right > 0
            || vPad.Triggers.Left > 0
            || vPad.Buttons.A == ButtonState.Pressed
            || vPad.Buttons.X == ButtonState.Pressed
            || vPad.Buttons.Y == ButtonState.Pressed
            || vPad.Buttons.RightStick == ButtonState.Pressed
            || vPad.DPad.Left == ButtonState.Pressed
            || vPad.DPad.Right == ButtonState.Pressed
            || vPad.Buttons.Start == ButtonState.Pressed
        )
        {
            pad = vPad;
        }
#endif

        bool escPressed =
            (keyboard.IsKeyDown(Keys.Escape) && _prevKeyboard.IsKeyUp(Keys.Escape))
            || (
                pad.Buttons.Start == ButtonState.Pressed
                && _prevPad.Buttons.Start == ButtonState.Released
            );

        if (escPressed)
        {
            if (_isMainMenuOpen)
            {
                // não faz nada, escape não fecha o menu principal (poderia fechar o jogo, mas vamos ignorar)
            }
            else if (_isInventoryOpen)
            {
                _isInventoryOpen = false;
                _mouseCaptured = true;
                IsMouseVisible = false;
                CenterMouse();
                _draggedSlot = null;
            }
            else if (_isExitMenuOpen)
            {
                _isExitMenuOpen = false;
                _mouseCaptured = true;
                IsMouseVisible = false;
                CenterMouse();
            }
            else
            {
                _isExitMenuOpen = true;
                _mouseCaptured = false;
                IsMouseVisible = true;
            }
        }

        if (_isMainMenuOpen)
        {
            bool touchStart = false;
            int cx = 0, cy = 0;

            if (
                mouse.LeftButton == ButtonState.Pressed
                && _prevMouse.LeftButton == ButtonState.Released
            )
            {
                cx = mouse.X;
                cy = mouse.Y;
                if (!_skinMenuOpen && GetStartButtonRect().Contains(mouse.Position))
                    touchStart = true;
            }

#if ANDROID
            foreach (var touch in _virtualGamepad.Touches)
#else
            foreach (var touch in TouchPanel.GetState())
#endif
            {
                if (touch.State == TouchLocationState.Pressed)
                {
                    cx = (int)touch.Position.X;
                    cy = (int)touch.Position.Y;
                    if (!_skinMenuOpen && GetStartButtonRect().Contains(touch.Position))
                        touchStart = true;
                }
            }

            if (_skinMenuOpen && (mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released || TouchPanel.GetState().Count > 0))
            {
                var colors = GetSkinColors();
                var rects = GetSkinButtonRects();
                for (int i = 0; i < rects.Length; i++)
                {
                    if (rects[i].Contains(cx, cy))
                    {
                        _hand = new Hand(colors[i]);
                        _hasSelectedSkin = true;
                        _skinMenuOpen = false;
                        _isMainMenuOpen = false;
                        _mouseCaptured = true;
                        IsMouseVisible = false;
                        CenterMouse();
                        break;
                    }
                }
            }
            else if (
                touchStart
                || (keyboard.IsKeyDown(Keys.Enter) && _prevKeyboard.IsKeyUp(Keys.Enter))
                || (
                    pad.Buttons.A == ButtonState.Pressed
                    && _prevPad.Buttons.A == ButtonState.Released
                )
            )
            {
                if (!_hasSelectedSkin)
                {
                    _skinMenuOpen = true;
                }
                else
                {
                    _isMainMenuOpen = false;
                    _mouseCaptured = true;
                    IsMouseVisible = false;
                    CenterMouse();
                }
            }
        }
        else if (_isExitMenuOpen)
        {
            bool touchYes = false;
            bool touchNo = false;

            if (
                mouse.LeftButton == ButtonState.Pressed
                && _prevMouse.LeftButton == ButtonState.Released
            )
            {
                if (GetYesButtonRect().Contains(mouse.Position))
                    touchYes = true;
                if (GetNoButtonRect().Contains(mouse.Position))
                    touchNo = true;
            }

#if ANDROID
            foreach (var touch in _virtualGamepad.Touches)
#else
            foreach (var touch in TouchPanel.GetState())
#endif
            {
                if (touch.State == TouchLocationState.Pressed)
                {
                    if (GetYesButtonRect().Contains(touch.Position))
                        touchYes = true;
                    if (GetNoButtonRect().Contains(touch.Position))
                        touchNo = true;
                }
            }

            if (
                touchYes
                || (keyboard.IsKeyDown(Keys.Y) && _prevKeyboard.IsKeyUp(Keys.Y))
                || (keyboard.IsKeyDown(Keys.Enter) && _prevKeyboard.IsKeyUp(Keys.Enter))
                || (
                    pad.Buttons.A == ButtonState.Pressed
                    && _prevPad.Buttons.A == ButtonState.Released
                )
            )
            {
#if IOS
                _isMainMenuOpen = true;
                _isExitMenuOpen = false;
                _mouseCaptured = false;
                IsMouseVisible = true;
#else
                Exit();
#endif
            }

            if (
                touchNo
                || (keyboard.IsKeyDown(Keys.N) && _prevKeyboard.IsKeyUp(Keys.N))
                || (
                    pad.Buttons.B == ButtonState.Pressed
                    && _prevPad.Buttons.B == ButtonState.Released
                )
            )
            {
                _isExitMenuOpen = false;
                _mouseCaptured = true;
                IsMouseVisible = false;
                CenterMouse();
            }
        }

        // Alternar captura do mouse com Tab
        if (
            keyboard.IsKeyDown(Keys.Tab)
            && _prevKeyboard.IsKeyUp(Keys.Tab)
            && !_isInventoryOpen
            && !_isExitMenuOpen
        )
        {
            _mouseCaptured = !_mouseCaptured;
            IsMouseVisible = !_mouseCaptured;
            if (_mouseCaptured)
                CenterMouse();
        }

        if (
            (keyboard.IsKeyDown(Keys.E) && _prevKeyboard.IsKeyUp(Keys.E))
            || (pad.Buttons.Y == ButtonState.Pressed && _prevPad.Buttons.Y == ButtonState.Released)
        )
        {
            if (!_isExitMenuOpen)
            {
                _isInventoryOpen = !_isInventoryOpen;
                if (_isInventoryOpen)
                {
                    _mouseCaptured = false;
                    IsMouseVisible = true;
                }
                else
                {
                    _mouseCaptured = true;
                    IsMouseVisible = false;
                    CenterMouse();
                    _draggedSlot = null;
                }
            }
        }

        if (keyboard.IsKeyDown(Keys.F2) && _prevKeyboard.IsKeyUp(Keys.F2))
        {
            _takeScreenshot = true;
        }
        int mouseDeltaX = 0;
        int mouseDeltaY = 0;
#if !ANDROID
        if (_mouseCaptured && IsActive)
        {
            var center = GetWindowCenter();
            mouseDeltaX = mouse.X - center.X;
            mouseDeltaY = mouse.Y - center.Y;
            CenterMouse();
        }
#else
        mouseDeltaX = _virtualGamepad.MouseDelta.X;
        mouseDeltaY = _virtualGamepad.MouseDelta.Y;
#endif
        if (_isInventoryOpen)
        {
            _player.Update(dt, default(KeyboardState), 0, 0, pad);
            UpdateInventoryUI(mouse, _prevMouse);
        }
        else if (!_isExitMenuOpen && !_isMainMenuOpen)
        {
            _player.Update(dt, keyboard, mouseDeltaX, mouseDeltaY, pad);

            if (GameSettings.EnableDayNightCycle)
            {
                float dayDurationSecs = GameSettings.DayNightDurationMinutes * 60f;
                _timeOfDay += dt / dayDurationSecs;
                if (_timeOfDay >= 1f)
                    _timeOfDay -= 1f;
            }

            // Seleção de bloco na hotbar (1-9)
            for (int i = 0; i < 9; i++)
            {
                if (keyboard.IsKeyDown(Keys.D1 + i))
                    _selectedHotbarIndex = i;
            }

            // Quebrar bloco (clique esquerdo ou X — igual ao Switch)
            bool doBreak =
                (
                    _mouseCaptured
                    && mouse.LeftButton == ButtonState.Pressed
                    && _prevMouse.LeftButton == ButtonState.Released
                )
                || (pad.Triggers.Right > 0.5f && _prevPad.Triggers.Right <= 0.5f)
                || (
                    pad.IsButtonDown(Buttons.RightTrigger)
                    && _prevPad.IsButtonUp(Buttons.RightTrigger)
                );
            if (doBreak)
            {
                if (_world.Raycast(_player.EyePosition, _player.Forward, 6f, out var hit, out _))
                {
                    var broken = _world.GetBlock(hit.X, hit.Y, hit.Z);
                    if (broken != BlockType.Bedrock)
                    {
                        _hand.Swing();
                        _world.SetBlock(hit.X, hit.Y, hit.Z, BlockType.Air);
                        AddToInventory(BlockInfo.GetDrop(broken));
                    }
                }
            }

            // Colocar bloco (clique direito ou RT)
            bool doPlace =
                (
                    _mouseCaptured
                    && mouse.RightButton == ButtonState.Pressed
                    && _prevMouse.RightButton == ButtonState.Released
                )
                || (pad.Triggers.Left > 0.5f && _prevPad.Triggers.Left <= 0.5f)
                || (
                    pad.IsButtonDown(Buttons.LeftTrigger)
                    && _prevPad.IsButtonUp(Buttons.LeftTrigger)
                );
            if (doPlace)
            {
                if (
                    _world.Raycast(
                        _player.EyePosition,
                        _player.Forward,
                        6f,
                        out var hit,
                        out var normal
                    )
                )
                {
                    int px = hit.X + normal.X;
                    int py = hit.Y + normal.Y;
                    int pz = hit.Z + normal.Z;
                    if (
                        !_player.IntersectsBlock(px, py, pz)
                        && BlockInfo.IsReplaceable(_world.GetBlock(px, py, pz))
                        && TryConsumeSelectedHotbarSlot(out var placedType)
                    )
                    {
                        _hand.Swing();
                        _world.SetBlock(px, py, pz, placedType);
                    }
                }
            }

            // Hotbar: D-Pad ←/→ ou Bumpers (LB/RB) - igual ao Xbox
            // (Y = inventário, B = agachar, LT = agachar, LB = largar item — reservados para implementação futura)
            if (
                (pad.DPad.Left == ButtonState.Pressed && _prevPad.DPad.Left == ButtonState.Released)
                || (
                    pad.Buttons.LeftShoulder == ButtonState.Pressed
                    && _prevPad.Buttons.LeftShoulder == ButtonState.Released
                )
            )
            {
                _selectedHotbarIndex = (_selectedHotbarIndex - 1 + 9) % 9;
            }
            if (
                (
                    pad.DPad.Right == ButtonState.Pressed
                    && _prevPad.DPad.Right == ButtonState.Released
                )
                || (
                    pad.Buttons.RightShoulder == ButtonState.Pressed
                    && _prevPad.Buttons.RightShoulder == ButtonState.Released
                )
            )
            {
                _selectedHotbarIndex = (_selectedHotbarIndex + 1) % 9;
            }
        }

        _entities.Update(dt, _player.Position);

        _hand.Update(dt, _player.HorizontalSpeed, _player.OnGround, new Vector2(mouseDeltaX, mouseDeltaY), _inventory[_selectedHotbarIndex].Type);

        _world.EnsureChunksAround(_player.Position);
        _world.RebuildDirtyMeshes(GraphicsDevice);

        _prevMouse = Mouse.GetState();
        _prevKeyboard = keyboard;
        _prevPad = pad;

        base.Update(gameTime);
    }

    private void UpdateInventoryUI(MouseState mouse, MouseState prevMouse)
    {
        if (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
        {
            int cx = GraphicsDevice.Viewport.Width / 2;
            int cy = GraphicsDevice.Viewport.Height / 2;

            int slotSize = Math.Max(36, GraphicsDevice.Viewport.Height / 15);
            int pad = Math.Max(4, slotSize / 9);

            int columns = 9;
            int rows = 3;

            int invWidth = columns * (slotSize + pad) - pad;
            int invHeight = rows * (slotSize + pad) - pad;
            int startX = cx - invWidth / 2;
            int startY = cy - invHeight / 2 - 40;

            // Clique nos slots da mochila (índices 9 a 35)
            for (int i = 9; i < 36; i++)
            {
                int col = (i - 9) % columns;
                int row = (i - 9) / columns;
                int x = startX + col * (slotSize + pad);
                int y = startY + row * (slotSize + pad);

                if (
                    mouse.X >= x
                    && mouse.X <= x + slotSize
                    && mouse.Y >= y
                    && mouse.Y <= y + slotSize
                )
                {
                    HandleSlotClick(i);
                    return;
                }
            }

            // Clique na hotbar inferior (índices 0 a 8)
            int hotbarWidth = 9 * (slotSize + pad) - pad;
            int hotbarStartX = cx - hotbarWidth / 2;
            int hotbarY = GraphicsDevice.Viewport.Height - slotSize - 12;

            for (int i = 0; i < 9; i++)
            {
                int x = hotbarStartX + i * (slotSize + pad);
                if (
                    mouse.X >= x
                    && mouse.X <= x + slotSize
                    && mouse.Y >= hotbarY
                    && mouse.Y <= hotbarY + slotSize
                )
                {
                    HandleSlotClick(i);
                    return;
                }
            }

            // Clicar fora dropa o item se estiver arrastando
            if (_draggedSlot.HasValue)
            {
                AddToInventory(_draggedSlot.Value.Type, _draggedSlot.Value.Count);
                _draggedSlot = null;
            }
        }
    }

    private void HandleSlotClick(int index)
    {
        var clickedSlot = _inventory[index];

        if (_draggedSlot.HasValue)
        {
            // Se já está arrastando algo
            if (clickedSlot.Type == _draggedSlot.Value.Type && clickedSlot.Count < 64)
            {
                // Junta os stacks se forem do mesmo tipo e não estiver cheio
                int space = 64 - clickedSlot.Count;
                int toAdd = Math.Min(space, _draggedSlot.Value.Count);
                _inventory[index].Count += toAdd;
                var dragged = _draggedSlot.Value;
                dragged.Count -= toAdd;
                if (dragged.Count <= 0)
                    _draggedSlot = null;
                else
                    _draggedSlot = dragged;
            }
            else
            {
                // Troca física (swap) inteira
                _inventory[index] = _draggedSlot.Value;
                if (clickedSlot.Type != BlockType.Air && clickedSlot.Count > 0)
                    _draggedSlot = clickedSlot;
                else
                    _draggedSlot = null;
            }
        }
        else if (clickedSlot.Type != BlockType.Air && clickedSlot.Count > 0)
        {
            // Pega do slot
            _draggedSlot = clickedSlot;
            _inventory[index] = new InventorySlot(BlockType.Air, 0);
        }
    }

    private Color GetSkyColor(float timeOfDay)
    {
        Color day = new Color(158, 194, 243);
        Color night = new Color(5, 5, 15);
        Color sunset = new Color(253, 148, 84);

        float sunAngle = timeOfDay * MathHelper.TwoPi - MathHelper.PiOver2;
        float sunHeight = MathF.Sin(sunAngle);

        if (sunHeight > 0.2f)
            return day;
        if (sunHeight < -0.2f)
            return night;

        float t = (sunHeight + 0.2f) / 0.4f; // 0.0 na noite, 1.0 de dia

        if (t < 0.5f)
        {
            float t2 = t * 2f;
            return Color.Lerp(night, sunset, t2);
        }
        else
        {
            float t2 = (t - 0.5f) * 2f;
            return Color.Lerp(sunset, day, t2);
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        bool isHeadInWater = _player.IsHeadInWater;
        Color skyColor = isHeadInWater ? new Color(20, 40, 100) : GetSkyColor(_timeOfDay);

        // Ajustar brilho dos blocos (DiffuseColor do BasicEffect)
        float sunAngle = _timeOfDay * MathHelper.TwoPi - MathHelper.PiOver2;
        float sunHeight = MathF.Sin(sunAngle);
        float lightIntensity = MathHelper.Clamp((sunHeight + 0.2f) / 0.4f, 0.15f, 1.0f);
        _effect.DiffuseColor = new Vector3(lightIntensity, lightIntensity, lightIntensity * 1.1f); // Tom levemente azulado à noite

        if (isHeadInWater && lightIntensity < 1f)
        {
            // Escurece a água também de noite
            skyColor = new Color(
                (int)(skyColor.R * lightIntensity),
                (int)(skyColor.G * lightIntensity),
                (int)(skyColor.B * lightIntensity)
            );
        }

        GraphicsDevice.Clear(skyColor);

        if (isHeadInWater)
        {
            _effect.FogColor = skyColor.ToVector3();
            _effect.FogStart = 0f;
            _effect.FogEnd = 12f; // Água turva
        }
        else
        {
            _effect.FogColor = skyColor.ToVector3();
            _effect.FogStart = (VoxelWorld.RenderDistance - 2) * Chunk.Size;
            _effect.FogEnd = VoxelWorld.RenderDistance * Chunk.Size;
        }

        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        GraphicsDevice.BlendState = BlendState.Opaque;

        var view = Matrix.CreateLookAt(
            _player.EyePosition,
            _player.EyePosition + _player.Forward,
            Vector3.Up
        );
        var projection = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(70f),
            GraphicsDevice.Viewport.AspectRatio,
            0.1f,
            1000f
        );

        _effect.View = view;
        _effect.Projection = projection;
        _effect.World = Matrix.Identity;

        _world.Draw(GraphicsDevice, _effect, _player.EyePosition, () => { });
        _entities.Draw(_effect);

        _hand.Draw(GraphicsDevice, _effect);

        DrawHud();

        base.Draw(gameTime);

        if (_takeScreenshot)
        {
            _takeScreenshot = false;
            TakeScreenshot();
        }
    }

    private void TakeScreenshot()
    {
        int w = GraphicsDevice.PresentationParameters.BackBufferWidth;
        int h = GraphicsDevice.PresentationParameters.BackBufferHeight;
        Color[] colors = new Color[w * h];
        GraphicsDevice.GetBackBufferData(colors);

        using (var tex = new Texture2D(GraphicsDevice, w, h))
        {
            tex.SetData(colors);
            string filename = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            using (var stream = File.OpenWrite(filename))
            {
                tex.SaveAsPng(stream, w, h);
            }
            Console.WriteLine($"Screenshot saved as {filename}");
        }
    }

    private void DrawHud()
    {
        _spriteBatch.Begin();
        int cx = GraphicsDevice.Viewport.Width / 2;
        int cy = GraphicsDevice.Viewport.Height / 2;

        if (_isMainMenuOpen)
        {
            int width = GraphicsDevice.Viewport.Width;
            int height = GraphicsDevice.Viewport.Height;
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, width, height), Color.Black * 0.9f);

            int titleScale = Math.Max(2, GraphicsDevice.Viewport.Height / 80);
            int subtitleScale = Math.Max(1, titleScale / 2);
            int textScale = Math.Max(1, titleScale / 3);

            if (_skinMenuOpen)
            {
                string title = "CHOOSE YOUR SKIN COLOR";
                int titleWidth = title.Length * 4 * subtitleScale;
                _textRenderer.DrawString(
                    _spriteBatch,
                    title,
                    cx - titleWidth / 2,
                    cy - titleScale * 8,
                    subtitleScale,
                    Color.White
                );

                var colors = GetSkinColors();
                var rects = GetSkinButtonRects();
                for (int i = 0; i < colors.Length; i++)
                {
                    _spriteBatch.Draw(_pixel, rects[i], colors[i]);
                    DrawRectOutline(new Rectangle(rects[i].X - 2, rects[i].Y - 2, rects[i].Width + 4, rects[i].Height + 4), 2, Color.White);
                }
            }
            else
            {
                string title = "MonoCraft";
                int titleWidth = title.Length * 4 * titleScale;
                _textRenderer.DrawString(
                    _spriteBatch,
                    title,
                    cx - titleWidth / 2,
                    cy - titleScale * 20,
                    titleScale,
                    Color.White
                );

                string subtitle = _hasSelectedSkin ? "RESUME GAME" : "START NEW GAME";
                int subtitleWidth = subtitle.Length * 4 * subtitleScale;
                var startRect = GetStartButtonRect();

                _spriteBatch.Draw(_pixel, startRect, Color.DarkGreen);

                _textRenderer.DrawString(
                    _spriteBatch,
                    subtitle,
                    startRect.X + (startRect.Width - subtitleWidth) / 2,
                    startRect.Y + (startRect.Height - 5 * subtitleScale) / 2,
                    subtitleScale,
                    Color.White,
                    false
                );

                string credits = "Credits: Carlos Felipe Araujo";
                int creditsWidth = credits.Length * 4 * textScale;
                _textRenderer.DrawString(
                    _spriteBatch,
                    credits,
                    cx - creditsWidth / 2,
                    cy + titleScale * 16,
                    textScale,
                    Color.Gray
                );
            }
        }
        else if (_isExitMenuOpen)
        {
            int width = GraphicsDevice.Viewport.Width;
            int height = GraphicsDevice.Viewport.Height;
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, width, height), Color.Black * 0.9f);

            int quitScale = Math.Max(2, GraphicsDevice.Viewport.Height / 180);
            string quitText = "QUIT? Y/N";
            int quitWidth = quitText.Length * 4 * quitScale;
            _textRenderer.DrawString(
                _spriteBatch,
                quitText,
                cx - quitWidth / 2,
                cy - quitScale * 4,
                quitScale,
                Color.White
            );

            var yesRect = GetYesButtonRect();
            var noRect = GetNoButtonRect();

            _spriteBatch.Draw(_pixel, yesRect, Color.DarkGreen);
            _spriteBatch.Draw(_pixel, noRect, Color.DarkRed);

            _textRenderer.DrawString(
                _spriteBatch,
                "YES",
                yesRect.X + (yesRect.Width - 3 * 4 * quitScale) / 2,
                yesRect.Y + (yesRect.Height - 5 * quitScale) / 2,
                quitScale,
                Color.White,
                false
            );
            _textRenderer.DrawString(
                _spriteBatch,
                "NO",
                noRect.X + (noRect.Width - 2 * 4 * quitScale) / 2,
                noRect.Y + (noRect.Height - 5 * quitScale) / 2,
                quitScale,
                Color.White,
                false
            );
        }
        else
        {
            // Crosshair
            DrawCrosshair(cx, cy);

            // Hotbar simples (quadrados coloridos)
            int slot = Math.Max(36, GraphicsDevice.Viewport.Height / 15);
            int pad = Math.Max(4, slot / 9);
            int totalWidth = 9 * (slot + pad) - pad;
            int startX = cx - totalWidth / 2;
            int y = GraphicsDevice.Viewport.Height - slot - 12;

            if (_isInventoryOpen)
            {
                // Desfocar o fundo (overlay translúcido)
                _spriteBatch.Draw(
                    _pixel,
                    new Rectangle(
                        0,
                        0,
                        GraphicsDevice.Viewport.Width,
                        GraphicsDevice.Viewport.Height
                    ),
                    Color.Black * 0.6f
                );

                int columns = 9;
                int rows = 3;

                int invWidth = columns * (slot + pad) - pad;
                int invHeight = rows * (slot + pad) - pad;
                int invStartX = cx - invWidth / 2;
                int invStartY = cy - invHeight / 2 - 40;

                // Fundo do inventário
                _spriteBatch.Draw(
                    _pixel,
                    new Rectangle(invStartX - 8, invStartY - 8, invWidth + 16, invHeight + 16),
                    Color.Black * 0.8f
                );

                // Desenhar mochila (índices 9 a 35)
                for (int i = 9; i < 36; i++)
                {
                    int col = (i - 9) % columns;
                    int row = (i - 9) / columns;
                    int slotX = invStartX + col * (slot + pad);
                    int slotY = invStartY + row * (slot + pad);

                    _spriteBatch.Draw(
                        _pixel,
                        new Rectangle(slotX, slotY, slot, slot),
                        Color.Black * 0.6f
                    );

                    var item = _inventory[i];
                    if (item.Type != BlockType.Air && item.Count > 0)
                    {
                        _spriteBatch.Draw(
                            _pixel,
                            new Rectangle(slotX, slotY, slot, slot),
                            BlockInfo.GetTopColor(item.Type)
                        );
                        int numScale = Math.Max(1, slot / 18);
                        _textRenderer.DrawNumber(
                            _spriteBatch,
                            item.Count,
                            slotX + slot - (numScale + 1),
                            slotY + slot - (numScale * 6),
                            numScale
                        );
                    }
                }
            }

            // Desenhar Hotbar (índices 0 a 8)
            for (int i = 0; i < 9; i++)
            {
                int x = startX + i * (slot + pad);
                bool selected = i == _selectedHotbarIndex && !_isInventoryOpen;

                _spriteBatch.Draw(
                    _pixel,
                    new Rectangle(x - 2, y - 2, slot + 4, slot + 4),
                    _isInventoryOpen
                        ? Color.Black * 0.8f
                        : (selected ? Color.LightGray : Color.Black * 0.5f)
                );

                // Sempre desenha o fundo interno do slot para não ficar um quadrado sólido
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, slot, slot), Color.Black * 0.6f);

                var item = _inventory[i];
                if (item.Type != BlockType.Air && item.Count > 0)
                {
                    _spriteBatch.Draw(
                        _pixel,
                        new Rectangle(x, y, slot, slot),
                        BlockInfo.GetTopColor(item.Type)
                    );
                    int numScale = Math.Max(1, slot / 18);
                    _textRenderer.DrawNumber(
                        _spriteBatch,
                        item.Count,
                        x + slot - (numScale + 1),
                        y + slot - (numScale * 6),
                        numScale
                    );
                }
            }

            // Desenhar bloco no cursor se estiver arrastando
            if (_isInventoryOpen && _draggedSlot.HasValue)
            {
                var mouseState = Mouse.GetState();
                var dragged = _draggedSlot.Value;
                _spriteBatch.Draw(
                    _pixel,
                    new Rectangle(mouseState.X - slot / 2, mouseState.Y - slot / 2, slot, slot),
                    BlockInfo.GetTopColor(dragged.Type)
                );
                int numScale = Math.Max(1, slot / 18);
                _textRenderer.DrawNumber(
                    _spriteBatch,
                    dragged.Count,
                    mouseState.X + slot / 2 - (numScale + 1),
                    mouseState.Y + slot / 2 - (numScale * 6),
                    numScale
                );
            }
        }

        _spriteBatch.End();

#if ANDROID
        _spriteBatch.Begin(blendState: BlendState.AlphaBlend);
        _virtualGamepad.Draw(_spriteBatch, _textRenderer);
        _spriteBatch.End();
#endif
    }

    private void DrawCrosshair(int cx, int cy)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(cx - 8, cy - 1, 16, 2), Color.White * 0.8f);
        _spriteBatch.Draw(_pixel, new Rectangle(cx - 1, cy - 8, 2, 16), Color.White * 0.8f);
    }

    private Point GetWindowCenter() =>
        new(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);

    private void CenterMouse()
    {
        var center = GetWindowCenter();
        Mouse.SetPosition(center.X, center.Y);
    }

    private void OnWindowClientSizeChanged(object sender, EventArgs e)
    {
        Window.ClientSizeChanged -= OnWindowClientSizeChanged;

        _graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
        _graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
        _graphics.ApplyChanges();

        Window.ClientSizeChanged += OnWindowClientSizeChanged;
    }

    private Rectangle GetYesButtonRect()
    {
        int cx = GraphicsDevice.Viewport.Width / 2;
        int cy = GraphicsDevice.Viewport.Height / 2;
        int scale = Math.Max(2, GraphicsDevice.Viewport.Height / 180);
        int w = 24 * scale;
        int h = 12 * scale;
        return new Rectangle(cx - w - 4 * scale, cy + 4 * scale, w, h);
    }

    private Rectangle GetNoButtonRect()
    {
        int cx = GraphicsDevice.Viewport.Width / 2;
        int cy = GraphicsDevice.Viewport.Height / 2;
        int scale = Math.Max(2, GraphicsDevice.Viewport.Height / 180);
        int w = 24 * scale;
        int h = 12 * scale;
        return new Rectangle(cx + 4 * scale, cy + 4 * scale, w, h);
    }

    private Rectangle GetStartButtonRect()
    {
        int cx = GraphicsDevice.Viewport.Width / 2;
        int cy = GraphicsDevice.Viewport.Height / 2;
        int titleScale = Math.Max(2, GraphicsDevice.Viewport.Height / 80);
        int subtitleScale = Math.Max(1, titleScale / 2);

        string subtitle = "START NEW GAME";
        int subtitleWidth = subtitle.Length * 4 * subtitleScale;

        int w = subtitleWidth + 8 * subtitleScale;
        int h = 12 * subtitleScale;

        return new Rectangle(cx - w / 2, cy + titleScale * 2, w, h);
    }

    private Color[] GetSkinColors() => new[]
    {
        new Color(255, 219, 172), // Tone 1
        new Color(222, 170, 128), // Tone 2 (Original)
        new Color(141, 85, 36),   // Tone 3
        new Color(77, 48, 22)     // Tone 4
    };

    private Rectangle[] GetSkinButtonRects()
    {
        int w = GraphicsDevice.Viewport.Width;
        int h = GraphicsDevice.Viewport.Height;
        int titleScale = Math.Max(2, h / 80);
        int buttonSize = titleScale * 8;
        int spacing = titleScale * 2;
        int cy = h / 2;

        var colors = GetSkinColors();
        var rects = new Rectangle[colors.Length];

        int totalWidth = colors.Length * buttonSize + (colors.Length - 1) * spacing;
        int startX = w / 2 - totalWidth / 2;

        for (int i = 0; i < colors.Length; i++)
        {
            rects[i] = new Rectangle(startX + i * (buttonSize + spacing), cy - buttonSize / 2, buttonSize, buttonSize);
        }
        return rects;
    }

    private void DrawRectOutline(Rectangle r, int t, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, t), color);
        _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Bottom - t, r.Width, t), color);
        _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, t, r.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(r.Right - t, r.Y, t, r.Height), color);
    }
}
