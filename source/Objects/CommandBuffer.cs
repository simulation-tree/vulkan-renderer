using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Unmanaged;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    /// <summary>
    /// For recording commands that eventually get submitted to a <see cref="Queue"/>.
    /// </summary>
    [SkipLocalsInit]
    public unsafe struct CommandBuffer : IDisposable, IEquatable<CommandBuffer>
    {
        public readonly CommandPool commandPool;

        internal VkCommandBuffer value;

        public readonly bool IsDisposed => value.IsNull;

        internal CommandBuffer(CommandPool commandPool, VkCommandBuffer value)
        {
            this.commandPool = commandPool;
            this.value = value;
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

            vkFreeCommandBuffers(commandPool.logicalDevice.value, commandPool.value, value);
            value = default;
        }

        /// <summary>
        /// Resets the command buffer back to its initial state.
        /// </summary>
        public readonly void Reset()
        {
            ThrowIfDisposed();

            VkResult result = vkResetCommandBuffer(value, VkCommandBufferResetFlags.None);
            ThrowIfFailedToReset(result);
        }

        public readonly void Begin(bool oneTimeSubmit = true, bool renderPassContinue = false, bool simultaneous = false)
        {
            ThrowIfDisposed();

            VkCommandBufferUsageFlags flags = default;
            if (oneTimeSubmit)
            {
                flags |= VkCommandBufferUsageFlags.OneTimeSubmit;
            }

            if (renderPassContinue)
            {
                flags |= VkCommandBufferUsageFlags.RenderPassContinue;
            }

            if (simultaneous)
            {
                flags |= VkCommandBufferUsageFlags.SimultaneousUse;
            }

            VkCommandBufferBeginInfo commandBufferBeginInfo = new()
            {
                flags = flags
            };

            VkResult result = vkBeginCommandBuffer(value, &commandBufferBeginInfo);
            ThrowIfFailedToBegin(result);
        }

        public readonly void End()
        {
            ThrowIfDisposed();

            VkResult result = vkEndCommandBuffer(value);
            ThrowIfFailedToEnd(result);
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

        public readonly void TransitionImageLayout(Image image, VkImageLayout oldLayout, VkImageLayout newLayout, VkImageAspectFlags aspects = VkImageAspectFlags.Color, uint layerCount = 1)
        {
            ThrowIfDisposed();

            VkImageMemoryBarrier barrier = new()
            {
                oldLayout = oldLayout,
                newLayout = newLayout,
                srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
                image = image.value,
                subresourceRange = new VkImageSubresourceRange(aspects, 0, 1, 0, layerCount)
            };

            VkPipelineStageFlags sourceStage;
            VkPipelineStageFlags destinationStage;

            if (oldLayout == VkImageLayout.Undefined)
            {
                if (newLayout == VkImageLayout.TransferDstOptimal)
                {
                    barrier.srcAccessMask = 0;
                    barrier.dstAccessMask = VkAccessFlags.TransferWrite;
                    sourceStage = VkPipelineStageFlags.TopOfPipe;
                    destinationStage = VkPipelineStageFlags.Transfer;
                }
                else if (newLayout == VkImageLayout.DepthStencilAttachmentOptimal)
                {
                    barrier.srcAccessMask = 0;
                    barrier.dstAccessMask = VkAccessFlags.DepthStencilAttachmentWrite;
                    sourceStage = VkPipelineStageFlags.TopOfPipe;
                    destinationStage = VkPipelineStageFlags.EarlyFragmentTests;
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported layout transition {oldLayout} -> {newLayout}");
                }
            }
            else if (oldLayout == VkImageLayout.TransferDstOptimal && newLayout == VkImageLayout.ShaderReadOnlyOptimal)
            {
                barrier.srcAccessMask = VkAccessFlags.TransferWrite;
                barrier.dstAccessMask = VkAccessFlags.ShaderRead;
                sourceStage = VkPipelineStageFlags.Transfer;
                destinationStage = VkPipelineStageFlags.FragmentShader;
            }
            else
            {
                throw new InvalidOperationException($"Unsupported layout transition {oldLayout} -> {newLayout}");
            }

            vkCmdPipelineBarrier(value, sourceStage, destinationStage, 0, 0, null, 0, null, 1, &barrier);
        }

        /// <summary>
        /// Begins rendering into the given frame buffer.
        /// </summary>
        public readonly void BeginRenderPass(RenderPass renderPass, Framebuffer framebuffer, Vector4 area, Vector4 clearColor, bool withPrimary = true)
        {
            ThrowIfDisposed();

            Span<VkClearValue> clearValue = stackalloc VkClearValue[2];
            clearValue[0].color = new VkClearColorValue(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
            clearValue[1].depthStencil = new VkClearDepthStencilValue(1.0f, 0);
            VkRenderPassBeginInfo renderPassBeginInfo = new()
            {
                renderPass = renderPass.value,
                framebuffer = framebuffer.value,
                renderArea = new VkRect2D((int)area.X, (int)area.Y, (uint)area.Z, (uint)area.W),
                clearValueCount = 2,
                pClearValues = clearValue.GetPointer()
            };

            vkCmdBeginRenderPass(value, &renderPassBeginInfo, withPrimary ? VkSubpassContents.Inline : VkSubpassContents.SecondaryCommandBuffers);
        }

        public readonly void EndRenderPass()
        {
            ThrowIfDisposed();

            vkCmdEndRenderPass(value);
        }

        public readonly void BindPipeline(Pipeline pipeline, VkPipelineBindPoint point)
        {
            ThrowIfDisposed();

            vkCmdBindPipeline(value, point, pipeline.value);
        }

        public readonly void BindVertexBuffer(VertexBuffer vertexBuffer, uint binding = 0, uint offset = 0)
        {
            ThrowIfDisposed();

            vkCmdBindVertexBuffer(value, binding, vertexBuffer.bufferDeviceMemory.buffer.value, offset);
        }

        public readonly void BindIndexBuffer(IndexBuffer indexBuffer, uint offset = 0)
        {
            ThrowIfDisposed();

            vkCmdBindIndexBuffer(value, indexBuffer.bufferDeviceMemory.buffer.value, offset, VkIndexType.Uint32);
        }

        public readonly void BindDescriptorSets(PipelineLayout layout, Span<DescriptorSet> descriptorSets, uint set = 0)
        {
            ThrowIfDisposed();

            Span<VkDescriptorSet> descriptorSetValue = stackalloc VkDescriptorSet[descriptorSets.Length];
            for (int i = 0; i < descriptorSets.Length; i++)
            {
                descriptorSetValue[i] = descriptorSets[i].value;
            }

            vkCmdBindDescriptorSets(value, VkPipelineBindPoint.Graphics, layout.value, set, new ReadOnlySpan<VkDescriptorSet>(descriptorSetValue.GetPointer(), descriptorSets.Length));
        }

        public readonly void BindDescriptorSet(PipelineLayout layout, DescriptorSet descriptorSet, uint set = 0)
        {
            ThrowIfDisposed();

            Span<VkDescriptorSet> descriptorSetValue = stackalloc VkDescriptorSet[1];
            descriptorSetValue[0] = descriptorSet.value;
            vkCmdBindDescriptorSets(value, VkPipelineBindPoint.Graphics, layout.value, set, new ReadOnlySpan<VkDescriptorSet>(descriptorSetValue.GetPointer(), 1));
        }

        public unsafe readonly void PushConstants(PipelineLayout layout, VkShaderStageFlags stage, Span<byte> data, uint offset = 0)
        {
            ThrowIfDisposed();

            vkCmdPushConstants(value, layout.value, stage, offset, (uint)data.Length, data.GetPointer());
        }

        public unsafe readonly void PushConstants(PipelineLayout layout, VkShaderStageFlags flags, MemoryAddress data, uint byteLength, uint offset = 0)
        {
            ThrowIfDisposed();

            vkCmdPushConstants(value, layout.value, flags, offset, byteLength, data.pointer);
        }

        public readonly void DrawIndexed(uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0)
        {
            ThrowIfDisposed();

            vkCmdDrawIndexed(value, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
        }

        public readonly void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0)
        {
            ThrowIfDisposed();

            vkCmdDraw(value, vertexCount, instanceCount, firstVertex, firstInstance);
        }

        public readonly void CopyBufferTo(Buffer source, Buffer destination)
        {
            VkBufferCopy region = default;
            region.dstOffset = 0;
            region.srcOffset = 0;
            region.size = source.byteLength;
            vkCmdCopyBuffer(value, source.value, destination.value, 1, &region);
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

            vkCmdCopyBufferToImage(value, sourceBuffer.value, destinationImage.value, VkImageLayout.TransferDstOptimal, 1, &region);
        }

        /// <summary>
        /// Copies data from the buffer with the given coordinates into the image.
        /// <para>
        /// Assumes the source buffer is 4 bytes per pixel.
        /// </para>
        /// </summary>
        public readonly void CopyBufferToImage(Buffer sourceBuffer, uint width, uint height, uint x, uint y, Image destinationImage, uint depth = 1, uint layerCount = 1)
        {
            ThrowIfDisposed();

            VkBufferImageCopy bufferImageCopy = new()
            {
                bufferOffset = ((width * y) + x) * 4,
                bufferRowLength = width,
                bufferImageHeight = height,
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, layerCount),
                imageOffset = new VkOffset3D(0, 0, 0),
                imageExtent = new VkExtent3D(destinationImage.width, destinationImage.height, depth)
            };

            vkCmdCopyBufferToImage(value, sourceBuffer.value, destinationImage.value, VkImageLayout.TransferDstOptimal, 1, &bufferImageCopy);
        }

        public readonly void CopyBufferTo(BufferDeviceMemory source, BufferDeviceMemory destination)
        {
            ThrowIfDisposed();

            VkBufferCopy region = default;
            region.dstOffset = 0;
            region.srcOffset = 0;
            region.size = source.buffer.byteLength;
            vkCmdCopyBuffer(value, source.buffer.value, destination.buffer.value, 1, &region);
        }

        public readonly void CopyBufferTo(Buffer source, Buffer destination, ulong size)
        {
            ThrowIfDisposed();

            VkBufferCopy region = default;
            region.dstOffset = 0;
            region.srcOffset = 0;
            region.size = size;
            vkCmdCopyBuffer(value, source.value, destination.value, 1, &region);
        }

        public readonly void CopyBufferTo(BufferDeviceMemory source, BufferDeviceMemory destination, ulong size)
        {
            ThrowIfDisposed();

            VkBufferCopy region = default;
            region.dstOffset = 0;
            region.srcOffset = 0;
            region.size = size;
            vkCmdCopyBuffer(value, source.buffer.value, destination.buffer.value, 1, &region);
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is CommandBuffer buffer && Equals(buffer);
        }

        public readonly bool Equals(CommandBuffer other)
        {
            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return value.GetHashCode();
        }

        [Conditional("DEBUG")]
        private static void ThrowIfFailedToReset(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to reset command buffer: {result}");
            }
        }

        [Conditional("DEBUG")]
        private static void ThrowIfFailedToBegin(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to begin command buffer: {result}");
            }
        }

        [Conditional("DEBUG")]
        private static void ThrowIfFailedToEnd(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to end command buffer: {result}");
            }
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