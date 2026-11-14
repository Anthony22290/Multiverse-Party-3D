using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Builds the tower at runtime: ten rooms in a row along +Z with shared architecture (bevelled stone, gold
/// trims, pilasters, sconces), a sliding door between rooms, a crest sign above each exit, and the starry
/// "multiverse" void around and below. Each room adds its own puzzle pieces and themed decor.
/// </summary>
public sealed class EscapeLevel : MonoBehaviour
{
    public const float WallHeight = 3.6f, WallThickness = .5f, DoorWidth = 3f;
    /// <summary>Tests: build exactly these rooms instead of a random draw.</summary>
    public static System.Type[] Override;
    public const int RoomsPerRun = 11;
    EscapeDoor[] doors;
    public Transform Shell { get; private set; }

    /// <summary>The pool: five new first-person puzzles and four reworked classics. The portal always closes the tower.</summary>
    public static readonly System.Type[] NewRooms = { typeof(RoomClock), typeof(RoomBells), typeof(RoomMirrors), typeof(RoomCipher), typeof(RoomSlider),
        typeof(RoomLanterns), typeof(RoomJugs), typeof(RoomPipes), typeof(RoomNumbers), typeof(RoomFootprints) };
    public static readonly System.Type[] ClassicRooms = { typeof(RoomSymbols), typeof(RoomLibrary), typeof(RoomTiles), typeof(RoomBraziers) };
    public static System.Type[] AllRooms => NewRooms.Concat(ClassicRooms).Append(typeof(RoomPortal)).ToArray();
    System.Type[] RoomTypes;

    static System.Type[] Draw()
    {
        var fresh = NewRooms.OrderBy(_ => Random.value).Take(RoomsPerRun - 3);
        var classic = ClassicRooms.OrderBy(_ => Random.value).Take(2);
        return fresh.Concat(classic).OrderBy(_ => Random.value).Append(typeof(RoomPortal)).ToArray();
    }

    public EscapeRoom[] Build(EscapeDirector director)
    {
#if UNITY_EDITOR
        // Tests: PlayerPrefs "Escape_Salas" = comma separated room class names (consumed once).
        string forced = PlayerPrefs.GetString("Escape_Salas", "");
        if (forced.Length > 0)
        {
            PlayerPrefs.DeleteKey("Escape_Salas");
            Override = forced.Split(',').Select(n => AllRooms.FirstOrDefault(t => t.Name == n.Trim())).Where(t => t != null).ToArray();
        }
#endif
        RoomTypes = Override != null && Override.Length > 0 ? Override : Draw();
        var root = new GameObject("Torre del Multiverso").transform;
        root.SetParent(transform, false);
        Shell = new GameObject("Estructura").transform;
        Shell.SetParent(root, false);
        Shell.gameObject.AddComponent<EscapeShell>();

        var rooms = new EscapeRoom[RoomTypes.Length];
        doors = new EscapeDoor[RoomTypes.Length - 1];
        for (int i = 0; i < RoomTypes.Length; i++)
        {
            var go = new GameObject($"Sala {i + 1}");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0, 0, i * EscapeDirector.RoomLength);
            var room = (EscapeRoom)go.AddComponent(RoomTypes[i]);
            room.Setup(director, i);
            BuildShell(room, i, i == RoomTypes.Length - 1);
            room.Build();
            rooms[i] = room;
        }
        BuildVoid(root, RoomTypes.Length);
        Physics.SyncTransforms();
        return rooms;
    }

    public void OpenDoor(int index)
    {
        if (doors != null && index >= 0 && index < doors.Length && doors[index] != null) doors[index].Open();
    }

    void BuildShell(EscapeRoom room, int index, bool last)
    {
        float L = EscapeDirector.RoomLength, W = EscapeDirector.RoomWidth, H = WallHeight, T = WallThickness;
        var shell = new GameObject($"Estructura sala {index + 1}").transform;
        shell.SetParent(Shell, false);
        shell.localPosition = room.transform.localPosition;

        Material floor = EscapeKit.Paint(room.Floor, .15f), floorTrim = EscapeKit.Paint(Color.Lerp(room.Floor, Color.black, .18f), .15f);
        Material wall = EscapeKit.Paint(room.Wall, .1f), trim = EscapeKit.Mat("Dorado", new Color(1f, .78f, .3f));
        Material accent = EscapeKit.Paint(room.Accent, .3f), dark = EscapeKit.Paint(Color.Lerp(room.Wall, Color.black, .35f), .1f);

        // Floor (or the room's own segments, e.g. the tile bridge over the void).
        var segments = room.FloorSegments ?? new[] { new Vector2(-L * .5f, L * .5f) };
        foreach (var s in segments)
        {
            float len = s.y - s.x, mid = (s.x + s.y) * .5f;
            EscapeKit.Block(shell, "Suelo", new Vector3(0, -.2f, mid), new Vector3(W + T * 2f, .4f, len), floor, .04f);
            EscapeKit.Block(shell, "Cenefa del suelo", new Vector3(0, .005f, mid), new Vector3(W - 1.6f, .02f, Mathf.Max(.2f, len - 1.6f)), floorTrim, .01f, false);
            EscapeKit.Block(shell, "Alfombra", new Vector3(0, .012f, mid), new Vector3(W - 2.2f, .02f, Mathf.Max(.1f, len - 2.2f)), floor, .01f, false);
        }
        float wallBottom = room.FloorSegments != null ? -7f : 0f;
        float wallH = H - wallBottom;

        // Side walls with a gold cap and pilasters.
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * (W * .5f + T * .5f);
            EscapeKit.Block(shell, "Muro lateral", new Vector3(x, wallBottom + wallH * .5f, 0), new Vector3(T, wallH, L + T), wall, .05f);
            EscapeKit.Block(shell, "Remate", new Vector3(x, H + .06f, 0), new Vector3(T + .2f, .12f, L + T), trim, .03f, false);
            EscapeKit.Block(shell, "Zócalo", new Vector3(side * (W * .5f - .04f), .22f, 0), new Vector3(.1f, .44f, L - .2f), dark, .02f, false);
            for (int p = -1; p <= 1; p++)
            {
                float z = p * 7.2f;
                EscapeKit.Block(shell, "Pilastra", new Vector3(side * (W * .5f - .1f), H * .5f, z), new Vector3(.32f, H, .55f), wall, .06f);
                EscapeKit.Block(shell, "Capitel", new Vector3(side * (W * .5f - .12f), H - .12f, z), new Vector3(.42f, .2f, .66f), trim, .04f, false);
            }
            // Sconces: glowing lamp boxes on the walls between pilasters.
            var lampMat = EscapeKit.Glow(room.Glow, 2.2f);
            for (int s = -1; s <= 1; s += 2)
            {
                EscapeKit.Block(shell, "Aplique", new Vector3(side * (W * .5f - .12f), 2.55f, s * 3.6f), new Vector3(.18f, .34f, .26f), lampMat, .04f, false);
                EscapeKit.Block(shell, "Soporte del aplique", new Vector3(side * (W * .5f - .06f), 2.32f, s * 3.6f), new Vector3(.12f, .1f, .32f), trim, .02f, false);
            }
        }
        // Room lights.
        EscapeKit.PointLight(shell, new Vector3(-4.5f, 3.9f, -4f), room.Glow, 15f, 1.6f);
        EscapeKit.PointLight(shell, new Vector3(4.5f, 3.9f, 4.5f), room.Glow, 15f, 1.6f);
        // A wooden beam lattice overhead makes the open ceiling feel like a room in first person.
        var beamMat = EscapeKit.Paint(Color.Lerp(room.Wall, new Color(.3f, .2f, .12f), .7f), .1f);
        for (int b = -2; b <= 2; b++)
            EscapeKit.Block(shell, "Viga", new Vector3(0, H + .15f, b * (L / 5f)), new Vector3(W + T * 2f, .3f, .34f), beamMat, .04f, false);

        // Back wall only for the first room.
        if (index == 0) Wall(shell, -L * .5f, false, wall, trim, wallBottom, wallH);

        // Front wall with door gap (solid for the last room).
        Wall(shell, L * .5f, !last, wall, trim, wallBottom, wallH);
        if (!last)
        {
            var doorRoot = new GameObject("Puerta").transform;
            doorRoot.SetParent(shell, false);
            doorRoot.localPosition = new Vector3(0, 0, L * .5f);
            var panel = EscapeKit.Block(doorRoot, "Hoja de la puerta", new Vector3(0, H * .5f, 0), new Vector3(DoorWidth, H, .3f), accent, .06f);
            EscapeKit.Block(panel.transform, "Relieve", new Vector3(0, 0, -.17f), new Vector3(.75f, .75f, .06f), trim, .02f, false);
            EscapeKit.Block(panel.transform, "Relieve trasero", new Vector3(0, 0, .17f), new Vector3(.75f, .75f, .06f), trim, .02f, false);
            for (int side = -1; side <= 1; side += 2)
                EscapeKit.Block(doorRoot, "Jamba", new Vector3(side * (DoorWidth * .5f + .2f), H * .5f + .1f, 0), new Vector3(.4f, H + .2f, .7f), trim, .05f);
            var lampMat = EscapeKit.Glow(Color.red, 2f);
            EscapeKit.Shape(PrimitiveType.Sphere, doorRoot, "Piloto", new Vector3(0, H + 1.28f, -.05f), Vector3.one * .32f, lampMat, false);
            var door = doorRoot.gameObject.AddComponent<EscapeDoor>();
            door.Setup(panel.transform, lampMat);
            doors[index] = door;
        }

        // Crest sign over the exit, readable from inside the room.
        var crest = EscapeKit.Block(shell, "Blasón", new Vector3(0, H + .55f, L * .5f - .05f), new Vector3(7.4f, 1.1f, .34f), dark, .08f, false);
        EscapeKit.Block(crest.transform, "Marco", new Vector3(0, 0, -.12f), new Vector3(7.6f, 1.24f, .12f), trim, .04f, false);
        EscapeKit.Text(shell, $"SALA {index + 1} · {room.Title.ToUpperInvariant()}", new Vector3(0, H + .55f, L * .5f - .26f), Vector3.zero, .034f, new Color(1f, .95f, .82f));

        // Big room number on the floor at the entrance.
        EscapeKit.Text(shell, (index + 1).ToString(), new Vector3(-W * .5f + 2.4f, .03f, -L * .5f + 2.2f), new Vector3(90, 0, 0), .16f, Color.Lerp(room.Floor, Color.black, .35f));
    }

    void Wall(Transform shell, float z, bool door, Material wall, Material trim, float bottom, float height)
    {
        float W = EscapeDirector.RoomWidth, T = WallThickness, H = WallHeight;
        if (!door)
        {
            EscapeKit.Block(shell, "Muro", new Vector3(0, bottom + height * .5f, z), new Vector3(W + T * 2f, height, T), wall, .05f);
            EscapeKit.Block(shell, "Remate", new Vector3(0, H + .06f, z), new Vector3(W + T * 2f + .2f, .12f, T + .2f), trim, .03f, false);
            return;
        }
        float seg = (W + T * 2f - DoorWidth) * .5f;
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * (DoorWidth * .5f + seg * .5f);
            EscapeKit.Block(shell, "Muro con puerta", new Vector3(x, bottom + height * .5f, z), new Vector3(seg, height, T), wall, .05f);
            EscapeKit.Block(shell, "Remate", new Vector3(x, H + .06f, z), new Vector3(seg + .1f, .12f, T + .2f), trim, .03f, false);
        }
    }

    void BuildVoid(Transform root, int count)
    {
        float L = EscapeDirector.RoomLength;
        var space = new GameObject("Vacío del multiverso").transform;
        space.SetParent(root, false);
        float midZ = (count - 1) * L * .5f;
        EscapeKit.Block(space, "Fondo", new Vector3(0, -14f, midZ), new Vector3(120f, .4f, count * L + 100f), EscapeKit.Paint(new Color(.1f, .06f, .2f), .05f), .1f, false);
        var rng = new System.Random(7);
        Color[] tints = { new Color(.55f, .9f, 1f), new Color(1f, .55f, .9f), new Color(1f, .9f, .5f), new Color(.6f, 1f, .7f) };
        var glows = new List<Material>();
        foreach (var t in tints) glows.Add(EscapeKit.Glow(t, .9f));
        for (int i = 0; i < 140; i++)
        {
            float x = (float)(rng.NextDouble() * 110 - 55), z = (float)(rng.NextDouble() * (count * L + 80) - 40);
            float s = .08f + (float)rng.NextDouble() * .2f;
            EscapeKit.Shape(PrimitiveType.Sphere, space, "Estrella", new Vector3(x, -13.6f + (float)rng.NextDouble() * 6f, z), Vector3.one * s, glows[i % glows.Count], false);
        }
        // Floating crystals around the tower, outside the walls.
        for (int i = 0; i < 46; i++)
        {
            float side = i % 2 == 0 ? -1 : 1;
            float x = side * (12f + (float)rng.NextDouble() * 26f), z = (float)(rng.NextDouble() * (count * L + 30) - 15);
            float y = -6f + (float)rng.NextDouble() * 9f, s = .5f + (float)rng.NextDouble() * 1.4f;
            var crystal = EscapeKit.Block(space, "Cristal flotante", new Vector3(x, y, z), new Vector3(s, s * 1.6f, s), glows[(i + 1) % glows.Count], s * .3f, false, new Vector3(45, i * 37, 45));
            var spin = crystal.AddComponent<EscapeSpin>();
            spin.axis = Vector3.up; spin.speed = 10f + i % 5 * 6f; spin.bob = .4f + (i % 3) * .2f; spin.bobSpeed = .5f + (i % 4) * .15f;
        }
    }
}
