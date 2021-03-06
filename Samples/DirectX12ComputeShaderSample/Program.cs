﻿using System;
using System.Linq;
using System.Threading.Tasks;
using DirectX12GameEngine.Graphics;
using DirectX12GameEngine.Shaders;
using Vortice.Direct3D12;
using Vortice.Dxc;

using CommandListType = DirectX12GameEngine.Graphics.CommandListType;

namespace DirectX12ComputeShaderSample
{
    public class MyComputeShader : ComputeShaderBase
    {
#nullable disable
        public RWStructuredBufferResource<float> Destination;

        public StructuredBufferResource<float> Source;
#nullable restore

        [ShaderMember]
        [ShaderMethod]
        [Shader("compute")]
        [NumThreads(100, 1, 1)]
        public override void CSMain(CSInput input)
        {
            Destination[input.DispatchThreadId.X] = Math.Max(Source[input.DispatchThreadId.X], 45);
        }
    }

    public static class GraphicsBufferExtensions
    {
        public static StructuredBufferResource<T> GetStructuredBuffer<T>(this GraphicsBuffer<T> buffer) where T : unmanaged
        {
            return new StructuredBufferResource<T>();
        }

        public static RWStructuredBufferResource<T> GetRWStructuredBuffer<T>(this GraphicsBuffer<T> buffer) where T : unmanaged
        {
            return new RWStructuredBufferResource<T>();
        }
    }

    public class Program
    {
        private static async Task Main()
        {
            // Create graphics device

            using GraphicsDevice device = new GraphicsDevice(FeatureLevel.Level11_0);

            // Create graphics buffer

            int width = 10;
            int height = 10;

            float[] array = new float[width * height];

            for (int i = 0; i < array.Length; i++)
            {
                array[i] = i;
            }

            float[] outputArray = new float[width * height];

            using GraphicsBuffer<float> sourceBuffer = GraphicsBuffer.New(device, array.AsSpan(), DirectX12GameEngine.Graphics.ResourceFlags.ShaderResource);
            using GraphicsBuffer<float> destinationBuffer = GraphicsBuffer.New<float>(device, array.Length * 2, DirectX12GameEngine.Graphics.ResourceFlags.UnorderedAccess);

            GraphicsBuffer<float> slicedDestinationBuffer = destinationBuffer.Slice(20, 60);
            slicedDestinationBuffer = slicedDestinationBuffer.Slice(10, 50);

            DescriptorSet descriptorSet = new DescriptorSet(device, 2);
            descriptorSet.AddUnorderedAccessViews(slicedDestinationBuffer);
            descriptorSet.AddShaderResourceViews(sourceBuffer);

            // Generate computer shader

            bool generateWithDelegate = true;

            ShaderGenerator shaderGenerator = generateWithDelegate
                ? CreateShaderGeneratorWithDelegate(sourceBuffer, destinationBuffer)
                : CreateShaderGeneratorWithClass();

            ShaderGeneratorResult result = shaderGenerator.GenerateShader();

            // Compile shader

            byte[] shaderBytecode = ShaderCompiler.Compile(DxcShaderStage.ComputeShader, result.ShaderSource, result.EntryPoints["compute"]);

            DescriptorRange1[] descriptorRanges = new DescriptorRange1[]
            {
                new DescriptorRange1(DescriptorRangeType.UnorderedAccessView, 1, 0),
                new DescriptorRange1(DescriptorRangeType.ShaderResourceView, 1, 0)
            };

            RootParameter1 rootParameter = new RootParameter1(new RootDescriptorTable1(descriptorRanges), ShaderVisibility.All);

            var rootSignatureDescription = new VersionedRootSignatureDescription(new RootSignatureDescription1(RootSignatureFlags.None, new[] { rootParameter }));
            var rootSignature = device.CreateRootSignature(rootSignatureDescription);

            PipelineState pipelineState = new PipelineState(device, rootSignature, shaderBytecode);

            // Execute computer shader

            using (CommandList commandList = new CommandList(device, CommandListType.Compute))
            {
                commandList.SetPipelineState(pipelineState);

                commandList.SetComputeRootDescriptorTable(0, descriptorSet);

                commandList.Dispatch(1, 1, 1);
                await commandList.FlushAsync();
            }

            // Print matrix

            Console.WriteLine("Before:");
            PrintMatrix(array, width, height);

            destinationBuffer.GetData(outputArray.AsSpan());

            Console.WriteLine();
            Console.WriteLine("After:");
            PrintMatrix(outputArray, width, height);
        }

        private static ShaderGenerator CreateShaderGeneratorWithClass()
        {
            MyComputeShader myComputeShader = new MyComputeShader();

            return new ShaderGenerator(myComputeShader);
        }

        [AnonymousShaderMethod(0)]
        private static ShaderGenerator CreateShaderGeneratorWithDelegate(GraphicsBuffer<float> sourceBuffer, GraphicsBuffer<float> destinationBuffer)
        {
            StructuredBufferResource<float> source = sourceBuffer.GetStructuredBuffer();
            RWStructuredBufferResource<float> destination = destinationBuffer.GetRWStructuredBuffer();

            Action<CSInput> action = input =>
            {
                destination[input.DispatchThreadId.X] = Math.Max(source[input.DispatchThreadId.X], 45);
            };

            return new ShaderGenerator(action, new ShaderAttribute("compute"), new NumThreadsAttribute(100, 1, 1));
        }

        private static void PrintMatrix(float[] array, int width, int height)
        {
            int numberWidth = array.Max().ToString().Length;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Console.Write(array[x + y * width].ToString().PadLeft(numberWidth));
                    Console.Write(", ");
                }

                Console.WriteLine();
            }
        }
    }
}
