using UnityEngine;
using MallHorror.Core;

namespace MallHorror.Controls
{
    /// <summary>
    /// Everything a player "asks for" in one frame. Movement code only ever reads this struct,
    /// never the keyboard directly. That is what lets the same movement code later run for
    /// remote LAN players (their frames arrive over the network) without being rewritten.
    /// </summary>
    public struct PlayerInputFrame
    {
        public Vector2 move;          // x = strafe, y = forward/back, length <= 1
        public Vector2 lookDelta;     // degrees this frame: x = yaw, y = pitch (up positive)
        public bool sprint;
        public Stance desiredStance;
        public bool interactHeld;
        public bool interactPressed;
        public bool dropPressed;
    }

    public interface IPlayerInputSource
    {
        PlayerInputFrame ReadFrame(float deltaTime);
    }
}
