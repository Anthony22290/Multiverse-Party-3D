using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

// Checks the 2026-11-10 Carreras Locas additions. Run with the race scene freshly in Play.
public static class RaceImprovementsPlaytest
{
    static readonly List<string> results = new List<string>();
    static void Check(bool ok, string s) => results.Add((ok ? "PASS " : "FAIL ") + s);
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static void Set(object o, string field, object v) => o.GetType().GetField(field, Any).SetValue(o, v);
    static async Task WaitUntil(Func<bool> condition, float timeout = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeout);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(15);
    }
    static async Task Wait(float seconds) { float end = Time.time + seconds; await WaitUntil(() => Time.time >= end, seconds * 4 + 5); }
    static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/Claude_Capturas/Carreras/" + name + ".png"));

    public static async Task<string[]> Run()
    {
        results.Clear(); Directory.CreateDirectory("Docs/Claude_Capturas/Carreras");
        var d = Object.FindAnyObjectByType<CrazyKartDirector>();
        if (!Application.isPlaying || d == null) throw new Exception("Race must be playing");
        if (d.Countdown < 3)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            await Task.Delay(300);
            await WaitUntil(() => (d = Object.FindAnyObjectByType<CrazyKartDirector>()) != null && d.Karts.Count > 0 && d.Countdown > 3);
        }
        var player = d.Karts.First(k => k.IsHuman); var bots = d.Karts.Where(k => !k.IsHuman).ToArray();
        var body = player.GetComponent<Rigidbody>();
        var bg = InputSystem.settings.backgroundBehavior; var routing = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        float savedLap = PlayerPrefs.GetFloat(CrazyKartDirector.LapKey, -1), savedRace = PlayerPrefs.GetFloat(CrazyKartDirector.RaceKey, -1);
        try
        {
            Check(d.GetComponent<CrazyRaceAudio>() != null, "Race audio component added");
            Check(Resources.Load<AudioClip>("RaceAudio/music") != null && Resources.Load<AudioClip>("RaceAudio/go") != null, "Music and cue clips load from Resources");
            // 1. Perfect start: press W when the countdown reaches 1.
            await WaitUntil(() => d.Countdown <= 2.6f); await Task.Delay(100); Capture("01_Cuenta_atras");
            await WaitUntil(() => d.Countdown <= 1.2f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            await WaitUntil(() => d.Playing); await Wait(.1f);
            Check(d.NaturalStart && player.StartResult == 1 && player.Boosting, "W on the 1 gives a perfect start boost (result=" + player.StartResult + ")");
            await Wait(.4f); Capture("02_Salida_perfecta");
            int botStarts = bots.Count(b => b.StartResult == 1);
            results.Add("INFO bots with perfect start: " + botStarts + "/" + bots.Length);
            await Wait(2f);
            // 2. Stall: pressing during the 3 stalls the kart briefly.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            player.Recover(); await Wait(.6f);
            Set(d, "countdown", 2.6f); Set(d, "<NaturalStart>k__BackingField", false);
            Set(player, "startResolved", false); Set(player, "pressedTooEarly", false); Set(player, "throttlePressedAt", -1f);
            Set(player, "<StartResult>k__BackingField", 0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            await WaitUntil(() => d.Playing); await Wait(.5f);
            Check(player.StartResult == -1 && player.Speed < 3, "Pressing too early stalls (result=" + player.StartResult + ", speed=" + player.Speed.ToString("F1") + ")");
            await Wait(1f);
            Check(player.Speed > 5, "Stall ends and the kart accelerates (speed=" + player.Speed.ToString("F1") + ")");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            // 3. Pause.
            d.SetPaused(true); await Task.Delay(400);
            var pausePanel = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(r => r.name == "Pausa");
            Check(Time.timeScale == 0 && pausePanel != null && pausePanel.gameObject.activeInHierarchy, "Pause freezes time and shows the menu");
            Capture("03_Pausa"); await Task.Delay(1500);
            d.SetPaused(false); await Task.Delay(100);
            Check(Time.timeScale == 1 && !pausePanel.gameObject.activeInHierarchy && !AudioListener.pause, "Continue restores time, audio and hides the menu");
            // 4. Item odds by position.
            int saveLap = player.CurrentLap; var botLaps = bots.Select(b => b.CurrentLap).ToArray();
            player.CurrentLap = 9; foreach (var b in bots) b.CurrentLap = 1;
            var leader = Enumerable.Range(0, 3000).Select(_ => d.RollItem(player)).ToArray();
            player.CurrentLap = 0; foreach (var b in bots) b.CurrentLap = 9;
            var last = Enumerable.Range(0, 3000).Select(_ => d.RollItem(player)).ToArray();
            player.CurrentLap = saveLap; for (int i = 0; i < bots.Length; i++) bots[i].CurrentLap = botLaps[i];
            float Share(int[] a, int item) => a.Count(x => x == item) / (float)a.Length;
            Check(Share(leader, 3) > .35f && Share(last, 1) + Share(last, 5) > .4f && Share(last, 3) < .1f && Share(leader, 6) == 0 && Share(last, 6) > .03f,
                "Leader gets traps, last gets turbos and storms: leader trap=" + Share(leader, 3).ToString("P0") + " last turbo+triple=" + (Share(last, 1) + Share(last, 5)).ToString("P0") + " last trap=" + Share(last, 3).ToString("P0") + " last storm=" + Share(last, 6).ToString("P0"));
            // Roulette, triple turbo and storm.
            typeof(CrazyKartController).GetField("<HeldItem>k__BackingField", Any).SetValue(player, 0);
            player.GiveItem(5, true); player.UseItem();
            Check(player.HeldItem == 5 && player.ItemCharges == 3 && player.RouletteSpinning, "Roulette blocks use while spinning");
            await Wait(1.2f); player.UseItem(); Check(player.Boosting && player.ItemCharges == 2 && player.ItemName.Contains("x2"), "Triple turbo spends one charge at a time");
            await Wait(.2f); player.UseItem(); await Wait(.2f); player.UseItem(); Check(player.HeldItem == 0, "Triple turbo empties after three uses");
            // 5. Homing pulse: rival 18 m ahead and 3.5 m to the side (a straight shot would miss).
            Set(d, "countdown", 100f);
            player.CurrentCheckpoint = 0; player.Recover(); await Wait(.3f);
            var rival = bots[0]; var rb = rival.GetComponent<Rigidbody>();
            Set(rival, "shieldUntil", 0f); Set(rival, "stunUntil", 0f); Set(rival, "hitProtectionUntil", 0f);
            rb.position = body.position + player.transform.forward * 18 + player.transform.right * 3.5f; rb.rotation = body.rotation;
            await Wait(.2f);
            Check(CrazyKartHazard.FindTarget(player) == rival, "Pulse acquires the rival ahead");
            CrazyKartHazard.Spawn(player, false);
            await WaitUntil(() => rival.Stunned, 3);
            Check(rival.Stunned, "Homing pulse reaches an off-axis rival");
            // 6. Lap record saved for humans only.
            PlayerPrefs.DeleteKey(CrazyKartDirector.LapKey);
            d.ReportLap(rival, 20); Check(d.RecordLap == 0, "Bot laps never set records");
            d.ReportLap(player, 31.5f);
            Check(d.NewLapRecord && Mathf.Approximately(d.RecordLap, 31.5f) && Time.time - player.LapRecordTime < 1, "Human lap sets and saves a lap record");
            d.ReportLap(player, 40); Check(Mathf.Approximately(d.RecordLap, 31.5f), "Slower lap keeps the record");
            // 7. Finish with rivals still racing: projected times.
            Set(d, "countdown", 0f); await Wait(2.5f);
            Set(player, "<FinishTime>k__BackingField", d.RoundTime); Set(player, "<HasFinished>k__BackingField", true);
            d.Finish(player); await Wait(.6f);
            Check(d.Finished && bots.All(b => b.HasFinished || b.EstimatedFinish > d.RoundTime), "Unfinished rivals get projected times: " + string.Join(", ", bots.Select(b => b.EstimatedFinish.ToString("F0"))));
            Capture("04_Resultados_estimados"); await Task.Delay(1200);
        }
        finally
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = bg; InputSystem.settings.editorInputBehaviorInPlayMode = routing;
            if (savedLap < 0) PlayerPrefs.DeleteKey(CrazyKartDirector.LapKey); else PlayerPrefs.SetFloat(CrazyKartDirector.LapKey, savedLap);
            if (savedRace < 0) PlayerPrefs.DeleteKey(CrazyKartDirector.RaceKey); else PlayerPrefs.SetFloat(CrazyKartDirector.RaceKey, savedRace);
            PlayerPrefs.Save(); Time.timeScale = 1; AudioListener.pause = false;
        }
        File.WriteAllLines("Docs/Claude_Pruebas/Carreras_Mejoras_2026-11-10.txt", results);
        return results.ToArray();
    }

    // Full three-lap race with every kart driven by the AI, measuring the new systems.
    public static async Task<string> FullRace()
    {
        var d = Object.FindAnyObjectByType<CrazyKartDirector>();
        if (!Application.isPlaying || d == null) throw new Exception("Play race");
        foreach (var k in d.Karts) k.IsHuman = false;
        Time.timeScale = 3; float maxSpeed = 0; var slips = new Dictionary<CrazyKartController, int>(); var lastSlip = new Dictionary<CrazyKartController, float>();
        var hits = 0; var stunned = d.Karts.ToDictionary(k => k, k => false);
        foreach (var k in d.Karts) { slips[k] = 0; lastSlip[k] = -100; }
        var lastPos = d.Karts.ToDictionary(k => k, k => k.transform.position); var lastRec = d.Karts.ToDictionary(k => k, k => k.LastRecoveryTime); var falls = new List<string>();
        var deadline = DateTime.UtcNow.AddSeconds(150);
        try
        {
            while (!d.Finished && DateTime.UtcNow < deadline)
            {
                foreach (var k in d.Karts)
                {
                    maxSpeed = Mathf.Max(maxSpeed, k.Speed);
                    if (k.LastSlipstreamBoost > lastSlip[k]) { lastSlip[k] = k.LastSlipstreamBoost; slips[k]++; }
                    if (k.Stunned && !stunned[k]) hits++; stunned[k] = k.Stunned;
                    if (k.LastRecoveryTime > lastRec[k]) { lastRec[k] = k.LastRecoveryTime; falls.Add(k.DisplayName + "@" + lastPos[k].ToString("F0") + " cp" + k.CurrentCheckpoint); }
                    lastPos[k] = k.transform.position;
                }
                await Task.Delay(30);
            }
            await Task.Delay(300); Time.timeScale = 1; await Task.Delay(1200);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/Claude_Capturas/Carreras/05_Resultados_carrera.png"));
            var recovery = d.Karts.Sum(k => k.GetComponent<CrazyKartRecovery>().AutomaticRecoveries);
            string s = (d.Finished && d.Finishers.Count == d.Karts.Count ? "PASS" : "FAIL") + " Full AI race, finishers=" + d.Finishers.Count + "/" + d.Karts.Count + ", time=" + d.RoundTime.ToString("F1") +
                ", maxSpeed=" + maxSpeed.ToString("F1") + ", impacts=" + hits + ", autoRecoveries=" + recovery + ", slipstreamBoosts=" + slips.Values.Sum() + "\n" +
                "recoveries: " + string.Join(" | ", falls) + "\n" + string.Join("\n", d.Karts.OrderBy(k => d.Position(k)).Select(k => d.Position(k) + ". " + k.DisplayName + " finish=" + k.FinishTime.ToString("F1") + " bestLap=" + k.BestLapTime.ToString("F1") + " start=" + k.StartResult + " slips=" + slips[k]));
            File.WriteAllText("Docs/Claude_Pruebas/Carreras_CarreraCompleta_" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "_2026-11-10.txt", s);
            return s;
        }
        finally { Time.timeScale = 1; }
    }
}
