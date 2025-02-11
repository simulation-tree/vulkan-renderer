using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Fence : IDisposable, IEquatable<Fence>
    {
        public readonly LogicalDevice logicalDevice;

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

        [Obsolete("Default constructor not supported", true)]
        public Fence()
        {
            throw new NotImplementedException();
        }

        public Fence(LogicalDevice logicalDevice, bool isSignaled = true)
        {
            this.logicalDevice = logicalDevice;
            VkResult result = vkCreateFence(logicalDevice.Value, isSignaled ? VkFenceCreateFlags.Signaled : VkFenceCreateFlags.None, out value);
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

            vkDestroyFence(logicalDevice.Value, value);
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
                vkWaitForFences(logicalDevice.Value, 1, pFence, true, timeout);
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
                vkResetFences(logicalDevice.Value, 1, pFence);
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
