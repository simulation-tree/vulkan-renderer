using System;
using Vortice.Vulkan;

namespace Vulkan
{
    public readonly unsafe struct IndexBuffer : IDisposable
    {
        public readonly BufferDeviceMemory bufferDeviceMemory;

        public IndexBuffer(Queue graphicsQueue, CommandPool commandPool, ReadOnlySpan<uint> data)
        {
            uint byteLength = (uint)data.Length * sizeof(uint);
            VkPhysicalDeviceLimits limits = graphicsQueue.logicalDevice.physicalDevice.GetLimits();
            byteLength = (uint)(Math.Ceiling(byteLength / (float)limits.minUniformBufferOffsetAlignment) * limits.minUniformBufferOffsetAlignment);
            using BufferDeviceMemory stagingBuffer = new(graphicsQueue.logicalDevice, byteLength, VkBufferUsageFlags.TransferSrc, VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent);

            Span<uint> destinationData = stagingBuffer.memory.Map<uint>(stagingBuffer.buffer.byteLength);
            data.CopyTo(destinationData);
            stagingBuffer.memory.Unmap();

            bufferDeviceMemory = new(graphicsQueue.logicalDevice, byteLength, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.IndexBuffer, VkMemoryPropertyFlags.DeviceLocal);

            using CommandBuffer tempCommandBuffer = commandPool.CreateCommandBuffer();
            tempCommandBuffer.Begin();
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