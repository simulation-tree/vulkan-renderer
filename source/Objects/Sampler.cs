using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Sampler : IDisposable, IEquatable<Sampler>
    {
        public readonly LogicalDevice device;

        private readonly VkSampler value;
        private bool valid;

        public readonly VkSampler Value
        {
            get
            {
                ThrowIfDisposed();

                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        public Sampler(LogicalDevice device, SamplerCreateParameters createInfo)
        {
            VkSamplerCreateInfo vkCreateInfo = new();
            vkCreateInfo.magFilter = createInfo.magFilter;
            vkCreateInfo.minFilter = createInfo.minFilter;
            vkCreateInfo.mipmapMode = createInfo.mipmapMode;
            vkCreateInfo.addressModeU = createInfo.addressModeX;
            vkCreateInfo.addressModeV = createInfo.addressModeY;
            vkCreateInfo.addressModeW = VkSamplerAddressMode.Repeat;
            vkCreateInfo.anisotropyEnable = createInfo.anisotropy;

            VkPhysicalDeviceProperties properties = device.physicalDevice.GetProperties();
            vkCreateInfo.maxAnisotropy = properties.limits.maxSamplerAnisotropy;
            vkCreateInfo.borderColor = VkBorderColor.IntOpaqueBlack;
            vkCreateInfo.unnormalizedCoordinates = false;
            vkCreateInfo.compareEnable = createInfo.compareEnable;
            vkCreateInfo.compareOp = createInfo.compareOp;
            vkCreateInfo.mipLodBias = createInfo.mipLoadBias;
            vkCreateInfo.minLod = createInfo.minLod;
            vkCreateInfo.maxLod = createInfo.maxLod;

            VkResult result = vkCreateSampler(device.Value, &vkCreateInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create sampler: {result}");
            }

            this.device = device;
            valid = true;
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkDestroySampler(device.Value, value);
            valid = false;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Sampler));
            }
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is Sampler sampler && Equals(sampler);
        }

        public readonly bool Equals(Sampler other)
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

        public static bool operator ==(Sampler left, Sampler right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Sampler left, Sampler right)
        {
            return !(left == right);
        }
    }
}
