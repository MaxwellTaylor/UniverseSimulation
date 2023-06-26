using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UniverseSimulation
{
    #region STRUCTS
    public struct ParticleData
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Mass;
        public float Entropy;
    }

    public struct ActorData
    {
        public Vector3 Position;
        public Vector3 Force;
        public float Mass;
    }
    #endregion

    #region ENUMS
    public enum ActorType
    {
        Attractor,
        Repeller,
        LinearForce,
    }

    public enum RenderTopology
    {
        Points,
        Lines,
        Tetrahedrons,
    }

    public enum InitShape
    {
        Sphere,
        AccretionDisc,
    }

    public enum ParticleCount
    {
        _4096 = 4096,
        _8192 = 8192,
        _16384 = 16384,
        _32768 = 32768,
        _65536 = 65536,
        _131072 = 131072,
        _262144 = 262144,
    }
    #endregion
}