using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using System;

namespace UniverseSimulation
{
    public class UniverseActor : MonoBehaviour
    {
        #region PUBLIC VARIABLES
        public delegate void OnDictUpdate();
        public static OnDictUpdate Delegate_OnDictUpdate = null;

        [Header("Behaviour type of UniverseActor.")]
        public ActorType ActorType = ActorType.Attractor;

        [Header("Force of UniverseActor. Used for LinearForce type.")]
        public MeasurementContainer Force = new MeasurementContainer(3.52e16, MeasurementUnits.Force_Meganewtons);

        [Header("Mass of UniverseActor. Used for Attractor and Repeller types.")]
        public MeasurementContainer Mass = new MeasurementContainer(1, MeasurementUnits.Mass_SolarMasses);
        #endregion

        #region PRIVATE VARIABLES
        private static Dictionary<UniverseActor, ActorData> s_ActorDataDict = new Dictionary<UniverseActor, ActorData>();

        // A hard limit on the number of supported UniverseActors
        private const int k_ActorCountLimit = 4;
        #endregion

        #region PROPERTIES
        public static Dictionary<UniverseActor, ActorData> ActorDataDict
        {
            get { return s_ActorDataDict; }
            set {}
        }

        public static int Limit
        {
            get { return k_ActorCountLimit; }
            set {}
        }
        #endregion

        #region MONOBEHAVIOUR
        private void OnValidate()
        {
            if (Delegate_OnDictUpdate != null)
                Delegate_OnDictUpdate.Invoke();
        }

        private void OnEnable()
        {
            var force = Force.GetScaled();
            var mass = Mass.GetScaled();

            var forceVector = (ActorType == ActorType.LinearForce) ? transform.forward * force : Vector3.zero;
            mass = (ActorType == ActorType.Attractor || ActorType == ActorType.Repeller) ? mass : 0f;
            mass *= (ActorType == ActorType.Repeller) ? -1f : 1f;

            var data = new ActorData()
            {
                Position = transform.position,
                Force = forceVector,
                Mass = mass,
            };

            if (!s_ActorDataDict.ContainsKey(this))
            {
                if (s_ActorDataDict.Count >= k_ActorCountLimit)
                    Debug.LogWarning("UniverseActor couldn't be registered as the hard limit has been reached!", this);
                else
                    s_ActorDataDict.Add(this, data);

                if (Delegate_OnDictUpdate != null)
                    Delegate_OnDictUpdate.Invoke();
            }
        }

        private void OnDisable()
        {
            if (s_ActorDataDict.ContainsKey(this))
                s_ActorDataDict.Remove(this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);

            if (ActorType == ActorType.Attractor || ActorType == ActorType.Repeller)
            {
                Gizmos.DrawSphere(transform.position, 25f);
            }
            else if (ActorType == ActorType.LinearForce)
            {
                Gizmos.DrawSphere(transform.position, 5f);
                Gizmos.DrawRay(transform.position, transform.forward * 50f);
            }
        }
        #endregion
    }
}