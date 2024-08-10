using Simulation;
using System;
using Unmanaged;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledComponentBuffer : IDisposable
    {
        public readonly eint entity;
        public readonly RuntimeType componentType;
        public readonly BufferDeviceMemory buffer;

        public CompiledComponentBuffer(eint entity, RuntimeType componentType, BufferDeviceMemory buffer)
        {
            this.entity = entity;
            this.componentType = componentType;
            this.buffer = buffer;
        }

        public readonly void Dispose()
        {
            buffer.Dispose();
        }
    }
}