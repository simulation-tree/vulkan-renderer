using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    [SkipLocalsInit]
    public unsafe struct DescriptorSetLayout : IDisposable, IEquatable<DescriptorSetLayout>
    {
        public readonly LogicalDevice logicalDevice;

        internal VkDescriptorSetLayout value;

        public readonly bool IsDisposed => value.IsNull;

        public DescriptorSetLayout(LogicalDevice logicalDevice, ReadOnlySpan<DescriptorSetLayoutBinding> bindings)
        {
            this.logicalDevice = logicalDevice;

            VkDescriptorSetLayoutCreateInfo createInfo = new();
            createInfo.bindingCount = (uint)bindings.Length;
            createInfo.pBindings = (VkDescriptorSetLayoutBinding*)bindings.GetPointer();

            VkResult result = vkCreateDescriptorSetLayout(logicalDevice.value, &createInfo, null, out value);
            ThrowIfFailedToCreate(result);
        }

        public DescriptorSetLayout(LogicalDevice logicalDevice, uint binding, VkDescriptorType type, VkShaderStageFlags stageFlags)
        {
            this.logicalDevice = logicalDevice;

            VkDescriptorSetLayoutBinding layoutBinding = new()
            {
                binding = binding,
                descriptorType = type,
                descriptorCount = 1,
                stageFlags = stageFlags
            };

            VkDescriptorSetLayoutCreateInfo createInfo = new();
            createInfo.bindingCount = 1;
            createInfo.pBindings = &layoutBinding;

            VkResult result = vkCreateDescriptorSetLayout(logicalDevice.value, &createInfo, null, out value);
            ThrowIfFailedToCreate(result);
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(DescriptorSetLayout));
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkDestroyDescriptorSetLayout(logicalDevice.value, value);
            value = default;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is DescriptorSetLayout layout && Equals(layout);
        }

        public readonly bool Equals(DescriptorSetLayout other)
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
                throw new InvalidOperationException("Failed to create descriptor set layout");
            }
        }

        public static bool operator ==(DescriptorSetLayout left, DescriptorSetLayout right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DescriptorSetLayout left, DescriptorSetLayout right)
        {
            return !(left == right);
        }
    }
}