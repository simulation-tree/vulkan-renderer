using System;
using Vortice.Vulkan;

namespace Vulkan
{
    public readonly unsafe struct IndexBuffer : IDisposable
    {
        public readonly BufferDeviceMemory bufferDeviceMemory;

        /// <summary>
        /// Amount of bytes stored in the buffer.
        /// </summary>
        public readonly uint Size => bufferDeviceMemory.buffer.size;

        public readonly LogicalDevice Device => bufferDeviceMemory.LogicalDevice;

        public IndexBuffer(Queue graphicsQueue, CommandPool commandPool, ReadOnlySpan<uint> data)
        {
            uint byteCount = (uint)data.Length * sizeof(uint);
            VkPhysicalDeviceLimits limits = graphicsQueue.logicalDevice.physicalDevice.GetLimits();
            byteCount = (uint)(Math.Ceiling(byteCount / (float)limits.minUniformBufferOffsetAlignment) * limits.minUniformBufferOffsetAlignment);
            using BufferDeviceMemory stagingBuffer = new(graphicsQueue.logicalDevice, byteCount, VkBufferUsageFlags.TransferSrc, VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent);

            Span<uint> destinationData = stagingBuffer.Map<uint>();
            data.CopyTo(destinationData);
            stagingBuffer.Unmap();

            bufferDeviceMemory = new(graphicsQueue.logicalDevice, byteCount, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.IndexBuffer, VkMemoryPropertyFlags.DeviceLocal);

            using CommandBuffer tempCommandBuffer = new(commandPool);
            tempCommandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmit);
            tempCommandBuffer.CopyBufferTo(stagingBuffer, bufferDeviceMemory);
            tempCommandBuffer.End();
            graphicsQueue.Submit(tempCommandBuffer);
            graphicsQueue.Wait();
        }

        public IndexBuffer(Queue graphicsQueue, CommandPool commandPool, void* pointer, uint byteCount)
        {
            VkPhysicalDeviceLimits limits = graphicsQueue.logicalDevice.physicalDevice.GetLimits();
            byteCount = (uint)(Math.Ceiling(byteCount / (float)limits.minUniformBufferOffsetAlignment) * limits.minUniformBufferOffsetAlignment);
            using BufferDeviceMemory stagingBuffer = new(graphicsQueue.logicalDevice, byteCount, VkBufferUsageFlags.TransferSrc, VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent);

            stagingBuffer.CopyFrom(pointer, byteCount);

            bufferDeviceMemory = new(graphicsQueue.logicalDevice, byteCount, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.IndexBuffer, VkMemoryPropertyFlags.DeviceLocal);

            using CommandBuffer tempCommandBuffer = new(commandPool);
            tempCommandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmit);
            tempCommandBuffer.CopyBufferTo(stagingBuffer, bufferDeviceMemory);
            tempCommandBuffer.End();
            graphicsQueue.Submit(tempCommandBuffer);
            graphicsQueue.Wait();
        }

        public readonly void Dispose()
        {
            bufferDeviceMemory.Dispose();
        }
    }
}
