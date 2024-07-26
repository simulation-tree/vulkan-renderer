using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct ShaderModule : IDisposable, IEquatable<ShaderModule>
    {
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

        public ShaderModule(LogicalDevice device, ReadOnlySpan<byte> code)
        {
            this.logicalDevice = device;
            VkResult result = vkCreateShaderModule(device.Value, code, null, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create shader module: {result}");
            }

            valid = true;
        }

        public void Dispose()
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
