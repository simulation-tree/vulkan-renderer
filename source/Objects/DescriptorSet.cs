using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unmanaged;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    [SkipLocalsInit]
    public unsafe struct DescriptorSet : IEquatable<DescriptorSet>, IDisposable
    {
        public readonly DescriptorPool pool;

        private readonly VkDescriptorSet value;
        private bool valid;

        public readonly VkDescriptorSet Value
        {
            get
            {
                ThrowIfDisposed();

                return value;
            }
        }

        public readonly LogicalDevice LogicalDevice
        {
            get
            {
                ThrowIfDisposed();

                return pool.logicalDevice;
            }
        }

        public readonly bool IsDisposed => !valid;

        internal DescriptorSet(DescriptorPool pool, VkDescriptorSet value)
        {
            this.pool = pool;
            this.value = value;
            valid = true;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(DescriptorSet));
            }
        }

        /// <summary>
        /// Updates the contents of the descriptor set with the buffer data.
        /// </summary>
        public readonly void Update(Buffer buffer, byte binding = 0)
        {
            ThrowIfDisposed();

            VkDescriptorBufferInfo bufferInfo = new();
            bufferInfo.buffer = buffer.Value;
            bufferInfo.offset = 0;
            bufferInfo.range = buffer.size;

            USpan<VkWriteDescriptorSet> descriptorWrite = stackalloc VkWriteDescriptorSet[1];
            descriptorWrite[0] = new()
            {
                dstSet = value,
                dstBinding = binding,
                dstArrayElement = 0,
                descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                descriptorCount = 1,
                pBufferInfo = &bufferInfo
            };

            vkUpdateDescriptorSets(pool.logicalDevice.Value, 1, descriptorWrite, 0, null);
        }

        /// <summary>
        /// Updates the contents of the descriptor set with the image data.
        /// </summary>
        public readonly void Update(ImageView imageView, Sampler sampler, byte binding = 0)
        {
            ThrowIfDisposed();

            VkDescriptorImageInfo imageInfo = new();
            imageInfo.imageView = imageView.Value;
            imageInfo.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
            imageInfo.sampler = sampler.Value;

            USpan<VkWriteDescriptorSet> descriptorWrite = stackalloc VkWriteDescriptorSet[1];
            descriptorWrite[0] = new()
            {
                dstSet = value,
                dstBinding = binding,
                dstArrayElement = 0,
                descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
                descriptorCount = 1,
                pImageInfo = &imageInfo
            };

            vkUpdateDescriptorSets(pool.logicalDevice.Value, 1, descriptorWrite, 0, null);
        }

        /// <summary>
        /// Updates the contents of the descriptor set with a range of buffers.
        /// </summary>
        public readonly void Update(USpan<Buffer> buffers, byte startBinding = 0)
        {
            ThrowIfDisposed();

            USpan<VkWriteDescriptorSet> descriptorWrite = stackalloc VkWriteDescriptorSet[(int)buffers.Length];
            for (uint i = 0; i < buffers.Length; i++)
            {
                Buffer buffer = buffers[i];
                VkDescriptorBufferInfo bufferInfo = new();
                bufferInfo.buffer = buffer.Value;
                bufferInfo.offset = 0;
                bufferInfo.range = buffer.size;

                descriptorWrite[i] = new()
                {
                    dstSet = value,
                    dstBinding = startBinding,
                    dstArrayElement = 0,
                    descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                    descriptorCount = 1,
                    pBufferInfo = &bufferInfo
                };

                startBinding++;
            }

            vkUpdateDescriptorSets(pool.logicalDevice.Value, buffers.Length, descriptorWrite, 0, null);
        }

        /// <summary>
        /// Updates the contents of the descriptor set with a range of image views and samplers.
        /// </summary>
        public readonly void Update(USpan<ImageView> imageViews, USpan<Sampler> samplers, byte startBinding = 0)
        {
            ThrowIfDisposed();

            USpan<VkWriteDescriptorSet> descriptorWrite = stackalloc VkWriteDescriptorSet[(int)imageViews.Length];
            for (uint i = 0; i < imageViews.Length; i++)
            {
                VkDescriptorImageInfo imageInfo = new();
                imageInfo.imageView = imageViews[i].Value;
                imageInfo.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
                imageInfo.sampler = samplers[i].Value;

                descriptorWrite[i] = new()
                {
                    dstSet = value,
                    dstBinding = startBinding,
                    dstArrayElement = 0,
                    descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
                    descriptorCount = 1,
                    pImageInfo = &imageInfo
                };

                startBinding++;
            }

            vkUpdateDescriptorSets(pool.logicalDevice.Value, imageViews.Length, descriptorWrite, 0, null);
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkFreeDescriptorSets(pool.logicalDevice.Value, pool.Value, value);
            valid = false;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is DescriptorSet set && Equals(set);
        }

        public readonly bool Equals(DescriptorSet other)
        {
            if (IsDisposed && other.IsDisposed)
            {
                return true;
            }

            return value == other.value;
        }

        public readonly override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public static bool operator ==(DescriptorSet left, DescriptorSet right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DescriptorSet left, DescriptorSet right)
        {
            return !(left == right);
        }
    }
}