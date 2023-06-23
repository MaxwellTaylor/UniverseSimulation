using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using System;

public class UniverseActor : MonoBehaviour
{
    private enum ActorType
    {
        Attractor,
        Repeller,
        LinearForce,
    }

    [SerializeField] private ActorType m_ActorType = ActorType.Attractor;

    [SerializeField] private float m_ActorStrength = 1f;

}