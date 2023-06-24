#ifndef __COMPUTE_HELPER_FUNCS__
#define __COMPUTE_HELPER_FUNCS__

    float3 SafeNormalise(float3 vec, float len)
    {
        // The intrinsic normalize() function seems to return NaN's
        return vec / max(len, EPSILON);
    }

    float3 Newton(float3 direction, float m1m2, float r2)
    {
        // Newton's law of universal gravitation
        // F = G(m1m2/r^2)
        return direction * _G * (m1m2 / r2);
    }

    float CalcDistance(float3 vec)
    {
        float r = length(vec);

        // Direction of gravitation force
        vec = SafeNormalise(vec, r);

        // Inverse square law
        r *= _DistanceCoeff;
        float r2 = max(r * r, EPSILON);
        r2 = lerp(r2, 1.0, _DistanceSoftening);

        return r2;
    }

    void CalcInputForce(ParticleData data, ActorData actor, inout float3 force)
    {
        float3 toActor = actor.Position - data.Position;

        float r2 = CalcDistance(toActor);
        force += Newton(actor.Force, 1.0, r2);
    }

    void CalcGravitation(ParticleData data, ParticleData dataOther, inout float3 force)
    {
        float m1m2 = data.Mass * dataOther.Mass;
        float3 toOther = dataOther.Position - data.Position;

        float r2 = CalcDistance(toOther);
        force += Newton(toOther, m1m2, r2);
    }

    float CalcLuminosity(float mass)
    {
        // Mass–luminosity relation
        // L = m^3.5

        #define LUM_EXPONENT 1.2
        #define LUM_DIM 0.1

        float lum = saturate(mass / _MaxMass);

        lum = pow(lum, LUM_EXPONENT);
        lum = max(lum, LUM_DIM);

        return lum;
    }

    float3 Colourise(ParticleData data)
    {
        #define COLOUR_A float3(0.176, 0.733, 0.921)
        #define COLOUR_B float3(1.2, 0.6, 0.1)

        float3 base = lerp(COLOUR_A, COLOUR_B, data.Entropy);
        return base;
    }

    float2 ScreenPosition(float4 position)
    {
        float4 o = position * 0.5f;

        o.xy = float2(o.x, o.y) + o.w;
        o.zw = position.zw;
        o.xy /= o.w;

        return o.xy;
    }

    bool ShouldClip(ParticleData data)
    {
        float4 position = float4(data.Position, 1.0);
        position = mul(_VPMatrix, position);

        float2 screenUV = ScreenPosition(position);
        return screenUV.x < 0.0 || screenUV.x > 1.0 || screenUV.y < 0.0 || screenUV.y > 1.0;
    }

#endif // __COMPUTE_HELPER_FUNCS__