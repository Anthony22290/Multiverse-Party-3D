using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Which touch controls each scene shows (2026-11-21). Every button presses the same key / mouse button the PC
/// version uses (see PartyGames.Controls), so the minigames need no changes.
/// Positions are in 1280×720 units from the anchor corner (x to the left is negative for Right-anchored buttons).
/// </summary>
public static class MobileLayouts
{
    public enum Look { None, Drag, RightDrag }
    public enum Anchor { Right, Left, Center, TopLeft }

    public struct Button
    {
        public string Label; public Key Key, Key2; public int Mouse; public Color Color;
        public Vector2 Position; public float Radius; public Anchor Anchor;
    }

    public sealed class Layout
    {
        public bool Stick = true;          // floating joystick on the left: WASD + gamepad left stick
        public bool Triggers;              // joystick up/down also drive the gamepad triggers (karts)
        public Look Look = Look.None;      // drag on the right half: mouse delta (RightDrag also holds the right button)
        public float LookSpeed = 1f;
        public Button[] Buttons = new Button[0];
        public Func<bool> Pad;             // when true, the code pad replaces the controls
        public bool Empty => !Stick && Look == Look.None && Buttons.Length == 0;
    }

    public static readonly Layout Empty = new Layout { Stick = false };

    // Colours of the main actions.
    static readonly Color Jump = new Color(.35f, .75f, 1f), Act = new Color(1f, .72f, .25f), Shove = new Color(1f, .45f, .4f),
        Other = new Color(.65f, .6f, 1f), Grey = new Color(.7f, .72f, .78f), Good = new Color(.45f, .9f, .5f);

    // Slots around the bottom-right corner: 0 = main (big), then a ring around it.
    static readonly Vector2[] Slots =
    {
        new Vector2(-135, 135), new Vector2(-300, 100), new Vector2(-255, 255), new Vector2(-110, 300),
        new Vector2(-440, 85), new Vector2(-400, 225), new Vector2(-280, 395), new Vector2(-130, 450),
    };

    static Button B(int slot, string label, Key key, Color color, int mouse = 0, Key key2 = Key.None) =>
        new Button { Label = label, Key = key, Key2 = key2, Mouse = mouse, Color = color, Position = Slots[slot], Radius = slot == 0 ? 82 : 58, Anchor = Anchor.Right };

    static Button At(Anchor a, Vector2 pos, float radius, string label, Key key, Color color, int mouse = 0) =>
        new Button { Label = label, Key = key, Mouse = mouse, Color = color, Position = pos, Radius = radius, Anchor = a };

    static Button Pause() => At(Anchor.TopLeft, new Vector2(34, -34), 24, "II", Key.Escape, Grey);

    public static Layout For(string scene)
    {
        switch (scene)
        {
            case MultiverseParty.HubScene:
                return new Layout
                {
                    Look = Look.RightDrag,
                    Buttons = new[]
                    {
                        B(0, "SALTAR", Key.Space, Jump), B(1, "EMPUJAR", Key.F, Shove), B(2, "CHAT", Key.T, Other),
                        B(4, "HOLA", Key.Z, Grey), B(5, "¡YUJU!", Key.X, Grey), B(3, "BURLA", Key.C, Grey),
                    }
                };
            case "ArenaDelCaos":
                return new Layout
                {
                    Look = Look.Drag,
                    Buttons = new[]
                    {
                        B(0, "SALTAR", Key.Space, Jump), B(1, "GOLPE", Key.E, Shove), B(2, "MANO IZQ.", Key.None, Act, 1),
                        B(3, "MANO DER.", Key.None, Act, 2), B(4, "LEVANTAR", Key.LeftShift, Other), B(5, "LANZAR", Key.F, Other), Pause(),
                    }
                };
            case "InvasionZombi":
                return new Layout
                {
                    Look = Look.RightDrag,
                    Buttons = new[] { B(0, "GOLPE", Key.J, Shove), B(1, "USAR", Key.E, Act), B(2, "SOLTAR", Key.Q, Grey), Pause() }
                };
            case "CarrerasLocas":
                return new Layout
                {
                    Triggers = true,
                    Buttons = new[] { B(0, "OBJETO", Key.Space, Act), B(1, "DERRAPE", Key.LeftShift, Jump), B(2, "PISTA", Key.R, Grey), Pause() }
                };
            case "CodigoMortal":
                return new Layout
                {
                    Look = Look.RightDrag,
                    Buttons = new[] { B(0, "SALTAR", Key.Space, Jump), B(1, "TERMINAL", Key.E, Act), B(2, "PROTEGER", Key.Q, Good), Pause() },
                    Pad = () => { var d = UnityEngine.Object.FindAnyObjectByType<MortalCodeDirector>(); return d != null && d.HUD != null && d.HUD.TerminalOpen; }
                };
            case "SalaDeEscape":
                return new Layout
                {
                    Look = Look.Drag,
                    Buttons = new[] { B(0, "USAR", Key.E, Act), B(1, "SOLTAR", Key.Q, Grey), B(2, "PISTA", Key.H, Other), Pause() },
                    Pad = () => { var h = UnityEngine.Object.FindAnyObjectByType<EscapeHUD>(); return h != null && h.KeypadOpen; }
                };
            case "RodillosDelCalabozo":
                return new Layout
                {
                    Buttons = new[]
                    {
                        B(0, "SALTAR", Key.Space, Jump), B(1, "AGACHAR", Key.LeftShift, Other),
                        At(Anchor.Right, new Vector2(-110, 560), 44, "◄ CÁM", Key.Q, Grey), At(Anchor.Right, new Vector2(-220, 560), 44, "CÁM ►", Key.E, Grey), Pause(),
                    }
                };
            case "PicoPalaTijera":
                return new Layout
                {
                    Stick = false,
                    Buttons = new[]
                    {
                        At(Anchor.Right, new Vector2(-170, 170), 120, "CAVAR", Key.Space, Act),
                        At(Anchor.Left, new Vector2(110, 110), 72, "1\nPALA", Key.Digit1, new Color(.85f, .62f, .35f)),
                        At(Anchor.Left, new Vector2(270, 110), 72, "2\nPICO", Key.Digit2, Grey),
                        At(Anchor.Left, new Vector2(430, 110), 72, "3\nTIJERA", Key.Digit3, Good), Pause(),
                    }
                };
            case "ArmaTuNave":
                return new Layout
                {
                    Look = Look.RightDrag,
                    Buttons = new[] { B(0, "SALTAR", Key.Space, Jump), B(1, "COGER", Key.E, Act), B(2, "SOLTAR", Key.Q, Grey), B(3, "EMPUJAR", Key.F, Shove), Pause() }
                };
            case "Propulsores":
                return new Layout { Stick = false, Buttons = new[] { At(Anchor.Right, new Vector2(-190, 190), 140, "¡PROPULSOR!", Key.Space, Jump), Pause() } };
            case "MinigolfLoco":
                return new Layout
                {
                    Look = Look.RightDrag,
                    Buttons = new[] { B(0, "GOLPE", Key.Space, Act), B(1, "FINO", Key.LeftShift, Other), B(2, "TARJETA", Key.Tab, Grey), Pause() }
                };
            case "PatataCaliente":
                return new Layout { Buttons = new[] { B(0, "SALTAR", Key.Space, Jump), B(1, "ACELERÓN", Key.LeftShift, Shove), Pause() } };
            case "GallinasLocas":
                return new Layout { Buttons = new[] { B(0, "COGER", Key.E, Act), B(1, "EMPUJÓN", Key.LeftShift, Shove), B(2, "SALTAR", Key.Space, Jump), Pause() } };
            case "RepartoLoco":
                return new Layout { Buttons = new[] { B(0, "SALTAR", Key.Space, Jump), B(1, "ACELERÓN", Key.LeftShift, Shove), Pause() } };
            case "Atrapalo":
                return new Layout { Stick = false, Buttons = new[] { At(Anchor.Right, new Vector2(-190, 190), 140, "¡ATRAPAR!", Key.Space, Act), Pause() } };
            case "SueloQueCae":
                return new Layout { Buttons = new[] { B(0, "SALTAR", Key.Space, Jump), Pause() } };
            case "PinturaLoca":
                return new Layout { Buttons = new[] { B(0, "SALTAR", Key.Space, Jump), B(1, "EMPUJÓN", Key.LeftShift, Shove), Pause() } };
            case "ReyColina":
                return new Layout { Buttons = new[] { B(0, "SALTAR", Key.Space, Jump), B(1, "BOMBA", Key.E, Act), B(2, "EMPUJÓN", Key.LeftShift, Shove), Pause() } };
            case "MemoriaMusical":
                return new Layout
                {
                    Stick = false,
                    Buttons = new[]
                    {
                        At(Anchor.Center, new Vector2(0, 330), 82, "ROJO", Key.UpArrow, new Color(1f, .3f, .3f)),
                        At(Anchor.Center, new Vector2(170, 175), 82, "AZUL", Key.RightArrow, new Color(.3f, .55f, 1f)),
                        At(Anchor.Center, new Vector2(0, 100), 82, "VERDE", Key.DownArrow, new Color(.3f, .85f, .4f)),
                        At(Anchor.Center, new Vector2(-170, 175), 82, "AMARILLO", Key.LeftArrow, new Color(1f, .85f, .25f)), Pause(),
                    }
                };
        }
        return Empty;   // main menu and anything else: only the game's own buttons
    }
}
