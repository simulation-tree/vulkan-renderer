using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Semaphore : IDisposable, IEquatable<Semaphore>
    {
        public readonly LogicalDevice logicalDevice;

        private readonly VkSemaphore value;
        private bool valid;

        public readonly VkSemaphore Value
        {
            get
            {
                ThrowIfDisposed();

                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        [Obsolete("Default constructor not supported", true)]
        public Semaphore()
        { 
            throw new NotImplementedException();
        }

        public Semaphore(LogicalDevice logicalDevice)
        {
            this.logicalDevice = logicalDevice;
            VkResult result = vkCreateSemaphore(logicalDevice.Value, out value);
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
                throw new ObjectDisposedException(nameof(Semaphore));
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkDestroySemaphore(logicalDevice.Value, value);
            valid = false;
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
            return HashCode.Combine(value, IsDisposed);
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