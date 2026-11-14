using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Builds the Pirate Arena and its local/online session menu when its scene starts.</summary>
public sealed class PirateArenaBootstrap : MonoBehaviour
{
    public GameObject shipLarge, sharkModel, cannonModel, barrelModel, bucketModel;
    public GameObject anneModel, captainModel, henryModel, makoModel, skeletonModel;
    public GameObject rockModel, palmModel, chestModel, anchorModel;
    public PirateArenaSettings settings;
    public Material atlas, wood, darkWood, brass, rope, cloth, water, foam, warning, iron;
    public PhysicsMaterial fighterGrip, propGrip;
    public PirateArenaMap Map { get; private set; }

    private void Start()
    {
        if (settings == null) settings = ScriptableObject.CreateInstance<PirateArenaSettings>();
        GameObject oldCube = GameObject.Find("Cube");
        if (oldCube != null) Destroy(oldCube);
        Map = new PirateArenaMap(this);
        Map.Build();
        ConfigureLighting();
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Arena Camera", typeof(Camera), typeof(AudioListener));
            camera = cameraObject.GetComponent<Camera>();
            camera.tag = "MainCamera";
        }
        camera.fieldOfView = 55f;
        camera.nearClipPlane = 0.15f;
        camera.farClipPlane = 300f;
        camera.backgroundColor = new Color(0.38f, 0.72f, 0.83f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        PirateArenaDirector director = new GameObject("Pirate Arena Round").AddComponent<PirateArenaDirector>();
        new GameObject("Pirate Audio").AddComponent<PirateArenaAudio>();
        director.Configure(Map.Width, Map.Length, settings.deckHeight, camera, this);
        PirateArenaCamera follow = camera.GetComponent<PirateArenaCamera>();
        if (follow == null) follow = camera.gameObject.AddComponent<PirateArenaCamera>();
        follow.SetDirector(director);
        GameObject[] models = { anneModel, captainModel, henryModel, makoModel, skeletonModel, captainModel };
        // The party decides how many pirates board (4–6) and who they are; slot 0 is this computer's player.
        var roster = MultiverseParty.CurrentRoster();
        int crew = Mathf.Clamp(roster.Count, 2, models.Length);
        for (int i = 0; i < crew; i++)
        {
            float angle = Mathf.PI * 2f * i / models.Length;
            Vector3 spawn = new Vector3(Mathf.Cos(angle) * Map.Width * 0.26f,
                settings.deckHeight + 1.12f, Map.CenterCourtZ + Mathf.Sin(angle) * 3.6f);
            GameObject actor = new GameObject(i == 0 ? "Player - Anne" : "Bot - Pirate " + i);
            actor.transform.position = spawn;
            Rigidbody body = actor.AddComponent<Rigidbody>();
            body.mass = 58f;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations = 12;
            body.solverVelocityIterations = 6;
            body.maxAngularVelocity = 9f;
            body.maxDepenetrationVelocity = 3f;
            body.centerOfMass = new Vector3(0f, -0.25f, 0f);
            CapsuleCollider capsule = actor.AddComponent<CapsuleCollider>();
            capsule.height = 1.75f;
            capsule.radius = 0.38f;
            capsule.sharedMaterial = fighterGrip;
            PirateCameraObstacle.IgnoreFor(capsule);
            PirateFighter fighter = actor.AddComponent<PirateFighter>();
            GameObject[] byCharacter = { anneModel, captainModel, henryModel, makoModel, skeletonModel };
            var model = MultiverseParty.InMatch && i < roster.Count ? byCharacter[roster.Characters[i] % byCharacter.Length] : models[i];
            fighter.Initialize(director, i == 0, model, spawn, i);
            director.Register(fighter);
            fighter.SetControl(i == 0, roster.Names[Mathf.Min(i, roster.Count - 1)]);
        }

        // Movable props are harmless physics objects: no weapons or damage values.
        for (int side = -1; side <= 1; side += 2)
            for (int row = 0; row < 3; row++)
                CreateThrowable("Barril de carga", barrelModel,
                    new Vector3(side * (Map.Width * 0.5f - 1.5f), settings.deckHeight + 0.65f,
                        Map.CenterCourtZ - 3f + row * 3.1f), 18f, director);
        for (int i = 0; i < 2; i++)
            CreateThrowable("Cubo de pesca", bucketModel,
                new Vector3((i * 2 - 1) * 2f, settings.deckHeight + 0.65f, Map.CenterCourtZ + 1f), 9f, director);

        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 position = new Vector3(side * (Map.Width * 0.5f - 1.7f), Map.BowHeight, Map.BowCenterZ);
            GameObject cannon = new GameObject("Cañón automático " + side);
            cannon.transform.position = position;
            cannon.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var cannonVisual = PirateArenaVisuals.AddModel(cannonModel, cannon.transform, 1.65f, atlas, false);
            if (cannonVisual != null)
            {
                Bounds bounds = PirateArenaVisuals.BoundsOf(cannonVisual);
                var blocker = cannon.AddComponent<BoxCollider>();
                blocker.center = cannon.transform.InverseTransformPoint(bounds.center);
                blocker.size = bounds.size;
                cannon.AddComponent<PirateArenaSurface>();
            }
            cannon.AddComponent<CannonHazard>().Initialize(director, settings.deckHeight, Map.Width, Map.Length);
        }
        GameObject shark = new GameObject("Shark - overboard hazard");
        shark.transform.position = new Vector3(Map.Width * 0.5f + 5f, -0.25f, 0f);
        PirateArenaVisuals.AddModel(sharkModel, shark.transform, 2.4f, atlas, true);
        shark.AddComponent<SharkHazard>().Initialize(director, Map.Width, Map.Length, settings.deckHeight);
        director.BeginRound();
        new GameObject("Pirate Session").AddComponent<PirateSession>().Initialize(director);
        Debug.Log("Arena del Caos lista: barco de " + settings.shipLength + " m, 6 tripulantes y materiales asignados.");
    }

    private void CreateThrowable(string label, GameObject model, Vector3 position, float mass, PirateArenaDirector director)
    {
        GameObject root = new GameObject(label);
        root.transform.position = position;
        PirateArenaVisuals.AddModel(model, root.transform, 1.1f, atlas, true);
        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = mass;
        body.linearDamping = 0.12f;
        body.angularDamping = 0.4f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.solverIterations = 12;
        body.solverVelocityIterations = 6;
        body.maxDepenetrationVelocity = 3f;
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.height = 1.1f;
        collider.radius = 0.4f;
        collider.sharedMaterial = propGrip;
        PirateCameraObstacle.IgnoreFor(collider);
        GrabbableProp prop = root.AddComponent<GrabbableProp>();
        prop.Initialize();
        director.Register(prop);
    }

    private static void ConfigureLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.54f, 0.72f, 0.86f);
        RenderSettings.ambientEquatorColor = new Color(0.63f, 0.64f, 0.57f);
        RenderSettings.ambientGroundColor = new Color(0.23f, 0.27f, 0.29f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.38f, 0.72f, 0.83f);
        RenderSettings.fogStartDistance = 90f;
        RenderSettings.fogEndDistance = 230f;
        Light sun = Object.FindAnyObjectByType<Light>();
        if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.89f, 0.72f);
        sun.intensity = 1.35f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.65f;
        sun.shadowBias = 0.03f;
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
    }
}

