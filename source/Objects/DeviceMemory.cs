using System;
using System.Diagnostics;
using Unmanaged;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct DeviceMemory : IDisposable, IEquatable<DeviceMemory>
    {
        public readonly LogicalDevice logicalDevice;
        public readonly ulong byteLength;
        public readonly VkMemoryPropertyFlags memoryFlags;

        private VkDeviceMemory value;

        public readonly bool IsDisposed => value.IsNull;

        public DeviceMemory(Buffer buffer, VkMemoryPropertyFlags memoryFlags)
        {
            this.logicalDevice = buffer.logicalDevice;
            this.memoryFlags = memoryFlags;
            vkGetBufferMemoryRequirements(logicalDevice.value, buffer.value, out VkMemoryRequirements memoryRequirements);
            byteLength = memoryRequirements.size;

            VkMemoryAllocateInfo allocInfo = new();
            allocInfo.allocationSize = memoryRequirements.size;
            allocInfo.memoryTypeIndex = logicalDevice.GetMemoryTypeIndex(memoryRequirements, memoryFlags);

            VkResult result = vkAllocateMemory(logicalDevice.value, &allocInfo, null, out value);
            ThrowIfUnableToAllocate(result);

            result = vkBindBufferMemory(logicalDevice.value, buffer.value, value, 0);
            ThrowIfUnableToBind(result);
        }

        public DeviceMemory(Image image, VkMemoryPropertyFlags memoryFlags)
        {
            this.logicalDevice = image.logicalDevice;
            vkGetImageMemoryRequirements(logicalDevice.value, image.value, out VkMemoryRequirements memoryRequirements);
            byteLength = memoryRequirements.size;

            VkMemoryAllocateInfo allocateInfo = new();
            allocateInfo.allocationSize = memoryRequirements.size;
            allocateInfo.memoryTypeIndex = logicalDevice.GetMemoryTypeIndex(memoryRequirements, memoryFlags);

            VkResult result = vkAllocateMemory(logicalDevice.value, &allocateInfo, null, out value);
            ThrowIfUnableToAllocate(result);

            result = vkBindImageMemory(logicalDevice.value, image.value, value, 0);
            ThrowIfUnableToBind(result);
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(DeviceMemory));
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkFreeMemory(logicalDevice.value, value);
            value = default;
        }

        public readonly MemoryAddress Map()
        {
            ThrowIfDisposed();

            void* byteData;
            VkResult result = vkMapMemory(logicalDevice.value, value, 0, byteLength, 0, &byteData);
            ThrowIfUnableToMap(result);

            return new(byteData);
        }

        public readonly Span<T> Map<T>(int length) where T : unmanaged
        {
            ThrowIfDisposed();

            void* byteData;
            VkResult result = vkMapMemory(logicalDevice.value, value, 0, byteLength, 0, &byteData);
            ThrowIfUnableToMap(result);

            return new(byteData, length);
        }

        public readonly Span<T> Map<T>(uint length) where T : unmanaged
        {
            ThrowIfDisposed();

            void* byteData;
            VkResult result = vkMapMemory(logicalDevice.value, value, 0, byteLength, 0, &byteData);
            ThrowIfUnableToMap(result);

            return new(byteData, (int)length);
        }

        public readonly void Unmap()
        {
            ThrowIfDisposed();

            vkUnmapMemory(logicalDevice.value, value);
        }

        /// <summary>
        /// Copies the given <paramref name="sourceData"/> into the buffer.
        /// </summary>
        public readonly void CopyFrom<T>(ReadOnlySpan<T> sourceData) where T : unmanaged
        {
            ThrowIfDisposed();

            void* byteData;
            VkResult result = vkMapMemory(logicalDevice.value, value, 0, byteLength, 0, &byteData);
            ThrowIfUnableToMap(result);

            sourceData.CopyTo(new Span<T>(byteData, sourceData.Length));
            vkUnmapMemory(logicalDevice.value, value);
        }

        /// <summary>
        /// Copies the given <paramref name="sourceData"/> into the buffer.
        /// </summary>
        public readonly void CopyFrom<T>(Span<T> sourceData) where T : unmanaged
        {
            ThrowIfDisposed();

            void* byteData;
            VkResult result = vkMapMemory(logicalDevice.value, value, 0, byteLength, 0, &byteData);
            ThrowIfUnableToMap(result);

            sourceData.CopyTo(new Span<T>(byteData, sourceData.Length));
            vkUnmapMemory(logicalDevice.value, value);
        }

        /// <summary>
        /// Copies the given <paramref name="sourceData"/>.
        /// </summary>
        public readonly void CopyFrom(MemoryAddress sourceData, int sourceByteLength)
        {
            ThrowIfDisposed();

            void* byteData;
            VkResult result = vkMapMemory(logicalDevice.value, value, 0, byteLength, 0, &byteData);
            ThrowIfUnableToMap(result);

            new Span<byte>(sourceData.Pointer, sourceByteLength).CopyTo(new Span<byte>(byteData, sourceByteLength));
            vkUnmapMemory(logicalDevice.value, value);
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is DeviceMemory memory && Equals(memory);
        }

        public readonly bool Equals(DeviceMemory other)
        {
            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(value);
        }

        [Conditional("DEBUG")]
        private static void ThrowIfUnableToMap(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException("Unable to map memory");
            }
        }

        [Conditional("DEBUG")]
        private static void ThrowIfUnableToBind(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException("Unable to bind memory");
            }
        }

        [Conditional("DEBUG")]
        private static void ThrowIfUnableToAllocate(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException("Unable to allocate memory");
            }
        }

        public static bool operator ==(DeviceMemory left, DeviceMemory right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DeviceMemory left, DeviceMemory right)
        {
            return !(left == right);
        }
    }
}