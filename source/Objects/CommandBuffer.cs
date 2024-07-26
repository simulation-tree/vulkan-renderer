using System;
using System.Diagnostics;
using System.Numerics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct CommandBuffer : IDisposable, IEquatable<CommandBuffer>
    {
        public readonly CommandPool commandPool;

        private readonly VkCommandBuffer value;
        private bool valid;

        public readonly VkCommandBuffer Value
        {
            get
            {
                ThrowIfDisposed();
                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        public CommandBuffer(CommandPool commandPool)
        {
            VkCommandBufferAllocateInfo commandBufferAllocateInfo = new()
            {
                commandPool = commandPool.Value,
                level = VkCommandBufferLevel.Primary,
                commandBufferCount = 1
            };

            this.commandPool = commandPool;
            VkResult result = vkAllocateCommandBuffer(commandPool.logicalDevice.Value, &commandBufferAllocateInfo, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to allocate command buffer: {result}");
            }

            valid = true;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(CommandBuffer));
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();
            vkFreeCommandBuffers(commandPool.logicalDevice.Value, commandPool.Value, value);
            valid = false;
        }

        public readonly void Reset()
        {
            ThrowIfDisposed();
            VkResult result = vkResetCommandBuffer(value, VkCommandBufferResetFlags.None);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to reset command buffer: {result}");
            }
        }

        public readonly void Begin(VkCommandBufferUsageFlags usage)
        {
            ThrowIfDisposed();
            VkCommandBufferBeginInfo commandBufferBeginInfo = new()
            {
                flags = usage
            };

            VkResult result = vkBeginCommandBuffer(value, &commandBufferBeginInfo);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to begin command buffer: {result}");
            }
        }

        public readonly void End()
        {
            ThrowIfDisposed();
            VkResult result = vkEndCommandBuffer(value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to end command buffer: {result}");
            }
        }

        public readonly void SetViewport(Vector4 rect, float minDepth = 0f, float maxDepth = 1f)
        {
            ThrowIfDisposed();
            VkViewport viewport = new(rect.X, rect.Y, rect.Z, rect.W, minDepth, maxDepth);
            vkCmdSetViewport(value, 0, 1, &viewport);
        }

        public readonly void SetScissor(Vector4 rect)
        {
            ThrowIfDisposed();
            VkRect2D rectValue = new((int)rect.X, (int)rect.Y, (uint)rect.Z, (uint)rect.W);
            vkCmdSetScissor(value, 0, 1, &rectValue);
        }

        public readonly void TransitionImageLayout(Image image, VkImageLayout currentLayout, VkImageLayout newLayout, VkImageAspectFlags aspects = VkImageAspectFlags.Color)
        {
            ThrowIfDisposed();
            VkImageMemoryBarrier barrier = new()
            {
                oldLayout = currentLayout,
                newLayout = newLayout,
                srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
                image = image.Value,
                subresourceRange = new VkImageSubresourceRange(aspects, 0, 1, 0, 1)
            };

            VkPipelineStageFlags sourceStage;
            VkPipelineStageFlags destinationStage;

            if (currentLayout == VkImageLayout.Undefined && newLayout == VkImageLayout.TransferDstOptimal)
            {
                barrier.srcAccessMask = 0;
                barrier.dstAccessMask = VkAccessFlags.TransferWrite;

                sourceStage = VkPipelineStageFlags.TopOfPipe;
                destinationStage = VkPipelineStageFlags.Transfer;
            }
            else if (currentLayout == VkImageLayout.Undefined && newLayout == VkImageLayout.DepthStencilAttachmentOptimal)
            {
                barrier.srcAccessMask = 0;
                barrier.dstAccessMask = VkAccessFlags.DepthStencilAttachmentRead | VkAccessFlags.DepthStencilAttachmentWrite;

                sourceStage = VkPipelineStageFlags.TopOfPipe;
                destinationStage = VkPipelineStageFlags.EarlyFragmentTests;
            }
            else if (currentLayout == VkImageLayout.TransferDstOptimal && newLayout == VkImageLayout.ShaderReadOnlyOptimal)
            {
                barrier.srcAccessMask = VkAccessFlags.TransferWrite;
                barrier.dstAccessMask = VkAccessFlags.ShaderRead;

                sourceStage = VkPipelineStageFlags.Transfer;
                destinationStage = VkPipelineStageFlags.FragmentShader;
            }
            else
            {
                throw new InvalidOperationException("Unsupported layout transition");
            }

            vkCmdPipelineBarrier(value, sourceStage, destinationStage, 0, 0, null, 0, null, 1, &barrier);
        }

        public readonly void BindPipeline(Pipeline pipeline, VkPipelineBindPoint point)
        {
            ThrowIfDisposed();
            vkCmdBindPipeline(value, point, pipeline.Value);
        }

        public readonly void BindVertexBuffer(VertexBuffer vertexBuffer, uint binding = 0, uint offset = 0)
        {
            ThrowIfDisposed();
            vkCmdBindVertexBuffer(value, binding, vertexBuffer.bufferDeviceMemory.buffer.Value, offset);
        }

        public readonly void BindIndexBuffer(IndexBuffer indexBuffer, uint offset = 0)
        {
            ThrowIfDisposed();
            vkCmdBindIndexBuffer(value, indexBuffer.bufferDeviceMemory.buffer.Value, offset, VkIndexType.Uint32);
        }

        public readonly void BindDescriptorSets(PipelineLayout layout, ReadOnlySpan<DescriptorSet> descriptorSets, uint set)
        {
            ThrowIfDisposed();
            VkDescriptorSet* descriptorSetValue = stackalloc VkDescriptorSet[descriptorSets.Length];
            for (int i = 0; i < descriptorSets.Length; i++)
            {
                descriptorSetValue[i] = descriptorSets[i].Value;
            }

            vkCmdBindDescriptorSets(value, VkPipelineBindPoint.Graphics, layout.Value, set, new ReadOnlySpan<VkDescriptorSet>(descriptorSetValue, descriptorSets.Length));
        }

        public readonly void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0)
        {
            ThrowIfDisposed();
            vkCmdDrawIndexed(value, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
        }

        public readonly void CopyBufferTo(Buffer source, Buffer destination)
        {
            VkBufferCopy region = default;
            region.dstOffset = 0;
            region.srcOffset = 0;
            region.size = source.size;
            vkCmdCopyBuffer(value, source.Value, destination.Value, 1, &region);
        }

        /// <summary>
        /// Copies data from the buffer into the image.
        /// </summary>
        public readonly void CopyBufferToImage(Buffer sourceBuffer, Image destinationImage, uint depth = 1)
        {
            ThrowIfDisposed();
            VkBufferImageCopy region = new()
            {
                bufferOffset = 0,
                bufferRowLength = 0,
                bufferImageHeight = 0,
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                imageOffset = new VkOffset3D(0, 0, 0),
                imageExtent = new VkExtent3D(destinationImage.width, destinationImage.height, depth)
            };

            vkCmdCopyBufferToImage(value, sourceBuffer.Value, destinationImage.Value, VkImageLayout.TransferDstOptimal, 1, &region);
        }

        /// <summary>
        /// Copies data from the buffer with the given coordinates into the image.
        /// <para>
        /// Assumes the source buffer is 4 bytes per pixel.
        /// </para>
        /// </summary>
        public readonly void CopyBufferToImage(Buffer sourceBuffer, uint width, uint height, uint x, uint y, Image destinationImage, uint depth = 1)
        {
            ThrowIfDisposed();
            VkBufferImageCopy bufferImageCopy = new()
            {
                bufferOffset = ((width * y) + x) * 4,
                bufferRowLength = width,
                bufferImageHeight = height,
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                imageOffset = new VkOffset3D(0, 0, 0),
                imageExtent = new VkExtent3D(destinationImage.width, destinationImage.height, depth)
            };

            vkCmdCopyBufferToImage(value, sourceBuffer.Value, destinationImage.Value, VkImageLayout.TransferDstOptimal, 1, &bufferImageCopy);
        }

        public readonly void CopyBufferTo(BufferDeviceMemory source, BufferDeviceMemory destination)
        {
            ThrowIfDisposed();
            VkBufferCopy region = default;
            region.dstOffset = 0;
            region.srcOffset = 0;
            region.size = source.buffer.size;
            vkCmdCopyBuffer(value, source.buffer.Value, destination.buffer.Value, 1, &region);
        }

        public readonly void CopyBufferTo(Buffer source, Buffer destination, ulong size)
        {
            ThrowIfDisposed();
            VkBufferCopy region = default;
            region.dstOffset = 0;
            region.srcOffset = 0;
            region.size = size;
            vkCmdCopyBuffer(value, source.Value, destination.Value, 1, &region);
        }

        public readonly void CopyBufferTo(BufferDeviceMemory source, BufferDeviceMemory destination, ulong size)
        {
            ThrowIfDisposed();
            VkBufferCopy region = default;
            region.dstOffset = 0;
            region.srcOffset = 0;
            region.size = size;
            vkCmdCopyBuffer(value, source.buffer.Value, destination.buffer.Value, 1, &region);
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is CommandBuffer buffer && Equals(buffer);
        }

        public readonly bool Equals(CommandBuffer other)
        {
            if (IsDisposed && other.IsDisposed)
            {
                return true;
            }

            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(value);
        }

        public static bool operator ==(CommandBuffer left, CommandBuffer right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CommandBuffer left, CommandBuffer right)
        {
            return !(left == right);
        }
    }
}
