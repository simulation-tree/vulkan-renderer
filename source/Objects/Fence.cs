using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Fence : IDisposable, IEquatable<Fence>
    {
        public readonly LogicalDevice device;

        private readonly VkFence value;
        private bool valid;

        public readonly VkFence Value
        {
            get
            {
                ThrowIfDisposed();
                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        public Fence()
        {
            throw new NotImplementedException();
        }

        public Fence(LogicalDevice device, VkFenceCreateFlags flags = VkFenceCreateFlags.None)
        {
            this.device = device;

            VkResult result = vkCreateFence(device.Value, flags, out value);
            if (result != VkResult.Success)
            {
                throw new Exception(result.ToString());
            }

            valid = true;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Fence));
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();
            vkDestroyFence(device.Value, value);
            valid = false;
        }

        /// <summary>
        /// Waits for the fence to be signalled.
        /// </summary>
        public readonly void Wait(ulong timeout = ulong.MaxValue)
        {
            ThrowIfDisposed();
            fixed (VkFence* pFence = &value)
            {
                vkWaitForFences(device.Value, 1, pFence, true, timeout);
            }
        }

        /// <summary>
        /// Sets the state of the fence back to unsignaled.
        /// </summary>
        public readonly void Reset()
        {
            ThrowIfDisposed();
            fixed (VkFence* pFence = &value)
            {
                vkResetFences(device.Value, 1, pFence);
            }
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is Fence fence && Equals(fence);
        }

        public readonly bool Equals(Fence other)
        {
            if (IsDisposed && other.IsDisposed)
            {
                return true;
            }

            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(value);
        }

        public static bool operator ==(Fence left, Fence right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Fence left, Fence right)
        {
            return !(left == right);
        }
    }
}
