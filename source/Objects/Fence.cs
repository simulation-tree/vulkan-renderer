using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Fence : IDisposable, IEquatable<Fence>
    {
        public readonly LogicalDevice logicalDevice;

        internal VkFence value;

        public readonly bool IsDisposed => value.IsNull;

        [Obsolete("Default constructor not supported", true)]
        public Fence()
        {
            throw new NotImplementedException();
        }

        public Fence(LogicalDevice logicalDevice, VkFenceCreateFlags fenceFlags = VkFenceCreateFlags.Signaled)
        {
            this.logicalDevice = logicalDevice;
            VkResult result = vkCreateFence(logicalDevice.value, fenceFlags, out value);
            ThrowIfUnableToCreate(result);
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

            vkDestroyFence(logicalDevice.value, value);
            value = default;
        }

        /// <summary>
        /// Waits for the fence to be signalled.
        /// </summary>
        public readonly void Wait(ulong timeout = ulong.MaxValue)
        {
            ThrowIfDisposed();

            fixed (VkFence* pFence = &value)
            {
                vkWaitForFences(logicalDevice.value, 1, pFence, true, timeout);
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
                vkResetFences(logicalDevice.value, 1, pFence);
            }
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is Fence fence && Equals(fence);
        }

        public readonly bool Equals(Fence other)
        {
            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return value.GetHashCode();
        }

        [Conditional("DEBUG")]
        private static void ThrowIfUnableToCreate(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException($"Unable to create fence: {result}");
            }
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