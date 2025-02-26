using Collections.Generic;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unmanaged;
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

        private readonly VkDescriptorPool value;
        private bool valid;

        public readonly VkDescriptorPool Value
        {
            get
            {
                ThrowIfDisposed();
                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        /// <summary>
        /// Creates a descriptor pool object that can allocate descriptor sets.
        /// </summary>
        public DescriptorPool(LogicalDevice logicalDevice, uint maxAllocations, uint poolSizeCount, VkDescriptorType type)
        {
            if (maxAllocations == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAllocations));
            }

            this.logicalDevice = logicalDevice;
            USpan<VkDescriptorPoolSize> poolSizes = stackalloc VkDescriptorPoolSize[1] { new(type, maxAllocations) };
            VkDescriptorPoolCreateInfo createInfo = new()
            {
                poolSizeCount = poolSizeCount,
                pPoolSizes = poolSizes,
                maxSets = maxAllocations,
                flags = VkDescriptorPoolCreateFlags.FreeDescriptorSet
            };

            VkResult result = vkCreateDescriptorPool(logicalDevice.Value, &createInfo, null, out value);
            ThrowIfFailedToCreate(result);

            valid = true;
        }

        public DescriptorPool(LogicalDevice logicalDevice, USpan<(VkDescriptorType type, uint descriptorCount)> pools, uint maxSets)
        {
            if (pools.Length == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pools));
            }

            this.logicalDevice = logicalDevice;
            USpan<VkDescriptorPoolSize> poolSizes = stackalloc VkDescriptorPoolSize[(int)pools.Length];
            for (uint i = 0; i < pools.Length; i++)
            {
                (VkDescriptorType type, uint descriptorCount) = pools[i];
                poolSizes[i] = new(type, descriptorCount);
            }

            VkDescriptorPoolCreateInfo createInfo = new()
            {
                poolSizeCount = pools.Length,
                pPoolSizes = poolSizes,
                maxSets = maxSets,
                flags = VkDescriptorPoolCreateFlags.FreeDescriptorSet
            };

            VkResult result = vkCreateDescriptorPool(logicalDevice.Value, &createInfo, null, out value);
            ThrowIfFailedToCreate(result);

            valid = true;
        }

        public void Dispose()
        {
            ThrowIfDisposed();
            vkDestroyDescriptorPool(logicalDevice.Value, value);
            valid = false;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(DescriptorPool));
            }
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

        /// <summary>
        /// Allocates as many new descriptor sets from the pool, as there are layouts given.
        /// </summary>
        public readonly Array<DescriptorSet> Allocate(USpan<DescriptorSetLayout> layouts)
        {
            ThrowIfDisposed();

            USpan<VkDescriptorSetLayout> layoutPointers = stackalloc VkDescriptorSetLayout[(int)layouts.Length];
            for (uint i = 0; i < layouts.Length; i++)
            {
                layoutPointers[i] = layouts[i].Value;
            }

            VkDescriptorSetAllocateInfo allocateInfo = new()
            {
                descriptorPool = value,
                descriptorSetCount = layouts.Length,
                pSetLayouts = layoutPointers
            };

            USpan<VkDescriptorSet> descriptorSet = stackalloc VkDescriptorSet[(int)layouts.Length];
            VkResult result = vkAllocateDescriptorSets(logicalDevice.Value, &allocateInfo, descriptorSet);
            ThrowIfFailedToAllocate(result);

            Array<DescriptorSet> sets = new(layouts.Length);
            for (uint i = 0; i < layouts.Length; i++)
            {
                sets[i] = new(this, descriptorSet[i]);
            }

            return sets;
        }

        public readonly DescriptorSet Allocate(DescriptorSetLayout setLayout)
        {
            ThrowIfDisposed();

            VkDescriptorSetLayout layout = setLayout.Value;
            VkDescriptorSetAllocateInfo allocateInfo = new()
            {
                descriptorPool = value,
                descriptorSetCount = 1,
                pSetLayouts = &layout
            };

            VkDescriptorSet descriptorSet;
            VkResult result = vkAllocateDescriptorSets(logicalDevice.Value, &allocateInfo, &descriptorSet);
            ThrowIfFailedToAllocate(result);

            return new(this, descriptorSet);
        }

        /// <summary>
        /// Attempts to allocate a new instance from the pool, assuming the given layout
        /// is one that the pool can support.
        /// </summary>
        /// <returns><c>true</c> when successful, otherwise the pool is out of memory.</returns>
        public readonly bool TryAllocate(DescriptorSetLayout layout, out DescriptorSet set)
        {
            ThrowIfDisposed();

            VkDescriptorSetLayout vkValue = layout.Value;
            VkDescriptorSetAllocateInfo allocateInfo = new()
            {
                descriptorPool = value,
                descriptorSetCount = 1,
                pSetLayouts = &vkValue
            };

            VkDescriptorSet descriptorSet;
            VkResult result = vkAllocateDescriptorSets(logicalDevice.Value, &allocateInfo, &descriptorSet);
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
            if (IsDisposed && other.IsDisposed)
            {
                return true;
            }

            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(value, IsDisposed);
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