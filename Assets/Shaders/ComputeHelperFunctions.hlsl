#ifndef __COMPUTE_HELPER_FUNCS__
#define __COMPUTE_HELPER_FUNCS__

    float3 SafeNormalise(float3 vec, float len)
    {
        // The intrinsic normalize() function seems to return NaN's
        return vec / max(len, EPSILON);
    }

    void CalcGravitation(ParticleData data, ParticleData dataOther, inout float3 force)
    {
        float m1m2 = data.Mass * dataOther.Mass;

        float3 toOther = dataOther.Position - data.Position;
        float r = length(toOther);

        // Direction of gravitation force
        toOther = SafeNormalise(toOther, r);

        // Inverse square law
        r *= _DistanceCoeff;
        float r2 = max(r * r, EPSILON);
        r2 = lerp(r2, 1.0, _DistanceSoftening);

        // Newton's law of universal gravitation
        // F = G(m1m2/r^2)
        force += toOther * _G * (m1m2 / r2);
    }

    void CalcForces(Input input, inout ParticleData data)
    {
        #define N _InstanceCount / THREAD_DIM.y

        uint start = N * input.ThreadID.y;
        uint finish = start + N;
        uint tile = start / N;

        uint x;
        uint idxRead;
        uint idxShared;

        float3 force = float3(0.0, 0.0, 0.0);

        //[unroll(N / THREAD_DIM.x)]
        for (uint i = start; i < finish; i += THREAD_DIM.x, tile++) 
        {
            x = input.GroupID.x + tile;

            idxShared = input.ThreadID.x + THREAD_DIM.x * input.ThreadID.y;
            idxRead = ((x) < GROUP_DIM.x) ? (x) : (x - GROUP_DIM.x)  * THREAD_DIM.x + input.ThreadID.x;

            _SharedData[idxShared] = _ParticleBufferRead[idxRead];
            GroupMemoryBarrierWithGroupSync();

            [unroll(THREAD_DIM.x)]
            for (uint j = 0; j < THREAD_DIM.x; j++) 
            {
                idxShared = j + THREAD_DIM.x * input.ThreadID.y;
                CalcGravitation(data, _SharedData[idxShared], /*inout*/ force);
            }

            GroupMemoryBarrierWithGroupSync();
        }

        float3 acceleration = (force / data.Mass) * _TimeStep;

        data.Velocity += acceleration;
        data.Position += data.Velocity;
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