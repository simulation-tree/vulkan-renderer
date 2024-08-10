using Simulation;
using System;
using Unmanaged;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledComponentBuffer : IDisposable
    {
        public readonly eint materialEntity;
        public readonly eint containerEntity;
        public readonly RuntimeType componentType;
        public readonly BufferDeviceMemory buffer;

        public CompiledComponentBuffer(eint materialEntity, eint containerEntity, RuntimeType componentType, BufferDeviceMemory buffer)
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