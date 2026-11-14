using UnityEngine;

[CreateAssetMenu(menuName = "Multiverse/Pirate Arena Settings")]
public sealed class PirateArenaSettings : ScriptableObject
{
    [Header("Ship dimensions")]
    [Range(32f, 56f)] public float shipLength = 44f;
    [Range(1f, 1.4f)] public float beamMultiplier = 1.2f;
    public float deckHeight = 3f;
    [Header("Movement and balance")]
    public float moveSpeed = 5.6f;
    public float acceleration = 18f;
    public float airControl = 0.22f;
    public float jumpSpeed = 6.3f;
    public float uprightSpring = 52f;
    public float uprightDamping = 10f;
    [Min(1f)] public float maxBalanceAcceleration = 90f;
    [Min(0.1f)] public float standUpBlendSeconds = 0.45f;
    public float pushSpeed = 6.8f;
    public float pushCooldown = 0.75f;
    [Tooltip("Extra hit strength from a running start (0.3 = up to +30%).")]
    [Range(0f, 1f)] public float pushRunUpBonus = 0.3f;
    [Tooltip("Velocity the attacker loses when a hit lands (m/s at standstill).")]
    [Range(0f, 3f)] public float pushRecoil = 0.9f;
    [Tooltip("Original soft-toy crew look (PirateCrewStyle). Off = the imported pirate models.")]
    public bool softCrew = true;
    [Header("Brawler combat (arena balance, not reference-game constants)")]
    [Min(1f)] public float knockoutThreshold = 3f;
    [Min(0f)] public float hitRecoveryDelay = 2.5f;
    [Min(0f)] public float hitRecoveryPerSecond = 0.65f;
    [Min(0.1f)] public float knockoutSeconds = 2.4f;
    [Min(0f)] public float recoveryProtectionSeconds = 0.9f;
    [Min(0.1f)] public float gripEnduranceSeconds = 8f;
    [Min(0f)] public float gripRecoveryPerSecond = 0.3f;
    [Header("Local bots")]
    [Min(0.5f)] public float botCarrySeconds = 4.5f;
    [Min(0.1f)] public float botEscapeInterval = 0.65f;
    [Header("Grabbing and throwing")]
    public float grabReach = 1.9f;
    public float carrySpring = 1800f;
    public float carryDamping = 170f;
    public float throwSpeed = 9.5f;
    public float throwUpSpeed = 4.8f;
    public float maxCarrySeconds = 3.8f;
    public Vector3 carryAnchor = new Vector3(0f, 0.08f, 0.75f);
    public Vector3 liftAnchor = new Vector3(0f, 0.78f, 0.35f);
    [Range(0f, 6f)] public float carryBalance = 3f;
    [Header("Cannon")]
    public float cannonWarningSeconds = 2.2f;
    public float cannonRadius = 3.4f;
    public float cannonMinInterval = 7f;
    public float cannonMaxInterval = 12f;
}
