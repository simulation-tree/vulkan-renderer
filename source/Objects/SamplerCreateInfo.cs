using System;
using Vortice.Vulkan;

namespace Vulkan
{
    public struct SamplerCreateInfo : IEquatable<SamplerCreateInfo>
    {
        public VkFilter magFilter;
        public VkFilter minFilter;
        public VkSamplerMipmapMode mipmapMode;
        public bool anisotropy;
        public float maxAnisotropy;
        public VkSamplerAddressMode addressModeX;
        public VkSamplerAddressMode addressModeY;
        public float mipLoadBias;
        public float minLod;
        public float maxLod;
        public VkCompareOp compareOp;
        public bool compareEnable;

        public SamplerCreateInfo()
        {
            minFilter = VkFilter.Linear;
            magFilter = VkFilter.Linear;
            mipmapMode = VkSamplerMipmapMode.Linear;
            anisotropy = true;
            maxAnisotropy = 16.0f;
            addressModeX = VkSamplerAddressMode.Repeat;
            addressModeY = VkSamplerAddressMode.Repeat;
            mipLoadBias = 0.0f;
            minLod = 0.0f;
            maxLod = 0.0f;
            compareOp = VkCompareOp.Always;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is SamplerCreateInfo info && Equals(info);
        }

        public readonly bool Equals(SamplerCreateInfo other)
        {
            return magFilter == other.magFilter &&
                   minFilter == other.minFilter &&
                   mipmapMode == other.mipmapMode &&
                   anisotropy == other.anisotropy &&
                   maxAnisotropy == other.maxAnisotropy &&
                   addressModeX == other.addressModeX &&
                   addressModeY == other.addressModeY &&
                   mipLoadBias == other.mipLoadBias &&
                   minLod == other.minLod &&
                   maxLod == other.maxLod &&
                   compareOp == other.compareOp &&
                   compareEnable == other.compareEnable;
        }

        public readonly override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(magFilter);
            hash.Add(minFilter);
            hash.Add(mipmapMode);
            hash.Add(anisotropy);
            hash.Add(maxAnisotropy);
            hash.Add(addressModeX);
            hash.Add(addressModeY);
            hash.Add(mipLoadBias);
            hash.Add(minLod);
            hash.Add(maxLod);
            hash.Add(compareOp);
            hash.Add(compareEnable);
            return hash.ToHashCode();
        }

        public static bool operator ==(SamplerCreateInfo left, SamplerCreateInfo right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SamplerCreateInfo left, SamplerCreateInfo right)
        {
            return !(left == right);
        }
    }
}
