using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using System;

namespace UniverseSimulation
{
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
        private static int k_ActorCountLimit = 32;

        // The behaviour type of the actor
        [SerializeField] private ActorType m_ActorType = ActorType.Attractor;

        // Unit: Kilograms
        // The mass of the actor; used for Attractor and Repeller types
        [SerializeField] private double m_Mass = 1f;

        // The force of the actor; used for LinearForce type
        [SerializeField] private double m_Force = 1f;


        private static double m_SimulationUnitScale;
        private static Dictionary<UniverseActor, ActorData> s_ActorsDict = new Dictionary<UniverseActor, ActorData>();

        public static Dictionary<UniverseActor, ActorData> ActorsDict
        {
            get { return s_ActorsDict; }
            set { }
        }

        public static int Limit
        {
            get { return k_ActorCountLimit; }
            set { }
        }

        public delegate void OnDictUpdate();
        public static OnDictUpdate Delegate_OnDictUpdate = null;

        public static void SetScale(double scale)
        {
            m_SimulationUnitScale = scale;
        }

        private void OnValidate()
        {
            if (Delegate_OnDictUpdate != null)
                Delegate_OnDictUpdate.Invoke();
        }

        private void OnEnable()
        {
            var force = (m_ActorType == ActorType.LinearForce) ? transform.forward * (float)(m_Force * m_SimulationUnitScale) : Vector3.zero;
            var mass = (m_ActorType == ActorType.Attractor || m_ActorType == ActorType.Repeller) ? (float)(m_Mass * m_SimulationUnitScale) : 0f;

            // Mass is inverted for Repeller type
            mass *= (m_ActorType == ActorType.Repeller) ? -1f : 1f;

            var data = new ActorData()
            {
                Position = transform.position,
                Force = force,
                Mass = mass,
            };

            if (!s_ActorsDict.ContainsKey(this))
            {
                if (s_ActorsDict.Count >= k_ActorCountLimit)
                    Debug.LogWarning("UniverseActor couldn't be registered as the hard limit has been reached!", this);
                else
                    s_ActorsDict.Add(this, data);

                if (Delegate_OnDictUpdate != null)
                    Delegate_OnDictUpdate.Invoke();
            }
        }

        private void OnDisable()
        {
            if (s_ActorsDict.ContainsKey(this))
                s_ActorsDict.Remove(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);

            switch(m_ActorType)
            {
                case ActorType.Attractor:
                    Gizmos.DrawSphere(transform.position, 25f);
                    break;

                case ActorType.Repeller:
                    Gizmos.DrawSphere(transform.position, 25f);
                    break;

                case ActorType.LinearForce:
                    Gizmos.DrawSphere(transform.position, 5f);
                    Gizmos.DrawRay(transform.position, transform.forward * 50f);
                    break;
            }
        }
    }
}