using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

/// <summary>
/// "SALAS ABIERTAS" (2026-11-21): connects a spare runner to the Photon lobby of the game (same region and version as
/// the rooms, see <see cref="MultiverseParty.StartOnline"/>) and keeps the list of visible rooms up to date, so friends
/// can join without typing the code. Rooms are visible unless the host marked them as private in the menu.
/// </summary>
public sealed class PartyRoomBrowser : MonoBehaviour
{
    public struct Room
    {
        public string Code, Host;
        public int Players, Size;
        public bool Playing, Open;
    }

    public readonly List<Room> Rooms = new List<Room>();
    public string Status { get; private set; } = "";
    public bool Connected { get; private set; }
    public event Action Changed;

    NetworkRunner runner;
    bool starting, closing;

    public static PartyRoomBrowser Open()
    {
        var b = new GameObject("Buscador de salas").AddComponent<PartyRoomBrowser>();
        _ = b.Begin();
        return b;
    }

    async Task Begin()
    {
        if (starting) return;
        starting = true; Status = "Buscando salas…"; Changed?.Invoke();
        try
        {
            runner = gameObject.AddComponent<NetworkRunner>();
            runner.ProvideInput = false;
            var callbacks = new NetworkDelegates();
            callbacks.OnSessionListUpdated = (r, list) => { if (!closing) OnList(list); };
            runner.AddCallbacks(callbacks);
            var settings = MultiverseParty.OnlineSettings();
            var result = await runner.JoinSessionLobby(SessionLobby.ClientServer, null, null, settings);
            if (closing) return;
            Connected = result.Ok;
            Status = result.Ok ? "Buscando salas…" : "No se pudo buscar salas (" + result.ShutdownReason + ").";
        }
        catch (Exception e)
        {
            Status = "No se pudo buscar salas: " + e.Message;
            Debug.LogWarning("PartyRoomBrowser: " + e);
        }
        Changed?.Invoke();
    }

    void OnList(List<SessionInfo> list)
    {
        Rooms.Clear();
        foreach (var s in list)
        {
            if (s == null || !s.IsValid || !s.IsVisible) continue;
            var room = new Room { Code = s.Name, Players = s.PlayerCount, Size = s.MaxPlayers, Open = s.IsOpen, Host = "?" };
            if (s.Properties != null)
            {
                if (s.Properties.TryGetValue(MultiverseParty.RoomHostKey, out var h) && h != null && h.IsString) room.Host = (string)h;
                if (s.Properties.TryGetValue(MultiverseParty.RoomStateKey, out var e) && e != null && e.IsInt) room.Playing = (int)e == 1;
                if (s.Properties.TryGetValue(MultiverseParty.RoomSizeKey, out var t) && t != null && t.IsInt) room.Size = (int)t;
            }
            Rooms.Add(room);
        }
        Rooms.Sort((a, b) => a.Playing != b.Playing ? (a.Playing ? 1 : -1) : string.CompareOrdinal(a.Code, b.Code));
        Status = Rooms.Count == 0 ? "No hay salas abiertas ahora mismo. Crea una o únete con código." : Rooms.Count + (Rooms.Count == 1 ? " sala abierta" : " salas abiertas");
        Changed?.Invoke();
    }

    /// <summary>Leave the lobby (before joining a room, or when the list is closed).</summary>
    public async Task Close()
    {
        if (closing) return;
        closing = true; Changed = null;
        var r = runner; runner = null;
        if (r != null) { try { await r.Shutdown(); } catch (Exception) { } }
        if (this != null) Destroy(gameObject);
    }

    void OnDestroy()
    {
        closing = true;
        if (runner != null) { try { _ = runner.Shutdown(); } catch (Exception) { } runner = null; }
    }
}
