using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Pruebas de pasos de audio y físicas de los tripulantes: estabilidad, empujes, agarres y colisiones.</summary>
public static class PiratePhysicsPlaytest
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> results = new List<string>();
    static void Set(object obj, string key, object value) => obj.GetType().GetField(key, Flags).SetValue(obj, value);
    static object Get(object obj, string key) => obj.GetType().GetField(key, Flags).GetValue(obj);
    static void Check(bool pass, string text)
    {
        results.Add((pass ? "PASS " : "FAIL ") + text);
        if (!pass) throw new Exception(text);
    }
    static async Task Wait(float seconds) { float end = Time.time + seconds; while (Time.time < end) await Task.Delay(25); }
    static List<AudioSource> stepVoices;
    static void SampleSteps()
    {
        stepVoices = new List<AudioSource>();
        var audio = Object.FindAnyObjectByType<PirateArenaAudio>();
        if (audio != null)
            foreach (Transform child in audio.transform)
            {
                var voice = child.GetComponent<AudioSource>();
                if (voice.clip != null && voice.clip.name.StartsWith("step_")) stepVoices.Add(voice);
            }
    }
    static void Place(PirateFighter f, Vector3 position)
    {
        f.Respawn();
        f.Body.interpolation = RigidbodyInterpolation.None;
        f.transform.SetPositionAndRotation(position, Quaternion.identity);
        f.Body.position = position; f.Body.rotation = Quaternion.identity;
        f.Body.linearVelocity = Vector3.zero; f.Body.angularVelocity = Vector3.zero;
        Set(f, "botThink", 100000f);
    }

    public static async Task<string[]> Run()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Entra en Play primero.");
        results.Clear();
        var d = Object.FindAnyObjectByType<PirateArenaDirector>();
        if (d == null) throw new Exception("No hay PirateArenaDirector en la escena activa.");
        var cannons = Object.FindObjectsByType<CannonHazard>();
        var sharks = Object.FindObjectsByType<SharkHazard>();
        try
        {
            d.enabled = false; d.SendMessage("RestartRound");
            Set(d, "<IsPlaying>k__BackingField", true);
            foreach (var hazard in cannons) hazard.enabled = false;
            foreach (var hazard in sharks) hazard.enabled = false;
            foreach (var f in d.Fighters) { f.enabled = false; f.Eliminate(); }
            foreach (var f in d.Fighters) { f.enabled = true; }
            PirateFighter A = d.Fighters[0], B = d.Fighters[1], C = d.Fighters[2], W = d.Fighters[3], E = d.Fighters[4];
            float deck = d.DeckY;
            float half = d.Art.Map.Width * 0.5f;
            float court = d.Art.Map.CenterCourtZ;
            var settings = d.Settings;

            // --- Audio: ocho pasos CC0 de Kenney (recursos) ---
            string[] names = { "step_wood_000", "step_wood_001", "step_wood_002", "step_wood_003", "step_wood_004",
                               "step_stone_000", "step_stone_001", "step_stone_002" };
            int loaded = names.Count(n => Resources.Load<AudioClip>("PirateAudio/" + n) != null);
            Check(loaded == 8, "Los ocho clips CC0 de pasos cargan desde Resources (" + loaded + "/8)");

            var pickFloor = typeof(PirateFighterAudio).GetMethod("PickFloor", Flags | BindingFlags.Static);
            int floorOnDeck = Convert.ToInt32(pickFloor.Invoke(null, new object[] { new Vector3(0f, deck + 0.9f, court) }));
            Check(floorOnDeck == 0, "La cubierta se detecta como suelo de madera");

            Place(B, new Vector3(-2f, deck + 0.95f, court));
            var motion = B.GetComponent<PirateFighterAnimation>();
            Set(motion, "nextStep", 0f);
            Set(B, "move", Vector3.right);
            await Wait(1.0f);
            SampleSteps();
            Check(stepVoices.Count > 0, "Al andar se reproducen pasos integrados en una voz SFX");
            Check(stepVoices.All(v => v.clip != null && v.clip.name.StartsWith("step_wood")),
                "Los pasos sobre tablones salen del pool de madera");
            Set(B, "move", Vector3.zero);

            // --- Estabilidad ---
            Place(A, new Vector3(0f, deck + 0.95f, court));
            await Wait(2.2f);
            Vector3 drift = A.Body.position - new Vector3(0f, deck + 0.825f, court);
            Check(Mathf.Abs(drift.x) < 0.25f && Mathf.Abs(drift.z) < 0.25f && A.Body.linearVelocity.magnitude < 0.7f,
                "Un tripulante quieto no desliza en cubierta (desplazamiento " + drift.magnitude.ToString("F2") + " m)");
            Check(A.transform.up.y > 0.95f, "Un tripulante quieto se mantiene derecho");

            Place(A, new Vector3(0f, deck + 0.95f, court));
            await Wait(0.3f);
            Set(A, "lastGrounded", Time.time);
            Set(A, "jumpBufferedUntil", Time.time + 0.25f);
            await Wait(0.45f);
            Check(A.Body.position.y > deck + 1.6f, "Salto alcanza altura sobre la cubierta");
            await Wait(0.9f);

            Place(A, new Vector3(0f, deck + 4.4f, court));
            await Wait(1.4f);
            Check(A.IsAlive && A.Body.position.y > deck + 0.75f && A.transform.up.y > 0.8f,
                "Caida fuerte mantiene el cuerpo sobre la cubierta y se recupera erguido");

            // --- Empujes ---
            Place(A, new Vector3(-0.85f, deck + 0.95f, court));
            Place(B, new Vector3(0.85f, deck + 0.95f, court));
            await Wait(0.35f);
            Place(A, new Vector3(-0.85f, deck + 0.95f, court));
            Set(A, "facing", Vector3.right);
            Set(A, "pushPressed", true);
            await Wait(0.2f);
            Check(B.Body.linearVelocity.magnitude > settings.pushSpeed - 2f,
                "El empuje traslada impulso al objetivo (v=" + B.Body.linearVelocity.magnitude.ToString("F2") + ")");
            Check(B.IsStunned, "El objetivo empujado queda aturdido");
            Vector3 pushed = B.Body.position;
            await Wait(0.45f);
            Check((B.Body.position - pushed).magnitude > 0.8f, "El empujado recorre distancia measurable");

            Place(E, new Vector3(0f, deck + 0.95f, court - 8f));
            await Wait(0.3f);
            E.TakePush(Vector3.right * 40f);
            await Task.Delay(60);
            Check(E.Body.linearVelocity.magnitude <= 19f,
                "Un empujón absurdamente grande se limita (v=" + E.Body.linearVelocity.magnitude.ToString("F2") + ")");

            // Un empujón sobre un capturado lo libera de su portador (lejos de los cubos: TryGrab agarra lo más cercano)
            Place(C, new Vector3(-1.4f, deck + 0.95f, court - 4.5f));
            Place(W, new Vector3(-0.1f, deck + 0.95f, court - 4.5f));
            await Wait(0.3f);
            Set(C, "facing", Vector3.right);
            Set(C, "grabPressed", true);
            Set(C, "botThrowAt", Time.time + 30f);
            await Wait(0.25f);
            Check(Get(W, "holder") == C, "El agarre de otro tripulante se crea");
            Place(E, new Vector3(0.9f, deck + 0.95f, court - 4.5f));
            await Wait(0.3f);
            Set(E, "facing", Vector3.left);
            Set(E, "pushPressed", true);
            await Wait(0.25f);
            Check(Get(C, "holder") == null && Get(W, "holder") == null && !C.IsGrabbing,
                "Un empujón sobre el capturado lo libera de su portador");

            // --- Agarres ---
            Place(C, new Vector3(-1.4f, deck + 0.95f, court - 4.5f));
            Place(W, new Vector3(-0.1f, deck + 0.95f, court - 4.5f));
            await Wait(0.3f);
            Set(C, "facing", Vector3.right);
            Set(C, "grabPressed", true);
            Set(C, "botThrowAt", Time.time + 30f);
            await Wait(0.3f);
            Check(Get(W, "holder") == C && C.IsGrabbing && W.IsHeld, "Agarre directo con unión");
            Set(C, "lifting", true);
            await Wait(2.0f);
            Check(W.Body.position.y > deck + 1.25f,
                "Levantar mantiene al capturado en volandas (y=" + W.Body.position.y.ToString("F2") + ")");
            Set(W, "escapeTaps", 0);
            float escapeStart = Time.time;
            bool escaped = false;
            while (Time.time - escapeStart < 4f)
            {
                Set(W, "botThink", 0f);
                if (Get(C, "holder") == null) { escaped = true; break; }
                await Task.Delay(40);
            }
            Set(C, "lifting", false);
            Check(escaped, "El capturado escapa con tres toques de espacio");
            await Wait(0.5f);

            // Lanzamiento del barril
            var barrel = Object.FindObjectsByType<GrabbableProp>().First(p => p.name.Contains("Barril"));
            var barrelBody = barrel.GetComponent<Rigidbody>();
            Place(C, new Vector3(barrelBody.position.x - 1.3f, deck + 0.95f, barrelBody.position.z));
            await Wait(0.3f);
            Set(C, "facing", barrelBody.position.x < 0f ? Vector3.right : Vector3.left);
            Set(C, "grabPressed", true);
            Set(C, "botThrowAt", Time.time + 30f);
            await Wait(0.3f);
            Check(Get(barrel, "Holder") == C, "Se agarran los barriles de carga");
            Set(C, "grabReleased", true);
            await Wait(0.25f);
            Vector3 propVel = barrelBody.linearVelocity;
            float planar = new Vector3(propVel.x, 0f, propVel.z).magnitude;
            Check(planar >= 6f && Vector3.Dot(new Vector3(propVel.x, 0f, propVel.z).normalized, Vector3.right) > 0.9f,
                "El lanzamiento sale en la dirección del personaje (v=" + propVel.magnitude.ToString("F2") + ")");

            // Caducidad de la carga (se adelanta grabStarted en vez de esperar 3,8 s)
            barrelBody.linearVelocity = Vector3.zero; barrelBody.angularVelocity = Vector3.zero;
            barrelBody.position = new Vector3(2f, deck + 0.65f, court + 5f);
            Place(C, new Vector3(0.7f, deck + 0.95f, court + 5f));
            await Wait(0.3f);
            Set(C, "facing", Vector3.right);
            Set(C, "grabPressed", true);
            Set(C, "botThrowAt", Time.time + 30f);
            await Wait(0.3f);
            Check(C.IsGrabbing, "Segundo agarre del barril se crea");
            Set(C, "grabStarted", Time.time - (settings.maxCarrySeconds + 0.5f));
            await Wait(0.6f);
            Check(!C.IsGrabbing, "El agarre caduca tras maxCarrySeconds");

            // Borda: colgar y remontar con espacio
            Collider rail = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c.name == "Pasamanos" &&
                    Mathf.Abs(c.transform.position.z - court) < 6f && Mathf.Abs(c.transform.position.x) > half - 0.4f);
            if (rail != null)
            {
                Vector3 railCenter = rail.transform.position;
                float side = Mathf.Sign(railCenter.x);
                Place(W, new Vector3(railCenter.x + side * 0.55f, deck + 0.55f, railCenter.z));
                await Wait(0.25f);
                Set(W, "facing", Vector3.left * side);
                Set(W, "grabPressed", true);
                await Wait(0.4f);
                Check(W.IsHoldingLedge, "Colgar de una borda tras caer fuera del barco");
                Set(W, "jumpBufferedUntil", Time.time + 0.3f);
                await Wait(2.0f);
                Check(!W.IsHoldingLedge && W.Body.position.y > deck + 0.7f && Mathf.Abs(W.Body.position.x) < half - 0.1f,
                    "El salto desde la borda remonta a la cubierta");
            }
            else results.Add("OBS No se encontró un pasamanos contiguo para la prueba de borda");

            // Choque de carrera entre dos tripulantes: control, no aturdimiento masivo
            Place(A, new Vector3(-2.3f, deck + 0.95f, court - 6f));
            Place(B, new Vector3(2.3f, deck + 0.95f, court - 6f));
            await Wait(0.3f);
            Set(A, "move", Vector3.right);
            Set(B, "move", Vector3.left);
            await Wait(1.1f);
            Check(!A.IsStunned && !B.IsStunned, "Un choque de carrera entre tripulantes no provoca aturdimiento");
            Check(A.IsAlive && B.IsAlive, "Ningún tripulante muere por el choque");

            foreach (var hazard in cannons) hazard.enabled = true;
            foreach (var hazard in sharks) hazard.enabled = true;
            d.SendMessage("RestartRound");
            d.enabled = true;
            File.WriteAllLines("Temp/AnimationChecks/physics-results.txt", results);
            return results.ToArray();
        }
        catch (Exception e)
        {
            results.Add(e.ToString());
            File.WriteAllLines("Temp/AnimationChecks/physics-results.txt", results);
            throw;
        }
    }
}
