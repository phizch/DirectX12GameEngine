﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DirectX12GameEngine.Core;
using Nito.AsyncEx.Interop;
using SharpGen.Runtime;
using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;

namespace DirectX12GameEngine.Graphics
{
    public sealed class GraphicsDevice : IDisposable, ICollector
    {
        private readonly AutoResetEvent fenceEvent = new AutoResetEvent(false);
        private readonly object fenceLock = new object();

        private Vortice.Direct3D11.ID3D11Device? nativeDirect3D11Device;

        public GraphicsDevice(FeatureLevel minFeatureLevel = FeatureLevel.Level11_0, bool enableDebugLayer = false)
        {
#if DEBUG
            if (enableDebugLayer)
            {
                Result debugResult = D3D12.D3D12GetDebugInterface(out ID3D12Debug debugInterface);

                using ID3D12Debug debug = debugInterface;

                if (debugResult.Success)
                {
                    debug.EnableDebugLayer();
                }
            }
#endif
            FeatureLevel = minFeatureLevel < FeatureLevel.Level11_0 ? FeatureLevel.Level11_0 : minFeatureLevel;

            Result result = D3D12.D3D12CreateDevice(null, (Vortice.Direct3D.FeatureLevel)FeatureLevel, out ID3D12Device device);

            if (result.Failure)
            {
                throw new COMException("Device creation failed.", result.Code);
            }

            NativeDevice = device;

            NativeComputeCommandQueue = NativeDevice.CreateCommandQueue(new CommandQueueDescription(Vortice.Direct3D12.CommandListType.Compute));
            NativeCopyCommandQueue = NativeDevice.CreateCommandQueue(new CommandQueueDescription(Vortice.Direct3D12.CommandListType.Copy));
            NativeDirectCommandQueue = NativeDevice.CreateCommandQueue(new CommandQueueDescription(Vortice.Direct3D12.CommandListType.Direct));

            BundleAllocatorPool = new CommandAllocatorPool(this, CommandListType.Bundle);
            ComputeAllocatorPool = new CommandAllocatorPool(this, CommandListType.Compute);
            CopyAllocatorPool = new CommandAllocatorPool(this, CommandListType.Copy);
            DirectAllocatorPool = new CommandAllocatorPool(this, CommandListType.Direct);

            NativeComputeFence = NativeDevice.CreateFence(0, FenceFlags.None);
            NativeCopyFence = NativeDevice.CreateFence(0, FenceFlags.None);
            NativeDirectFence = NativeDevice.CreateFence(0, FenceFlags.None);

            DepthStencilViewAllocator = new DescriptorAllocator(this, DescriptorHeapType.DepthStencilView, 1);
            RenderTargetViewAllocator = new DescriptorAllocator(this, DescriptorHeapType.RenderTargetView, 2);
            ShaderResourceViewAllocator = new DescriptorAllocator(this, DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, 4096);
            SamplerAllocator = new DescriptorAllocator(this, DescriptorHeapType.Sampler, 256);

            ShaderVisibleShaderResourceViewAllocator = new DescriptorAllocator(this, DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, 4096, DescriptorHeapFlags.ShaderVisible);
            ShaderVisibleSamplerAllocator = new DescriptorAllocator(this, DescriptorHeapType.Sampler, 256, DescriptorHeapFlags.ShaderVisible);

            CommandList = new CommandList(this, CommandListType.Direct);
            CommandList.Close();

            CopyCommandList = new CommandList(this, CommandListType.Copy);
            CopyCommandList.Close();
        }

        public CommandList CommandList { get; }

        public CommandList CopyCommandList { get; }

        public ICollection<IDisposable> Disposables { get; } = new List<IDisposable>();

        public FeatureLevel FeatureLevel { get; }

        public GraphicsPresenter? Presenter { get; set; }

        public ID3D12Device NativeDevice { get; }

        public Vortice.Direct3D11.ID3D11Device NativeDirect3D11Device
        {
            get
            {
                if (nativeDirect3D11Device is null)
                {
                    Result result = Vortice.Direct3D11.D3D11.D3D11On12CreateDevice(
                        NativeDevice, Vortice.Direct3D11.DeviceCreationFlags.BgraSupport, new[] { (Vortice.Direct3D.FeatureLevel)FeatureLevel }, new[] { NativeDirectCommandQueue }, 0,
                        out nativeDirect3D11Device, out _, out _);

                    if (result.Failure)
                    {
                        throw new COMException("Device creation failed.", result.Code);
                    }
                }

                return nativeDirect3D11Device;
            }
        }

        internal DescriptorAllocator DepthStencilViewAllocator { get; set; }

        internal DescriptorAllocator RenderTargetViewAllocator { get; set; }

        internal DescriptorAllocator ShaderResourceViewAllocator { get; set; }

        internal DescriptorAllocator SamplerAllocator { get; set; }

        internal DescriptorAllocator ShaderVisibleShaderResourceViewAllocator { get; }

        internal DescriptorAllocator ShaderVisibleSamplerAllocator { get; }


        internal CommandAllocatorPool BundleAllocatorPool { get; }

        internal CommandAllocatorPool ComputeAllocatorPool { get; }

        internal CommandAllocatorPool CopyAllocatorPool { get; }

        internal CommandAllocatorPool DirectAllocatorPool { get; }


        internal ID3D12CommandQueue NativeComputeCommandQueue { get; }

        internal ID3D12CommandQueue NativeCopyCommandQueue { get; }

        internal ID3D12CommandQueue NativeDirectCommandQueue { get; }


        internal ID3D12Fence NativeComputeFence { get; }

        internal ID3D12Fence NativeCopyFence { get; }

        internal ID3D12Fence NativeDirectFence { get; }


        internal long NextComputeFenceValue { get; private set; } = 1;

        internal long NextCopyFenceValue { get; private set; } = 1;

        internal long NextDirectFenceValue { get; private set; } = 1;

        public ID3D12RootSignature CreateRootSignature(VersionedRootSignatureDescription rootSignatureDescription)
        {
            return NativeDevice.CreateRootSignature(rootSignatureDescription);
        }

        public void Dispose()
        {
            NativeDirectCommandQueue.Signal(NativeDirectFence, NextDirectFenceValue);
            NativeDirectCommandQueue.Wait(NativeDirectFence, NextDirectFenceValue);

            CommandList.Dispose();
            CopyCommandList.Dispose();

            DepthStencilViewAllocator.Dispose();
            RenderTargetViewAllocator.Dispose();
            ShaderResourceViewAllocator.Dispose();

            BundleAllocatorPool.Dispose();
            ComputeAllocatorPool.Dispose();
            CopyAllocatorPool.Dispose();
            DirectAllocatorPool.Dispose();

            NativeComputeCommandQueue.Dispose();
            NativeCopyCommandQueue.Dispose();
            NativeDirectCommandQueue.Dispose();

            NativeComputeFence.Dispose();
            NativeDirectFence.Dispose();
            NativeDirectFence.Dispose();

            foreach (IDisposable disposable in Disposables)
            {
                disposable.Dispose();
            }

            fenceEvent.Dispose();

            nativeDirect3D11Device?.Dispose();

            NativeDevice.Dispose();
        }

        public void ExecuteCommandLists(bool wait, params CompiledCommandList[] commandLists)
        {
            ID3D12Fence fence = commandLists[0].Builder.CommandListType switch
            {
                CommandListType.Direct => NativeDirectFence,
                CommandListType.Compute => NativeComputeFence,
                CommandListType.Copy => NativeCopyFence,
                _ => throw new NotSupportedException("This command list type is not supported.")
            };

            long fenceValue = ExecuteCommandLists(commandLists);

            if (wait)
            {
                WaitForFence(fence, fenceValue);
            }
        }

        public Task ExecuteCommandListsAsync(params CompiledCommandList[] commandLists)
        {
            ID3D12Fence fence = commandLists[0].Builder.CommandListType switch
            {
                CommandListType.Direct => NativeDirectFence,
                CommandListType.Compute => NativeComputeFence,
                CommandListType.Copy => NativeCopyFence,
                _ => throw new NotSupportedException("This command list type is not supported.")
            };

            long fenceValue = ExecuteCommandLists(commandLists);

            return WaitForFenceAsync(fence, fenceValue);
        }

        private long ExecuteCommandLists(params CompiledCommandList[] commandLists)
        {
            CommandAllocatorPool commandAllocatorPool;
            ID3D12CommandQueue commandQueue;
            ID3D12Fence fence;
            long fenceValue;

            switch (commandLists[0].Builder.CommandListType)
            {
                case CommandListType.Compute:
                    commandAllocatorPool = ComputeAllocatorPool;
                    commandQueue = NativeComputeCommandQueue;

                    fence = NativeComputeFence;
                    fenceValue = NextComputeFenceValue++;
                    break;
                case CommandListType.Copy:
                    commandAllocatorPool = CopyAllocatorPool;
                    commandQueue = NativeCopyCommandQueue;

                    fence = NativeCopyFence;
                    fenceValue = NextCopyFenceValue++;
                    break;
                case CommandListType.Direct:
                    commandAllocatorPool = DirectAllocatorPool;
                    commandQueue = NativeDirectCommandQueue;

                    fence = NativeDirectFence;
                    fenceValue = NextDirectFenceValue++;
                    break;
                default:
                    throw new NotSupportedException("This command list type is not supported.");
            }

            ID3D12CommandList[] nativeCommandLists = new ID3D12CommandList[commandLists.Length];

            for (int i = 0; i < commandLists.Length; i++)
            {
                nativeCommandLists[i] = commandLists[i].NativeCommandList;
                commandAllocatorPool.Enqueue(commandLists[i].NativeCommandAllocator, fenceValue);
            }

            commandQueue.ExecuteCommandLists(nativeCommandLists);
            commandQueue.Signal(fence, fenceValue);

            return fenceValue;
        }

        internal bool IsFenceComplete(ID3D12Fence fence, long fenceValue)
        {
            return fence.CompletedValue >= fenceValue;
        }

        internal void WaitForFence(ID3D12Fence fence, long fenceValue)
        {
            if (IsFenceComplete(fence, fenceValue)) return;

            lock (fenceLock)
            {
                fence.SetEventOnCompletion(fenceValue, fenceEvent);

                fenceEvent.WaitOne();
            }
        }

        internal Task WaitForFenceAsync(ID3D12Fence fence, long fenceValue)
        {
            if (IsFenceComplete(fence, fenceValue)) return Task.CompletedTask;

            lock (fenceLock)
            {
                fence.SetEventOnCompletion(fenceValue, fenceEvent);

                return WaitHandleAsyncFactory.FromWaitHandle(fenceEvent);
            }
        }
    }
}
