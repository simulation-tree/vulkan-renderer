using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Semaphore : IDisposable, IEquatable<Semaphore>
    {
        public readonly LogicalDevice logicalDevice;

        internal VkSemaphore value;

        public readonly bool IsDisposed => value.IsNull;

        [Obsolete("Default constructor not supported", true)]
        public Semaphore()
        {
            throw new NotImplementedException();
        }

        public Semaphore(LogicalDevice logicalDevice)
        {
            this.logicalDevice = logicalDevice;
            VkResult result = vkCreateSemaphore(logicalDevice.value, out value);
            ThrowIfUnableToCreate(result);
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Semaphore));
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkDestroySemaphore(logicalDevice.value, value);
            value = default;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is Semaphore semaphore && Equals(semaphore);
        }

        public readonly bool Equals(Semaphore other)
        {
            if (IsDisposed && other.IsDisposed)
            {
                return true;
            }

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
                throw new Exception($"Failed to create semaphore: {result}");
            }
        }

        public static bool operator ==(Semaphore left, Semaphore right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Semaphore left, Semaphore right)
        {
            return !(left == right);
        }
    }
}