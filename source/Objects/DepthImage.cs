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
            VkFormat format = swapchain.device.GetDepthFormat();
            image = new(swapchain.device, swapchain.width, swapchain.height, 1, format, VkImageUsageFlags.DepthStencilAttachment);
            imageMemory = new(image, VkMemoryPropertyFlags.DeviceLocal);
            imageView = new(image, VkImageAspectFlags.Depth);

            using CommandPool tempPool = new(graphics);
            using CommandBuffer tempBuffer = new(tempPool);
            tempBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmit);
            tempBuffer.TransitionImageLayout(image, VkImageLayout.Undefined, VkImageLayout.DepthStencilAttachmentOptimal, VkImageAspectFlags.Depth);
            tempBuffer.End();
            graphics.Submit(tempBuffer);
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
