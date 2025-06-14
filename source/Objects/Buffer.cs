using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Buffer : IDisposable, IEquatable<Buffer>
    {
        public readonly LogicalDevice logicalDevice;
        public readonly uint byteLength;
        public readonly VkBufferUsageFlags usage;

        internal VkBuffer value;

        public readonly bool IsDisposed => value.IsNull;

        public Buffer(LogicalDevice logicalDevice, uint byteLength, VkBufferUsageFlags usage, VkSharingMode sharing = VkSharingMode.Exclusive)
        {
            if (byteLength == 0)
            {
                byteLength = 2;
                //throw new Exception("Buffer size cannot be zero!");
            }

            VkBufferCreateInfo bufferInfo = new()
            {
                size = byteLength,
                usage = usage,
                sharingMode = sharing
            };

            VkResult result = vkCreateBuffer(logicalDevice.value, &bufferInfo, null, out value);
            ThrowIfFailedToCreate(result);

            this.logicalDevice = logicalDevice;
            this.byteLength = byteLength;
            this.usage = usage;
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkDestroyBuffer(logicalDevice.value, value);
            value = default;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Buffer));
            }
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is Buffer buffer && Equals(buffer);
        }

        public readonly bool Equals(Buffer other)
        {
            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return value.GetHashCode();
        }

        [Conditional("DEBUG")]
        private static void ThrowIfFailedToCreate(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create buffer: {result}");
            }
        }

        public static bool operator ==(Buffer left, Buffer right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Buffer left, Buffer right)
        {
            return !(left == right);
        }
    }
}