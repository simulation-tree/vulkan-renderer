using System;
using Vortice.Vulkan;

namespace Vulkan
{
    public unsafe struct BufferDeviceMemory : IDisposable, IEquatable<BufferDeviceMemory>
    {
        public Buffer buffer;
        public DeviceMemory memory;

        public BufferDeviceMemory(LogicalDevice device, uint byteLength, VkBufferUsageFlags usage, VkMemoryPropertyFlags memoryFlags)
        {
            buffer = new(device, byteLength, usage);
            memory = new(buffer, memoryFlags);
        }

        public void Dispose()
        {
            memory.Dispose();
            buffer.Dispose();
            memory = default;
            buffer = default;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is BufferDeviceMemory memory && Equals(memory);
        }

        public readonly bool Equals(BufferDeviceMemory other)
        {
            return buffer.Equals(other.buffer) && memory.Equals(other.memory);
        }

        public readonly override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 31 + buffer.GetHashCode();
            hash = hash * 31 + memory.GetHashCode();
            return hash;
        }

        /// <summary>
        /// Resizes the buffer and memory to the new size,
        /// without copying old data.
        /// </summary>
        public void Resize(uint newByteLength)
        {
            VkBufferUsageFlags usage = buffer.usage;
            VkMemoryPropertyFlags memoryFlags = memory.memoryFlags;
            buffer.Dispose();
            memory.Dispose();
            buffer = new(buffer.logicalDevice, newByteLength, usage);
            memory = new(buffer, memoryFlags);
        }

        public static bool operator ==(BufferDeviceMemory left, BufferDeviceMemory right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BufferDeviceMemory left, BufferDeviceMemory right)
        {
            return !(left == right);
        }
    }
}