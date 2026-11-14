#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Opt-in local development harness. Excluded entirely from release players.</summary>
public sealed class PirateNetworkTestProbe : MonoBehaviour
{
    [Serializable] public class Command { public string action; public float x, z, yaw, lookPitch; public int slot; public bool grab, rightGrab, lift; }
    [Serializable] public class Snapshot
    {
        public string status, code;
        public bool running, host, playing, finished, started, menu;
        public int frame, players, round, winner, localSlot;
        public float time;
        public int[] owners, flags, actions, cannonPhases;
        public Vector3[] positions, props;
        public int sharkPhase;
        public Vector3 shark;
    }
    private string directory;
    private float nextWrite, controlsUntil;
    private Command controls;
    public void Configure(string folder) { directory = Path.GetFullPath(folder); Directory.CreateDirectory(directory); }
    private void LateUpdate()
    {
        var session = PirateSession.Instance;
        if (session == null || directory == null) return;
        session.TestInputEnabled = true;
        string commandPath = Path.Combine(directory, "command.json");
        if (File.Exists(commandPath))
        {
            Command command;
            try { command = JsonUtility.FromJson<Command>(File.ReadAllText(commandPath)); File.Delete(commandPath); }
            catch (IOException) { return; } // Retry next frame if the external harness is replacing the file.
            if (command.action == "start") session.BeginOnlineRound();
            else if (command.action == "leave") _ = session.Leave(false);
            else if (command.action == "controls") { controls = command; controlsUntil = Time.time + 2f; }
            else if (command.action == "jump") session.LatestInput.Jump++;
            else if (command.action == "push") session.LatestInput.Push++;
            else if (command.action == "drop") session.LatestInput.Drop++;
            else if (command.action == "menu") session.SetMenu(command.grab);
            else if (command.action == "quit") Application.Quit();
            else if (command.action == "join") _ = session.Connect(false, session.RoomCode);
            else if (command.action == "quiet" && session.World != null && session.World.HasStateAuthority)
            {
                // Isolate controls from random hazards only inside the opt-in development harness.
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                foreach (var cannon in session.Arena.Cannons) { cannon.ResetHazard(); typeof(CannonHazard).GetField("nextShot", flags).SetValue(cannon, Time.time + 300f); }
                foreach (var shark in session.Arena.Sharks) { shark.ResetHazard(); typeof(SharkHazard).GetField("nextLeap", flags).SetValue(shark, Time.time + 300f); }
            }
            else if (command.action == "cannons" && session.World != null && session.World.HasStateAuthority)
                foreach (var cannon in session.Arena.Cannons) cannon.StartRound();
            else if (command.action == "eliminate" && session.World != null && session.World.HasStateAuthority)
                session.Arena.Eliminate(session.Arena.Fighters[command.slot], "prueba de eliminación");
            else if (command.action == "position" && session.World != null && session.World.HasStateAuthority)
            {
                var fighter = session.Arena.Fighters[command.slot]; fighter.ReleaseGrab(false);
                fighter.Body.position = new Vector3(command.x, session.Arena.SurfaceHeight(new Vector3(command.x, 0, command.z)) + 1f, command.z);
                fighter.Body.linearVelocity = Vector3.zero; fighter.Body.angularVelocity = Vector3.zero; fighter.Body.rotation = Quaternion.Euler(0, command.yaw, 0);
            }
        }
        if (controls != null && Time.time < controlsUntil)
        {
            session.LatestInput.Move = new Vector2(controls.x, controls.z); session.LatestInput.Yaw = controls.yaw;
            session.LatestInput.Grab = controls.grab; session.LatestInput.RightGrab = controls.rightGrab;
            session.LatestInput.LookPitch = controls.lookPitch; session.LatestInput.Lift = controls.lift;
        }
        else { session.LatestInput.Move = Vector2.zero; session.LatestInput.Grab = false; session.LatestInput.RightGrab = false; session.LatestInput.Lift = false; }
        if (Time.unscaledTime < nextWrite) return;
        nextWrite = Time.unscaledTime + 0.25f;
        var runner = session.Runner; var world = session.World;
        var state = new Snapshot { status = session.Status, code = session.RoomCode, frame = Time.frameCount, time = Time.time,
            running = runner != null && runner.IsRunning, host = runner != null && runner.IsServer, menu = session.MenuOpen, localSlot = -1,
            positions = session.Arena.Fighters.Select(f => f.transform.position).ToArray(),
            flags = session.Arena.Fighters.Select(f => (f.IsAlive ? 1 : 0) | (f.IsGrabbing ? 8 : 0) | (f.IsHeld ? 4 : 0) | (f.IsLifting ? 32 : 0)).ToArray(),
            props = session.Arena.Props.Select(p => p.transform.position).ToArray(),
            actions = session.Arena.Fighters.Select(f => f.GetComponent<PirateFighterAnimation>().ActionSerial).ToArray() };
        if (world != null)
        {
            state.players = runner.ActivePlayers.Count(); state.round = world.RoundNumber; state.started = world.Started;
            state.playing = world.Round.Playing; state.finished = world.Round.Finished; state.winner = world.Round.Winner;
            state.owners = Enumerable.Range(0, 6).Select(i => world.Owners[i].RawEncoded).ToArray();
            state.cannonPhases = Enumerable.Range(0, 2).Select(i => world.Cannons[i].Phase).ToArray(); state.sharkPhase = world.Shark.Phase; state.shark = world.Shark.Position;
            for (int i = 0; i < 6; i++) if (world.Owners[i] == runner.LocalPlayer) state.localSlot = i;
        }
        try { File.WriteAllText(Path.Combine(directory, "status.json"), JsonUtility.ToJson(state, true)); }
        catch (IOException) { } // The harness may briefly hold its snapshot open on Windows.
    }
}
#endif
