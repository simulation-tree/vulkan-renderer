using System;

namespace Vulkan;

public unsafe struct InstanceBuffer : IDisposable
{
    public readonly BufferDeviceMemory bufferDeviceMemory;
    public uint instanceCount;

    public readonly LogicalDevice LogicalDevice => bufferDeviceMemory.LogicalDevice;

    public InstanceBuffer(Queue graphicsQueue, CommandPool commandPool, ReadOnlySpan<float> data)
    {
            
    }
        
    public readonly void Dispose()
    {
        bufferDeviceMemory.Dispose();
    }
}