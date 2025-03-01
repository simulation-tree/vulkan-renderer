using System;
using System.Runtime.CompilerServices;
using Unmanaged;
using Vortice.Vulkan;

namespace Vulkan
{
    public readonly unsafe struct BufferDeviceMemory : IDisposable, IEquatable<BufferDeviceMemory>
    {
        public readonly Buffer buffer;
        public readonly DeviceMemory memory;

        public readonly LogicalDevice LogicalDevice => buffer.logicalDevice;

        public BufferDeviceMemory(LogicalDevice device, uint size, VkBufferUsageFlags usage, VkMemoryPropertyFlags properties)
        {
            buffer = new(device, size, usage);
            memory = new(buffer, properties);
        }

        public readonly void Dispose()
        {
            memory.Dispose();
            buffer.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Allocation Map()
        {
            return memory.Map();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Unmap()
        {
            memory.Unmap();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly USpan<T> Map<T>() where T : unmanaged
        {
            Allocation memoryPointer = Map();
            return new(memoryPointer.Pointer, buffer.size);
        }

        /// <summary>
        /// Maps the memory and copies the <paramref name="data"/> to it,
        /// then unmaps it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void CopyFrom(Allocation data, uint byteLength)
        {
            Allocation memoryPointer = Map();
            memoryPointer.CopyFrom(data, byteLength);
            Unmap();
        }

        /// <summary>
        /// Maps the memory and copies the data from the given span then unmaps it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void CopyFrom<T>(USpan<T> data) where T : unmanaged
        {
            Allocation memoryPointer = Map();
            memoryPointer.CopyFrom(data.Pointer, buffer.size);
            Unmap();
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
            return HashCode.Combine(buffer, memory);
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