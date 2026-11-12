using UnityEngine;

namespace Evo.Tween
{
    /// <summary>
    /// Controls how a clip is placed relative to the current sequence group.
    /// </summary>
    public enum ClipFlow
    {
        Parallel = 0,
        Sequential = 1
    }

    /// <summary>
    /// Coordinate system used by Transform position and rotation modules.
    /// World and Local deliberately retain the old false/true serialized values.
    /// </summary>
    public enum CoordinateSpace
    {
        World = 0,
        Local = 1
    }

    /// <summary>
    /// Chooses which Unity player-loop phase advances a tween.
    /// </summary>
    public enum UpdatePhase
    {
        Update = 0,
        LateUpdate = 1,
        FixedUpdate = 2
    }

    /// <summary>
    /// Controls how a tween behaves after reaching the end of an iteration.
    /// </summary>
    public enum LoopMode
    {
        None = 0,
        Restart = 1,
        Yoyo = 2
    }

    /// <summary>
    /// Chooses whether playback is affected by <see cref="Time.timeScale"/>.
    /// </summary>
    public enum TimeMode
    {
        Scaled = 0,
        Unscaled = 1
    }

    /// <summary>
    /// Current playback state of a <see cref="TweenAnimator"/> component.
    /// </summary>
    public enum PlaybackState
    {
        Stopped = 0,
        Playing = 1,
        Paused = 2,
        Completed = 3
    }

    /// <summary>
    /// Optional inspector group for a serialized custom-module field.
    /// Unannotated module fields are automatically drawn in Settings.
    /// </summary>
    public enum ModuleFieldGroup
    {
        Values = 0,
        Settings = 1
    }

    /// <summary>
    /// Optional semantic role used by the generic editor for value controls.
    /// </summary>
    public enum ModuleFieldRole
    {
        None = 0,
        FromCurrent = 1,
        From = 2,
        To = 3
    }
}