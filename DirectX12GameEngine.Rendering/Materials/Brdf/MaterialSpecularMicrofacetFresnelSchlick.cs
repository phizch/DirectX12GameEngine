﻿using System.Numerics;
using DirectX12GameEngine.Shaders;

namespace DirectX12GameEngine.Rendering.Materials.Brdf
{
    [StaticResource]
    public class MaterialSpecularMicrofacetFresnelSchlick : IMaterialSpecularMicrofacetFresnelFunction
    {
        public void Visit(MaterialGeneratorContext context)
        {
        }

        [ShaderMember]
        [ShaderMethod]
        public Vector3 Compute(Vector3 f0)
        {
            return BrdfMicrofacet.FresnelSchlick(f0, MaterialPixelShadingStream.LDotH);
        }
    }
}
