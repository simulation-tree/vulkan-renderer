using Collections.Generic;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    /// <summary>
    /// Maintains a pool of descriptors, from which <see cref="DescriptorSet"/>
    /// instances are allocated.
    /// </summary>
    [SkipLocalsInit]
    public unsafe struct DescriptorPool : IDisposable, IEquatable<DescriptorPool>
    {
        public readonly LogicalDevice logicalDevice;

        internal VkDescriptorPool value;

        public readonly bool IsDisposed => value.IsNull;

        /// <summary>
        /// Creates a descriptor pool object that can allocate descriptor sets.
        /// </summary>
        public DescriptorPool(LogicalDevice logicalDevice, VkDescriptorType descriptorType, uint descriptorCount, uint poolSizeCount, uint maxSets)
        {
            if (descriptorCount == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(descriptorCount));
            }

            this.logicalDevice = logicalDevice;
            Span<VkDescriptorPoolSize> poolSizes = stackalloc VkDescriptorPoolSize[1] { new(descriptorType, descriptorCount) };
            VkDescriptorPoolCreateInfo createInfo = new()
            {
                poolSizeCount = poolSizeCount,
                pPoolSizes = poolSizes.GetPointer(),
                maxSets = maxSets,
                flags = VkDescriptorPoolCreateFlags.FreeDescriptorSet
            };

            VkResult result = vkCreateDescriptorPool(logicalDevice.value, &createInfo, null, out value);
            ThrowIfFailedToCreate(result);
        }

        public DescriptorPool(LogicalDevice logicalDevice, ReadOnlySpan<DescriptorPoolSize> poolSizes, uint maxSets)
        {
            if (poolSizes.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(poolSizes));
            }

            this.logicalDevice = logicalDevice;
            VkDescriptorPoolCreateInfo createInfo = new()
            {
                poolSizeCount = (uint)poolSizes.Length,
                pPoolSizes = (VkDescriptorPoolSize*)poolSizes.GetPointer(),
                maxSets = maxSets,
                flags = VkDescriptorPoolCreateFlags.FreeDescriptorSet
            };

            VkResult result = vkCreateDescriptorPool(logicalDevice.value, &createInfo, null, out value);
            ThrowIfFailedToCreate(result);
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkDestroyDescriptorPool(logicalDevice.value, value);
            value = default;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(DescriptorPool));
            }
        }

        /// <summary>
        /// Allocates as many new descriptor sets from the pool, as there are layouts given.
        /// </summary>
        public readonly Array<DescriptorSet> Allocate(ReadOnlySpan<DescriptorSetLayout> layouts)
        {
            ThrowIfDisposed();

            Span<VkDescriptorSetLayout> layoutPointers = stackalloc VkDescriptorSetLayout[layouts.Length];
            for (int i = 0; i < layouts.Length; i++)
            {
                layoutPointers[i] = layouts[i].value;
            }

            VkDescriptorSetAllocateInfo allocateInfo = new()
            {
                descriptorPool = value,
                descriptorSetCount = (uint)layouts.Length,
                pSetLayouts = layoutPointers.GetPointer()
            };

            Span<VkDescriptorSet> descriptorSet = stackalloc VkDescriptorSet[layouts.Length];
            VkResult result = vkAllocateDescriptorSets(logicalDevice.value, &allocateInfo, descriptorSet.GetPointer());
            ThrowIfFailedToAllocate(result);

            Array<DescriptorSet> sets = new(layouts.Length);
            for (int i = 0; i < layouts.Length; i++)
            {
                sets[i] = new(this, descriptorSet[i]);
            }

            return sets;
        }

        public readonly DescriptorSet Allocate(DescriptorSetLayout setLayout)
        {
            ThrowIfDisposed();

            VkDescriptorSetLayout layout = setLayout.value;
            VkDescriptorSetAllocateInfo allocateInfo = new()
            {
                descriptorPool = value,
                descriptorSetCount = 1,
                pSetLayouts = &layout
            };

            VkDescriptorSet descriptorSet;
            VkResult result = vkAllocateDescriptorSets(logicalDevice.value, &allocateInfo, &descriptorSet);
            ThrowIfFailedToAllocate(result);

            return new(this, descriptorSet);
        }

        /// <summary>
        /// Attempts to allocate a new instance from the pool, assuming the given layout
        /// is one that the pool can support.
        /// </summary>
        /// <returns><see cref="true"/> when successful, otherwise the pool is out of memory.</returns>
        public readonly bool TryAllocate(DescriptorSetLayout layout, out DescriptorSet set)
        {
            ThrowIfDisposed();

            VkDescriptorSetLayout vkValue = layout.value;
            VkDescriptorSetAllocateInfo allocateInfo = new()
            {
                descriptorPool = value,
                descriptorSetCount = 1,
                pSetLayouts = &vkValue
            };

            VkDescriptorSet descriptorSet;
            VkResult result = vkAllocateDescriptorSets(logicalDevice.value, &allocateInfo, &descriptorSet);
            ThrowIfFailedToAllocateOrNoMemoryLeft(result);

            if (result == VkResult.Success)
            {
                set = new(this, descriptorSet);
                return true;
            }

            set = default;
            return false;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is DescriptorPool pool && Equals(pool);
        }

        public readonly bool Equals(DescriptorPool other)
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
                throw new InvalidOperationException($"Failed to create descriptor pool {result}");
            }
        }

        [Conditional("DEBUG")]
        private static void ThrowIfFailedToAllocate(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException($"Failed to allocate descriptor sets: {result}");
            }
        }

        [Conditional("DEBUG")]
        private static void ThrowIfFailedToAllocateOrNoMemoryLeft(VkResult result)
        {
            if (result != VkResult.Success && result != VkResult.ErrorOutOfPoolMemory)
            {
                throw new InvalidOperationException($"Failed to allocate descriptor sets: {result}");
            }
        }

        public static bool operator ==(DescriptorPool left, DescriptorPool right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DescriptorPool left, DescriptorPool right)
        {
            return !(left == right);
        }
    }
}