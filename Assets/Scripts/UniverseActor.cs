using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using System;

namespace UniverseSimulation
{
    public delegate void DictUpdated();

    public class UniverseActor : MonoBehaviour
    {
        public enum ActorType
        {
            Attractor,
            Repeller,
            LinearForce,
        }

        public struct ActorData
        {
            public Vector3 Position;
            public Vector3 Force;
            public float Mass;
        }

        // A hard limit on the number of supported UniverseActors
        private static int k_ActorCountLimit = 16;

        // The behaviour type of the actor
        [SerializeField] private ActorType m_ActorType = ActorType.Attractor;

        // The mass of the actor; used for Attractor and Repeller types
        [SerializeField] private float m_Mass = 1f;

        // The force of the actor; used for LinearForce type
        [SerializeField] private float m_Force = 1f;


        private static Dictionary<UniverseActor, ActorData> m_ActorsDict = new Dictionary<UniverseActor, ActorData>();

        public static Dictionary<UniverseActor, ActorData> ActorsDict
        {
            get { return m_ActorsDict; }
            set { }
        }

        public static int Limit
        {
            get { return k_ActorCountLimit; }
            set { }
        }

        public static DictUpdated OnDictUpdate;

        private void OnEnable()
        {
            var force = (m_ActorType == ActorType.LinearForce) ? transform.forward * m_Force : Vector3.zero;
            var mass = (m_ActorType == ActorType.Attractor || m_ActorType == ActorType.Repeller) ? m_Mass : 0f;

            // Mass is inverted for Repeller type
            mass *= (m_ActorType == ActorType.Repeller) ? -1f : 1f;

            var data = new ActorData()
            {
                Position = transform.position,
                Force = force,
                Mass = mass,
            };

            if (!m_ActorsDict.ContainsKey(this))
            {
                if (m_ActorsDict.Count >= k_ActorCountLimit)
                    Debug.LogWarning("UniverseActor couldn't be registered as the hard limit has been reached!", this);
                else
                    m_ActorsDict.Add(this, data);

                OnDictUpdate();
            }
        }

        private void OnDisable()
        {
            if (m_ActorsDict.ContainsKey(this))
                m_ActorsDict.Remove(this);
        }
    }
}