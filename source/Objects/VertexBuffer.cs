using System;
using Unmanaged;
using Vortice.Vulkan;

namespace Vulkan
{
    public readonly unsafe struct VertexBuffer : IDisposable, IEquatable<VertexBuffer>
    {
        public readonly BufferDeviceMemory bufferDeviceMemory;

        public readonly LogicalDevice LogicalDevice => bufferDeviceMemory.LogicalDevice;

        public VertexBuffer(Queue graphicsQueue, CommandPool commandPool, USpan<float> data)
        {
            uint byteCount = data.Length * sizeof(float);
            VkPhysicalDeviceLimits limits = graphicsQueue.logicalDevice.physicalDevice.GetLimits();
            byteCount = (uint)(Math.Ceiling(byteCount / (float)limits.minUniformBufferOffsetAlignment) * limits.minUniformBufferOffsetAlignment);
            using BufferDeviceMemory stagingBuffer = new(graphicsQueue.logicalDevice, byteCount, VkBufferUsageFlags.TransferSrc, VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent);

            USpan<float> destinationData = stagingBuffer.Map<float>();
            data.CopyTo(destinationData);
            stagingBuffer.Unmap();

            bufferDeviceMemory = new(graphicsQueue.logicalDevice, byteCount, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.VertexBuffer, VkMemoryPropertyFlags.DeviceLocal);

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
            return HashCode.Combine(bufferDeviceMemory);
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
