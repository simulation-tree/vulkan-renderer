using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Sampler : IDisposable, IEquatable<Sampler>
    {
        public readonly LogicalDevice logicalDevice;

        internal VkSampler value;

        public readonly bool IsDisposed => value.IsNull;

        public Sampler(LogicalDevice logicalDevice, SamplerCreateParameters createInfo)
        {
            VkSamplerCreateInfo vkCreateInfo = new();
            vkCreateInfo.magFilter = createInfo.magFilter;
            vkCreateInfo.minFilter = createInfo.minFilter;
            vkCreateInfo.mipmapMode = createInfo.mipmapMode;
            vkCreateInfo.addressModeU = createInfo.addressModeX;
            vkCreateInfo.addressModeV = createInfo.addressModeY;
            vkCreateInfo.addressModeW = createInfo.addressModeW;
            vkCreateInfo.anisotropyEnable = createInfo.anisotropy;

            VkPhysicalDeviceProperties properties = logicalDevice.physicalDevice.GetProperties();
            vkCreateInfo.maxAnisotropy = properties.limits.maxSamplerAnisotropy;
            vkCreateInfo.borderColor = VkBorderColor.IntOpaqueBlack;
            vkCreateInfo.unnormalizedCoordinates = false;
            vkCreateInfo.compareEnable = createInfo.compareEnable;
            vkCreateInfo.compareOp = (VkCompareOp)createInfo.compareOperation;
            vkCreateInfo.mipLodBias = createInfo.mipLoadBias;
            vkCreateInfo.minLod = createInfo.minLod;
            vkCreateInfo.maxLod = createInfo.maxLod;

            VkResult result = vkCreateSampler(logicalDevice.value, &vkCreateInfo, null, out value);
            ThrowIfUnableToCreate(result);

            this.logicalDevice = logicalDevice;
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkDestroySampler(logicalDevice.value, value);
            value = default;
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
                throw new InvalidOperationException($"Failed to create sampler: {result}");
            }
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