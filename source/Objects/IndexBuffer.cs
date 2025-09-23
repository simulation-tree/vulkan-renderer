using System;
using Vortice.Vulkan;

namespace Vulkan
{
    public readonly unsafe struct IndexBuffer : IDisposable, IEquatable<IndexBuffer>
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

        public readonly override bool Equals(object? obj)
        {
            return obj is IndexBuffer buffer && Equals(buffer);
        }

        public readonly bool Equals(IndexBuffer other)
        {
            return bufferDeviceMemory.Equals(other.bufferDeviceMemory);
        }

        public readonly override int GetHashCode()
        {
            return bufferDeviceMemory.GetHashCode();
        }

        public static bool operator ==(IndexBuffer left, IndexBuffer right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(IndexBuffer left, IndexBuffer right)
        {
            return !(left == right);
        }
    }
}