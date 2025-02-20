using Materials;
using System;
using Vortice.Vulkan;

namespace Vulkan
{
    public struct SamplerCreateParameters : IEquatable<SamplerCreateParameters>
    {
        public VkFilter magFilter;
        public VkFilter minFilter;
        public VkSamplerMipmapMode mipmapMode;
        public bool anisotropy;
        public float maxAnisotropy;
        public VkSamplerAddressMode addressModeX;
        public VkSamplerAddressMode addressModeY;
        public VkSamplerAddressMode addressModeW;
        public float mipLoadBias;
        public float minLod;
        public float maxLod;
        public CompareOperation compareOperation;
        public bool compareEnable;

        /// <summary>
        /// Default paremeters for sampler creation.
        /// </summary>
        public SamplerCreateParameters() : this(VkFilter.Linear, VkSamplerAddressMode.Repeat)
        {
        }

        /// <summary>
        /// Default paremeters for sampler creation.
        /// </summary>
        public SamplerCreateParameters(VkFilter filter, VkSamplerAddressMode addressMode)
        {
            minFilter = filter;
            magFilter = filter;
            mipmapMode = filter == VkFilter.Nearest ? VkSamplerMipmapMode.Nearest : VkSamplerMipmapMode.Linear;
            anisotropy = true;
            maxAnisotropy = 16.0f;
            addressModeX = addressMode;
            addressModeY = addressMode;
            addressModeW = addressMode;
            mipLoadBias = 0.0f;
            minLod = 0.0f;
            maxLod = 0.0f;
            compareOperation = CompareOperation.Always;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is SamplerCreateParameters info && Equals(info);
        }

        public readonly bool Equals(SamplerCreateParameters other)
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
                   compareOperation == other.compareOperation &&
                   compareEnable == other.compareEnable;
        }

        public readonly override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + magFilter.GetHashCode();
                hash = hash * 23 + minFilter.GetHashCode();
                hash = hash * 23 + mipmapMode.GetHashCode();
                hash = hash * 23 + anisotropy.GetHashCode();
                hash = hash * 23 + maxAnisotropy.GetHashCode();
                hash = hash * 23 + addressModeX.GetHashCode();
                hash = hash * 23 + addressModeY.GetHashCode();
                hash = hash * 23 + mipLoadBias.GetHashCode();
                hash = hash * 23 + minLod.GetHashCode();
                hash = hash * 23 + maxLod.GetHashCode();
                hash = hash * 23 + compareOperation.GetHashCode();
                hash = hash * 23 + compareEnable.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(SamplerCreateParameters left, SamplerCreateParameters right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SamplerCreateParameters left, SamplerCreateParameters right)
        {
            return !(left == right);
        }
    }
}
