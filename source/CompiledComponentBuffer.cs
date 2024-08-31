using Simulation;
using System;
using Unmanaged;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledComponentBuffer : IDisposable
    {
        public readonly uint materialEntity;
        public readonly uint containerEntity;
        public readonly RuntimeType componentType;
        public readonly BufferDeviceMemory buffer;

        public CompiledComponentBuffer(uint materialEntity, uint containerEntity, RuntimeType componentType, BufferDeviceMemory buffer)
        {
            this.materialEntity = materialEntity;
            this.containerEntity = containerEntity;
            this.componentType = componentType;
            this.buffer = buffer;
        }

        public readonly void Dispose()
        {
            buffer.Dispose();
        }
    }
}