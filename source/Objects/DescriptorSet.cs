using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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

            Span<VkWriteDescriptorSet> descriptorWrite = stackalloc VkWriteDescriptorSet[1];
            descriptorWrite[0] = new()
            {
                dstSet = value,
                dstBinding = binding,
                dstArrayElement = 0,
                descriptorType = VK_DESCRIPTOR_TYPE_UNIFORM_BUFFER,
                descriptorCount = 1,
                pBufferInfo = &bufferInfo
            };

            vkUpdateDescriptorSets(pool.logicalDevice.Value, 1, descriptorWrite.GetPointer(), 0, null);
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

            Span<VkWriteDescriptorSet> descriptorWrite = stackalloc VkWriteDescriptorSet[1];
            descriptorWrite[0] = new()
            {
                dstSet = value,
                dstBinding = binding,
                dstArrayElement = 0,
                descriptorType = VK_DESCRIPTOR_TYPE_COMBINED_IMAGE_SAMPLER,
                descriptorCount = 1,
                pImageInfo = &imageInfo
            };

            vkUpdateDescriptorSets(pool.logicalDevice.Value, 1, descriptorWrite.GetPointer(), 0, null);
        }

        /// <summary>
        /// Updates the contents of the descriptor set with a range of buffers.
        /// </summary>
        public readonly void Update(ReadOnlySpan<Buffer> buffers, byte startBinding = 0)
        {
            ThrowIfDisposed();

            Span<VkWriteDescriptorSet> descriptorWrite = stackalloc VkWriteDescriptorSet[buffers.Length];
            for (int i = 0; i < buffers.Length; i++)
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

            vkUpdateDescriptorSets(pool.logicalDevice.Value, (uint)buffers.Length, descriptorWrite.GetPointer(), 0, null);
        }

        /// <summary>
        /// Updates the contents of the descriptor set with a range of image views and samplers.
        /// </summary>
        public readonly void Update(ReadOnlySpan<ImageView> imageViews, ReadOnlySpan<Sampler> samplers, byte startBinding = 0)
        {
            ThrowIfDisposed();

            Span<VkWriteDescriptorSet> descriptorWrite = stackalloc VkWriteDescriptorSet[imageViews.Length];
            for (int i = 0; i < imageViews.Length; i++)
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

            vkUpdateDescriptorSets(pool.logicalDevice.Value, (uint)imageViews.Length, descriptorWrite.GetPointer(), 0, null);
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
            if (!valid && !other.valid)
            {
                return true;
            }
            else if (valid != other.valid)
            {
                return false;
            }

            return value == other.value;
        }

        public readonly override int GetHashCode()
        {
            if (valid)
            {
                return value.GetHashCode();
            }
            else
            {
                return 0;
            }
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