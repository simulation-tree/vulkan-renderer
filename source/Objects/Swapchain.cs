using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Swapchain : IDisposable, IEquatable<Swapchain>
    {
        public readonly LogicalDevice device;
        public readonly uint width;
        public readonly uint height;
        public readonly VkFormat format;
        private bool valid;

        private readonly VkSwapchainKHR value;

        public readonly VkSwapchainKHR Value
        {
            get
            {
                ThrowIfDisposed();
                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        public Swapchain(LogicalDevice device, Surface surface, uint width, uint height)
        {
            this.device = device;
            this.width = width;
            this.height = height;
            SwapchainCapabilities swapchainInfo = surface.GetSwapchainInfo(device.physicalDevice);
            VkSurfaceFormatKHR surfaceFormat = swapchainInfo.ChooseSwapSurfaceFormat();
            VkPresentModeKHR presentMode = swapchainInfo.ChooseSwapPresentMode();
            format = surfaceFormat.format;
            uint imageCount = swapchainInfo.capabilities.minImageCount + 1;
            if (swapchainInfo.capabilities.maxImageCount > 0 && imageCount > swapchainInfo.capabilities.maxImageCount)
            {
                imageCount = swapchainInfo.capabilities.maxImageCount;
            }

            VkSwapchainCreateInfoKHR swapchainCreateInfo = new()
            {
                surface = surface.Value,
                minImageCount = imageCount,
                imageFormat = format,
                imageColorSpace = surfaceFormat.colorSpace,
                imageExtent = new(width, height),
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment
            };

            (uint graphics, uint present) = device.physicalDevice.GetQueueFamilies(surface);
            if (graphics != present)
            {
                uint* queueFamilies = stackalloc uint[2] { graphics, present };
                swapchainCreateInfo.imageSharingMode = VkSharingMode.Concurrent;
                swapchainCreateInfo.queueFamilyIndexCount = 2;
                swapchainCreateInfo.pQueueFamilyIndices = queueFamilies;
            }
            else
            {
                swapchainCreateInfo.imageSharingMode = VkSharingMode.Exclusive;
            }

            swapchainCreateInfo.preTransform = swapchainInfo.capabilities.currentTransform;
            swapchainCreateInfo.compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque;
            swapchainCreateInfo.presentMode = presentMode;
            swapchainCreateInfo.clipped = true;

            VkResult result = vkCreateSwapchainKHR(device.Value, &swapchainCreateInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create swap chain: {result}");
            }

            valid = true;
        }

        public void Dispose()
        {
            ThrowIfDisposed();
            vkDestroySwapchainKHR(device.Value, value);
            valid = false;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Swapchain));
            }
        }

        public readonly int CopyImagesTo(Span<Image> buffer)
        {
            ThrowIfDisposed();
            ReadOnlySpan<VkImage> imageSpan = vkGetSwapchainImagesKHR(device.Value, value);
            for (int i = 0; i < imageSpan.Length; i++)
            {
                Image image = new(device, imageSpan[i], width, height, format);
                buffer[i] = image;
            }

            return imageSpan.Length;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is Swapchain swapchain && Equals(swapchain);
        }

        public readonly bool Equals(Swapchain other)
        {
            if (!other.valid && !valid)
            {
                return true;
            }

            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(value);
        }

        public static bool operator ==(Swapchain left, Swapchain right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Swapchain left, Swapchain right)
        {
            return !(left == right);
        }
    }
}
