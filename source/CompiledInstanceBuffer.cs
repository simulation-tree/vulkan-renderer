using System;
using Vulkan;

namespace Rendering.Vulkan
{
    public readonly struct CompiledInstanceBuffer : IDisposable
    {
        public readonly BufferDeviceMemory buffer;

        public CompiledInstanceBuffer(BufferDeviceMemory buffer)
        {
            this.buffer = buffer;
        }

        public readonly void Dispose()
        {
            buffer.Dispose();
        }
    }
}