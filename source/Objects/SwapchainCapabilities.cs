using System;
using Vortice.Vulkan;

namespace Vulkan
{
    public ref struct SwapchainCapabilities(VkSurfaceCapabilitiesKHR capabilities, ReadOnlySpan<VkSurfaceFormatKHR> formats, ReadOnlySpan<VkPresentModeKHR> presentModes)
    {
        public readonly VkSurfaceCapabilitiesKHR capabilities = capabilities;
        public readonly ReadOnlySpan<VkSurfaceFormatKHR> formats = formats;
        public readonly ReadOnlySpan<VkPresentModeKHR> presentModes = presentModes;

        public readonly VkSurfaceFormatKHR ChooseSwapSurfaceFormat()
        {
            if (formats.Length == 1 && formats[0].format == VkFormat.Undefined)
            {
                return new VkSurfaceFormatKHR(VkFormat.B8G8R8A8Unorm, formats[0].colorSpace);
            }

            foreach (VkSurfaceFormatKHR availableFormat in formats)
            {
                if (availableFormat.format == VkFormat.B8G8R8A8Unorm)
                {
                    return availableFormat;
                }
            }

            return formats[0];
        }

        public readonly VkPresentModeKHR ChooseSwapPresentMode()
        {
            foreach (VkPresentModeKHR availablePresentMode in presentModes)
            {
                if (availablePresentMode == VkPresentModeKHR.Mailbox)
                {
                    return availablePresentMode;
                }
            }

            return VkPresentModeKHR.Fifo;
        }
    }
}
