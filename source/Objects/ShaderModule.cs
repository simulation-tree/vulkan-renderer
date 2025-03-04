using System;
using System.Diagnostics;
using Unmanaged;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public struct ShaderModule : IDisposable, IEquatable<ShaderModule>
    {
        public readonly bool isInstanced;
        public readonly LogicalDevice logicalDevice;

        private readonly VkShaderModule value;
        private bool valid;

        public readonly VkShaderModule Value
        {
            get
            {
                ThrowIfDisposed();

                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        /// <summary>
        /// Creates a shader module from the given SPV bytecode.
        /// </summary>
        public unsafe ShaderModule(LogicalDevice logicalDevice, USpan<byte> code, bool isInstanced = false)
        {
            this.isInstanced = isInstanced;
            this.logicalDevice = logicalDevice;
            VkResult result = vkCreateShaderModule(logicalDevice.Value, code, null, out value);
            ThrowIfFailedToCreate(result);

            valid = true;
        }

        public unsafe void Dispose()
        {
            ThrowIfDisposed();

            vkDestroyShaderModule(logicalDevice.Value, value);
            valid = false;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(ShaderModule));
            }
        }

        [Conditional("DEBUG")]
        private static void ThrowIfFailedToCreate(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create shader module: {result}");
            }
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is ShaderModule module && Equals(module);
        }

        public readonly bool Equals(ShaderModule other)
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

        public static bool operator ==(ShaderModule left, ShaderModule right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ShaderModule left, ShaderModule right)
        {
            return !(left == right);
        }
    }
}