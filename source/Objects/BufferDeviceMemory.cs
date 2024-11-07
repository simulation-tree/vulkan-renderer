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

        public readonly nint Map()
        {
            return memory.Map();
        }

        public readonly void Unmap()
        {
            memory.Unmap();
        }

        public readonly USpan<T> Map<T>() where T : unmanaged
        {
            nint pointer = Map();
            uint size = buffer.size;
            uint elementSize = TypeInfo<T>.size;
            return new USpan<T>((void*)pointer, size / elementSize);
        }

        /// <summary>
        /// Maps the memory and copies the data from the pointer,
        /// then unmaps it.
        /// </summary>
        public readonly void CopyFrom(void* pointer, uint length)
        {
            nint memoryPointer = Map();
            Unsafe.CopyBlock(pointer, (void*)memoryPointer, length);
            Unmap();
        }

        /// <summary>
        /// Maps the memory and copies the data from the given span of bytes,
        /// then unmaps it.
        /// </summary>
        public readonly void CopyFrom(USpan<byte> bytes)
        {
            nint pointer = Map();
            bytes.CopyTo(new USpan<byte>((void*)pointer, bytes.Length));
            Unmap();
        }

        /// <summary>
        /// Maps the memory and copies the data from the given span then unmaps it.
        /// </summary>
        public readonly void CopyFrom<T>(USpan<T> data) where T : unmanaged
        {
            nint pointer = Map();
            uint size = buffer.size;
            uint elementSize = TypeInfo<T>.size;
            USpan<T> span = new((void*)pointer, size / elementSize);
            data.CopyTo(span);
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
