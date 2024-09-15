using System;
using System.Diagnostics;
using Unmanaged;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct DescriptorSetLayout : IDisposable, IEquatable<DescriptorSetLayout>
    {
        public readonly LogicalDevice logicalDevice;

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

        public DescriptorSetLayout(LogicalDevice device, USpan<(byte binding, VkDescriptorType type, VkShaderStageFlags stage)> bindings)
        {
            this.logicalDevice = device;

            VkDescriptorSetLayoutBinding* layoutBindings = stackalloc VkDescriptorSetLayoutBinding[(int)bindings.Length];
            for (uint i = 0; i < bindings.Length; i++)
            {
                (byte binding, VkDescriptorType type, VkShaderStageFlags stage) = bindings[i];
                layoutBindings[i] = new()
                {
                    binding = binding,
                    descriptorType = type,
                    descriptorCount = 1,
                    stageFlags = stage
                };
            }

            VkDescriptorSetLayoutCreateInfo createInfo = new();
            createInfo.bindingCount = bindings.Length;
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
            this.logicalDevice = device;

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
            vkDestroyDescriptorSetLayout(logicalDevice.Value, value);
            valid = false;
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
