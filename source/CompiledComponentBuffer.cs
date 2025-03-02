using Materials;
using System;
using Vulkan;
using Worlds;

namespace Rendering.Vulkan
{
    public readonly struct CompiledComponentBuffer : IDisposable
    {
        public readonly Material material;
        public readonly uint targetEntity;
        public readonly DataType componentType;
        public readonly BufferDeviceMemory buffer;

        public CompiledComponentBuffer(Material material, uint targetEntity, DataType componentType, BufferDeviceMemory buffer)
        {
            this.material = material;
            this.targetEntity = targetEntity;
            this.componentType = componentType;
            this.buffer = buffer;
        }

        public readonly void Dispose()
        {
            buffer.Dispose();
        }
    }
}