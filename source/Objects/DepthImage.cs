using System;
using Vortice.Vulkan;

namespace Vulkan
{
    public readonly unsafe struct DepthImage : IDisposable
    {
        public readonly Image image;
        public readonly DeviceMemory imageMemory;
        public readonly ImageView imageView;

        public readonly LogicalDevice Device => image.logicalDevice;
        public readonly bool IsDisposed => image.IsDisposed;

        public DepthImage(Swapchain swapchain, Queue graphics)
        {
            VkFormat depthFormat = swapchain.device.GetDepthFormat();
            image = new(swapchain.device, swapchain.width, swapchain.height, 1, depthFormat, VkImageUsageFlags.DepthStencilAttachment);
            imageMemory = new(image, VkMemoryPropertyFlags.DeviceLocal);
            imageView = new(image, VkImageAspectFlags.Depth);
            
            using CommandPool tempPool = new(graphics, true);
            using CommandBuffer commandBuffer = tempPool.CreateCommandBuffer();
            commandBuffer.Begin();
            commandBuffer.TransitionImageLayout(image, VkImageLayout.Undefined, VkImageLayout.DepthStencilAttachmentOptimal, VkImageAspectFlags.Depth);
            commandBuffer.End();
            graphics.Submit(commandBuffer);
            graphics.Wait();
        }

        public readonly void Dispose()
        {
            imageView.Dispose();
            imageMemory.Dispose();
            image.Dispose();
        }
    }
}
