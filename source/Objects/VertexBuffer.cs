using System;
using Vortice.Vulkan;

namespace Vulkan
{
    public readonly unsafe struct VertexBuffer : IDisposable, IEquatable<VertexBuffer>
    {
        public readonly BufferDeviceMemory bufferDeviceMemory;

        public VertexBuffer(Queue graphicsQueue, CommandPool commandPool, ReadOnlySpan<float> data)
        {
            uint byteLength = (uint)data.Length * sizeof(float);
            VkPhysicalDeviceLimits limits = graphicsQueue.logicalDevice.physicalDevice.GetLimits();
            byteLength = (uint)(Math.Ceiling(byteLength / (float)limits.minUniformBufferOffsetAlignment) * limits.minUniformBufferOffsetAlignment);

            using BufferDeviceMemory stagingBuffer = new(graphicsQueue.logicalDevice, byteLength, VkBufferUsageFlags.TransferSrc, VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent);
            Span<float> destinationData = stagingBuffer.memory.Map<float>(stagingBuffer.buffer.byteLength);
            data.CopyTo(destinationData);
            stagingBuffer.memory.Unmap();

            bufferDeviceMemory = new(graphicsQueue.logicalDevice, byteLength, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.VertexBuffer, VkMemoryPropertyFlags.DeviceLocal);

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

        public readonly override bool Equals(object? obj)
        {
            return obj is VertexBuffer buffer && Equals(buffer);
        }

        public readonly bool Equals(VertexBuffer other)
        {
            return bufferDeviceMemory.Equals(other.bufferDeviceMemory);
        }

        public readonly override int GetHashCode()
        {
            return bufferDeviceMemory.GetHashCode();
        }

        public static bool operator ==(VertexBuffer left, VertexBuffer right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VertexBuffer left, VertexBuffer right)
        {
            return !(left == right);
        }
    }
}