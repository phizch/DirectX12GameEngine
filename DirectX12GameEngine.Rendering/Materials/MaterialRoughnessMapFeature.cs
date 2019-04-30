﻿using DirectX12GameEngine.Core;
using DirectX12GameEngine.Graphics;
using DirectX12GameEngine.Shaders;

namespace DirectX12GameEngine.Rendering.Materials
{
    [StaticResource]
    public class MaterialRoughnessMapFeature : IMaterialMicroSurfaceFeature
    {
        private Buffer? invertBuffer;

        public MaterialRoughnessMapFeature()
        {
        }

        public MaterialRoughnessMapFeature(IComputeScalar roughnessMap)
        {
            RoughnessMap = roughnessMap;
        }

        public void Visit(MaterialGeneratorContext context)
        {
            RoughnessMap.Visit(context);

            invertBuffer ??= Buffer.Constant.New(context.GraphicsDevice, Invert).DisposeBy(context.GraphicsDevice);

            context.ConstantBuffers.Add(invertBuffer);
        }

        #region Shader

        [ShaderResource] public IComputeScalar RoughnessMap { get; set; } = new ComputeScalar();

        [ConstantBufferResource] public bool Invert { get; set; }

        [ShaderMethod]
        public void Compute()
        {
            float roughness = RoughnessMap.Compute();
            roughness = Invert ? 1.0f - roughness : roughness;

            MaterialPixelStream.MaterialRoughness = roughness;
        }

        #endregion
    }
}