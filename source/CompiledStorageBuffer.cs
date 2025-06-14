using System;
using Vortice.Vulkan;
using Vulkan;

namespace Rendering.Vulkan
{
    public struct CompiledStorageBuffer : IDisposable
    {
        private BufferDeviceMemory buffer;

        public CompiledStorageBuffer(LogicalDevice device, uint byteLength)
        {
            VkMemoryPropertyFlags memoryFlags = VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent;
            buffer = new(device, byteLength, VkBufferUsageFlags.StorageBuffer, memoryFlags);
        }

        public void Dispose()
        {
            buffer.Dispose();
        }
    }
}