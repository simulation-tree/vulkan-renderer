using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public struct ShaderModule : IDisposable, IEquatable<ShaderModule>
    {
        public readonly LogicalDevice logicalDevice;

        internal VkShaderModule value;

        public readonly bool IsDisposed => value.IsNull;

        /// <summary>
        /// Creates a shader module from the given SPV bytecode.
        /// </summary>
        public unsafe ShaderModule(LogicalDevice logicalDevice, ReadOnlySpan<byte> code)
        {
            this.logicalDevice = logicalDevice;
            VkResult result = vkCreateShaderModule(logicalDevice.value, code, null, out value);
            ThrowIfFailedToCreate(result);
        }

        public unsafe void Dispose()
        {
            ThrowIfDisposed();

            vkDestroyShaderModule(logicalDevice.value, value);
            value = default;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(ShaderModule));
            }
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is ShaderModule module && Equals(module);
        }

        public readonly bool Equals(ShaderModule other)
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
                throw new Exception($"Failed to create shader module: {result}");
            }
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