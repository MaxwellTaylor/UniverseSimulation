#ifndef __COMPUTE_HELPER_FUNCS__
#define __COMPUTE_HELPER_FUNCS__

    float3 SafeNormalise(float3 vec, float len)
    {
        // The intrinsic normalize() function seems to return NaN's
        return vec / max(len, EPSILON);
    }

    bool SphereSphereCollisionTest(ParticleData data, ParticleData dataOther, float otherRadius)
    {
        float3 toOther = (dataOther.Position - data.Position) * _DistanceCoeff;

        float r2 = dot(toOther, toOther) + EPSILON;
        float ra2 = _ParticleRadius + otherRadius;

        bool test = r2 < (ra2*ra2);
        return test;
    }

    void SphereSphereMomentumExchange(ParticleData data, ParticleData dataOther, float otherRadius, inout float3 collisionOffset, inout float3 collisionAcceleration)
    {
        float3 normal = (data.Position - dataOther.Position);

        float r2 = dot(normal, normal) + EPSILON;
        float len = sqrt(r2);
        normal = SafeNormalise(normal, len);

        float3 deltaVelocity = dataOther.Velocity - data.Velocity;
        float massRatio = 1.0 / ((data.Mass / dataOther.Mass) + 1);
        float massTotal = data.Mass + dataOther.Mass;

        float radii = _ParticleRadius + otherRadius;
        float margin = _ParticleRadius * 0.1;

        float3 relativeVelocity = data.Velocity - dataOther.Velocity;
        float vDotN = dot(relativeVelocity, -normal);

        collisionAcceleration += vDotN * massRatio * normal;
        collisionOffset += (radii - len + margin) * normal;
    }

    float3 Newton(float3 direction, float m1m2, float r2)
    {
        DIST_F(r2)

        // Newton's Law of Universal Gravitation
        // Outputs Newtons

        // F = G(m1m2/r^2)
        return direction * _G * (m1m2 / r2);
    }

    void CalcGravitation(ParticleData data, ParticleData dataOther, inout float3 force)
    {
        float3 toOther = (dataOther.Position - data.Position) * _DistanceCoeff;
        float m1m2 = data.Mass * dataOther.Mass;

        float r2 = dot(toOther, toOther) + EPSILON;
        float len = sqrt(r2);
        toOther = SafeNormalise(toOther, len);

        force += Newton(toOther, m1m2, r2);
    }

    void CalcInputForce(ParticleData data, ActorData actor, inout float3 force)
    {
        float3 toActor = actor.Position - data.Position;
        float r2 = dot(toActor, toActor);

        force += Newton(actor.Force, 1.0, r2);
    }

    float CalcLuminosity(float mass)
    {
        // Mass–luminosity relation
        // L = m^3.5

        #define LUM_EXPONENT 2.0
        #define LUM_DIM 0.1

        float lum = saturate(mass / _AvgMass);

        lum = pow(lum, LUM_EXPONENT);
        lum = max(lum, LUM_DIM);

        return lum;
    }

    float3x3 RotationX(float theta)
    {
        float c = cos(theta);
        float s = sin(theta);
        return float3x3(float3(1.0, 0.0, 0.0), float3(0.0, c, -s), float3(0.0, s, c));
    }

    float3x3 RotationY(float theta)
    {
        float c = cos(theta);
        float s = sin(theta);
        return float3x3(float3(c, 0.0, s), float3(0.0, 1.0, 0.0), float3(-s, 0.0, c));
    }

    float3 Colourise(ParticleData data)
    {
        float mass = saturate(data.Mass / _AvgMass);
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