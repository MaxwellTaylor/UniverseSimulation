using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

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
        public float Radius;
    }

    [Serializable]
    public struct MeasurementContainer
    {
        public double Value;
        public MeasurementUnits Unit;

        private static double s_Scalar = 1.0;
        private static MeasurementUnits s_SpeedDefault = Common.k_StandardSpeedUnit;
        private static MeasurementUnits s_DistanceDefault = Common.k_StandardDistanceUnit;
        private static MeasurementUnits s_MassDefault = Common.k_StandardMassUnit;
        private static MeasurementUnits s_ForceDefault = Common.k_StandardForceUnit;

        private static List<MeasurementContainer> s_MeasurementContainerList = new List<MeasurementContainer>();
        private static Dictionary<MeasurementUnits, double> s_UnitConversionDict = new Dictionary<MeasurementUnits, double>()
        {
            // Speed
            { MeasurementUnits.Speed_MetresPerSecond,        1 },
            { MeasurementUnits.Speed_KilometresPerHour,      3.6 },
            { MeasurementUnits.Speed_KilometresPerSecond,    0.001 },

            // Distance
            { MeasurementUnits.Distance_Metres,              1 },
            { MeasurementUnits.Distance_Kilometres,          0.001 },
            { MeasurementUnits.Distance_AstronomicalUnits,   6.68459e-12 },
            { MeasurementUnits.Distance_Lightyears,          1.057e-16 },
            { MeasurementUnits.Distance_Parsecs,             3.24078e-17 },

            // Mass
            { MeasurementUnits.Mass_Kilograms,               1 },
            { MeasurementUnits.Mass_MetricTonnes,            0.001 },
            { MeasurementUnits.Mass_SolarMasses,             5.0279e-31 },

            // Force
            { MeasurementUnits.Force_Newtons,                1 },
            { MeasurementUnits.Force_Kilonewtons,            0.001 },
            { MeasurementUnits.Force_Meganewtons,            1e-6 },
        };

        public MeasurementContainer(double value, MeasurementUnits unit)
        {
            Value = value;
            Unit = unit;

            s_MeasurementContainerList.Add(this);
        }

        public static void SetGlobalScalar(double scale)
        {
            s_Scalar = scale;
        }

        public static void SetGlobalDefaults(
            MeasurementUnits speedDefault, MeasurementUnits distanceDefault,
            MeasurementUnits massDefault, MeasurementUnits forceDefault)
        {
            s_SpeedDefault = speedDefault;
            s_DistanceDefault = distanceDefault;
            s_MassDefault = massDefault;
            s_ForceDefault = forceDefault;
        }

        public void SetDouble(double value, MeasurementUnits unit)
        {
            Value = value;
            Unit = unit;
        }

        public static Vector4 GetUnitConversionVector()
        {
            // These values should be the same,
            // but for now the system allows discontinuity between units passed to the compute shader
            var converters = new Vector4(
                (float)(s_UnitConversionDict[s_SpeedDefault] * s_Scalar),
                (float)(s_UnitConversionDict[s_DistanceDefault] * s_Scalar),
                (float)(s_UnitConversionDict[s_MassDefault] * s_Scalar),
                (float)(s_UnitConversionDict[s_ForceDefault] * s_Scalar));
                
            return converters;
        }

        public double GetDouble(MeasurementUnits newUnit)
        {
            // Convert to lowest unit
            var baseUnitValue = Value * (1.0 / s_UnitConversionDict[Unit]);

            // Convert to new unit
            return baseUnitValue * s_UnitConversionDict[newUnit];
        }

        public float GetScaled()
        {
            if (Unit == MeasurementUnits.Speed_MetresPerSecond ||
                Unit == MeasurementUnits.Speed_KilometresPerHour ||
                Unit == MeasurementUnits.Speed_KilometresPerSecond)
                return GetScaled(s_SpeedDefault);

            else if (Unit == MeasurementUnits.Distance_Metres ||
                Unit == MeasurementUnits.Distance_Kilometres ||
                Unit == MeasurementUnits.Distance_AstronomicalUnits ||
                Unit == MeasurementUnits.Distance_Lightyears ||
                Unit == MeasurementUnits.Distance_Parsecs)
                return GetScaled(s_DistanceDefault);

            else if (Unit == MeasurementUnits.Mass_Kilograms ||
                Unit == MeasurementUnits.Mass_MetricTonnes ||
                Unit == MeasurementUnits.Mass_SolarMasses)
                return GetScaled(s_MassDefault);

            else if (Unit == MeasurementUnits.Force_Newtons ||
                Unit == MeasurementUnits.Force_Kilonewtons ||
                Unit == MeasurementUnits.Force_Meganewtons)
                return GetScaled(s_ForceDefault);

            else
                return 0f;
        }

        public float GetScaled(MeasurementUnits newUnit)
        {
            // Convert to base unit
            var baseUnitValue = Value * (1.0 / s_UnitConversionDict[Unit]);

            // Scale in base unit
            baseUnitValue *= s_Scalar;

            // Convert to new unit
            return (float)(baseUnitValue * s_UnitConversionDict[newUnit]);
        }
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

    public enum MeasurementUnits
    {
        // Speed
        Speed_MetresPerSecond,
        Speed_KilometresPerHour,
        Speed_KilometresPerSecond,

        // Distance
        Distance_Metres,
        Distance_Kilometres,
        Distance_AstronomicalUnits,
        Distance_Lightyears,
        Distance_Parsecs,

        // Mass
        Mass_Kilograms,
        Mass_MetricTonnes,
        Mass_SolarMasses,

        // Force
        Force_Newtons,
        Force_Kilonewtons,
        Force_Meganewtons,
    }

    public enum DistanceFunction
    {
        Linear = 1,
        Quadratic = 2,
        Quartic = 4,
        Sextic = 6,
        Octic = 8,
    }

    public enum ParticleCount
    {
        Tiny_256 = 256,
        Tiny_512 = 512,
        Tiny_1024 = 1024,
        Small_2048 = 2048,
        Small_4096 = 4096,
        Medium_8192 = 8192,
        Medium_16384 = 16384,
        Large_32768 = 32768,
        Large_65536 = 65536,
    }
    #endregion
}