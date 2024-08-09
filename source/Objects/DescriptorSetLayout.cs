using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct DescriptorSetLayout : IDisposable, IEquatable<DescriptorSetLayout>
    {
        public readonly LogicalDevice device;

        private readonly VkDescriptorSetLayout value;
        private bool valid;

        public readonly VkDescriptorSetLayout Value
        {
            get
            {
                ThrowIfDisposed();
                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        public DescriptorSetLayout(LogicalDevice device, ReadOnlySpan<(uint binding, VkDescriptorType type, VkShaderStageFlags stage)> bindings)
        {
            this.device = device;

            VkDescriptorSetLayoutBinding* layoutBindings = stackalloc VkDescriptorSetLayoutBinding[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                (uint binding, VkDescriptorType type, VkShaderStageFlags stage) = bindings[i];
                layoutBindings[i] = new()
                {
                    binding = (uint)i,
                    descriptorType = type,
                    descriptorCount = 1,
                    stageFlags = stage
                };
            }

            VkDescriptorSetLayoutCreateInfo createInfo = new();
            createInfo.bindingCount = (uint)bindings.Length;
            createInfo.pBindings = layoutBindings;

            VkResult result = vkCreateDescriptorSetLayout(device.Value, &createInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException("Failed to create descriptor set layout");
            }

            valid = true;
        }

        public DescriptorSetLayout(LogicalDevice device, uint binding, VkDescriptorType type, VkShaderStageFlags stageFlags)
        {
            this.device = device;

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

            VkResult result = vkCreateDescriptorSetLayout(device.Value, &createInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException("Failed to create descriptor set layout");
            }

            valid = true;
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
            vkDestroyDescriptorSetLayout(device.Value, value);
            valid = false;
        }

        public override bool Equals(object? obj)
        {
            return obj is DescriptorSetLayout layout && Equals(layout);
        }

        public bool Equals(DescriptorSetLayout other)
        {
            return value.Equals(other.value);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(value);
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
