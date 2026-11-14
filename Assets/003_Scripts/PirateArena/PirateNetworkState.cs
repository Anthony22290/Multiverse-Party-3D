using Fusion;
using UnityEngine;

public struct PirateNetworkInput : INetworkInput
{
    public Vector2 Move;
    public float Yaw, LookPitch;
    public NetworkBool Grab, RightGrab, Lift;
    public int Jump, Push, Drop, Throw;
}

public struct PirateBodyState : INetworkStruct
{
    public Vector3 Position, Velocity, Grip, LeftGrip, RightGrip;
    public Quaternion Rotation;
    public int Flags, Action, ActionSerial;
    public float LookPitch;
}

public struct PirateHazardState : INetworkStruct
{
    public Vector3 Position, Target, Projectile;
    public Quaternion Rotation;
    public int Phase;
}

public struct PirateRoundState : INetworkStruct
{
    public NetworkBool Playing, Finished;
    public float Countdown, Elapsed;
    public int Winner;
}
