using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Buffer : IDisposable, IEquatable<Buffer>
    {
        public readonly LogicalDevice logicalDevice;

        /// <summary>
        /// Size of the allocated buffer in bytes.
        /// </summary>
        public readonly uint size;

        private readonly VkBuffer buffer;
        private bool valid;

        public readonly bool IsDisposed => !valid;

        public readonly VkBuffer Value
        {
            get
            {
                ThrowIfDisposed();

                return buffer;
            }
        }

        public Buffer(LogicalDevice logicalDevice, uint size, VkBufferUsageFlags usage)
        {
            if (size == 0)
            {
                size = 2;
                //throw new Exception("Buffer size cannot be zero!");
            }

            VkBufferCreateInfo bufferInfo = new()
            {
                size = size,
                usage = usage,
                sharingMode = VkSharingMode.Exclusive
            };

            if (vkCreateBuffer(logicalDevice.Value, &bufferInfo, null, out buffer) != VkResult.Success)
            {
                throw new Exception("Failed to create buffer!");
            }

            this.logicalDevice = logicalDevice;
            this.size = size;
            valid = true;
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkDestroyBuffer(logicalDevice.Value, buffer);
            valid = false;
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
            return buffer.Equals(other.buffer);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(buffer);
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
