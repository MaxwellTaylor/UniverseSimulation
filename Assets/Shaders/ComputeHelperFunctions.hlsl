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

    void CalcInputForce(ParticleData data, ActorData actor, inout float3 force)
    {
        float3 toActor = actor.Position - data.Position;

        float r2 = dot(toActor, toActor) * _DistanceCoeff + EPSILON;
        r2 = lerp(r2, r2*r2, _DistanceSoftening);

        float r = sqrt(r2);
        force += Newton(actor.Force, 1.0, r);
    }

    void CalcGravitation(ParticleData data, ParticleData dataOther, inout float3 force)
    {
        float3 toOther = dataOther.Position - data.Position;
        float m1m2 = data.Mass * dataOther.Mass;

        float r2 = dot(toOther, toOther) * _DistanceCoeff + EPSILON;
        r2 = lerp(r2, r2*r2, _DistanceSoftening);

        float r = sqrt(r2);
        force += Newton(toOther, m1m2, r);
    }

    float CalcLuminosity(float mass)
    {
        // Mass–luminosity relation
        // L = m^3.5

        #define LUM_EXPONENT 2.0
        #define LUM_DIM 0.1

        float lum = saturate(mass / _MaxMass);

        lum = pow(lum, LUM_EXPONENT);
        lum = max(lum, LUM_DIM);

        return lum;
    }

    float3 Colourise(ParticleData data)
    {
        float mass = saturate(data.Mass / _MaxMass);
        float3 base = lerp(_ColourA.rgb, _ColourB.rgb, data.Entropy * mass);
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

        // A margin is applied to compensate for camera movement between passes
        const float margin = 0.2;

        float2 screenUV = ScreenPosition(position);
        return screenUV.x < 0.0 - margin || screenUV.x > 1.0 + margin || screenUV.y < 0.0 - margin || screenUV.y > 1.0 + margin;
    }

#endif // __COMPUTE_HELPER_FUNCS__