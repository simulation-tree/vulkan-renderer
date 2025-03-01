using System;
using System.Diagnostics.Contracts;
using Vulkan;
using Worlds;

namespace Rendering.Vulkan
{
    public readonly struct CompiledComponentBuffer : IDisposable
    {
        public readonly uint materialEntity;
        public readonly uint containerEntity;
        public readonly DataType componentType;
        public readonly BufferDeviceMemory buffer;

        public CompiledComponentBuffer(uint materialEntity, uint containerEntity, DataType componentType, BufferDeviceMemory buffer)
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