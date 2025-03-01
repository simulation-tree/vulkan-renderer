using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unmanaged;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    [SkipLocalsInit]
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

        public DescriptorSetLayout(LogicalDevice logicalDevice, USpan<VkDescriptorSetLayoutBinding> bindings)
        {
            this.logicalDevice = logicalDevice;

            VkDescriptorSetLayoutCreateInfo createInfo = new();
            createInfo.bindingCount = bindings.Length;
            createInfo.pBindings = bindings.Pointer;

            VkResult result = vkCreateDescriptorSetLayout(logicalDevice.Value, &createInfo, null, out value);
            ThrowIfFailedToCreate(result);

            valid = true;
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

            VkResult result = vkCreateDescriptorSetLayout(logicalDevice.Value, &createInfo, null, out value);
            ThrowIfFailedToCreate(result);

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

        [Conditional("DEBUG")]
        private readonly void ThrowIfFailedToCreate(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException("Failed to create descriptor set layout");
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