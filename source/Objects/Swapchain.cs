using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    [SkipLocalsInit]
    public unsafe struct Swapchain : IDisposable, IEquatable<Swapchain>
    {
        public readonly LogicalDevice logicalDevice;
        public readonly uint width;
        public readonly uint height;
        public readonly VkFormat format;

        internal VkSwapchainKHR value;

        public readonly bool IsDisposed => value.IsNull;

        public Swapchain(LogicalDevice logicalDevice, Surface surface, uint width, uint height)
        {
            this.logicalDevice = logicalDevice;
            this.width = width;
            this.height = height;
            SwapchainCapabilities swapchainInfo = surface.GetSwapchainInfo(logicalDevice.physicalDevice);
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
                surface = surface.value,
                minImageCount = imageCount,
                imageFormat = format,
                imageColorSpace = surfaceFormat.colorSpace,
                imageExtent = new(width, height),
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment
            };

            (uint graphics, uint present) = logicalDevice.physicalDevice.GetQueueFamilies(surface);
            if (graphics != present)
            {
                Span<uint> queueFamilies = stackalloc uint[2] { graphics, present };
                swapchainCreateInfo.imageSharingMode = VkSharingMode.Concurrent;
                swapchainCreateInfo.queueFamilyIndexCount = 2;
                swapchainCreateInfo.pQueueFamilyIndices = queueFamilies.GetPointer();
            }
            else
            {
                swapchainCreateInfo.imageSharingMode = VkSharingMode.Exclusive;
            }

            swapchainCreateInfo.preTransform = swapchainInfo.capabilities.currentTransform;
            swapchainCreateInfo.compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque;
            swapchainCreateInfo.presentMode = presentMode;
            swapchainCreateInfo.clipped = true;

            VkResult result = vkCreateSwapchainKHR(logicalDevice.value, &swapchainCreateInfo, null, out value);
            ThrowIfUnableToCreate(result);
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkDestroySwapchainKHR(logicalDevice.value, value);
            value = default;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Swapchain));
            }
        }

        public readonly int GetSwapchainImageCount()
        {
            ThrowIfDisposed();

            VkResult result = vkGetSwapchainImagesKHR(logicalDevice.value, value, out uint count);
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException($"Failed to get swapchain images: {result}");
            }

            return (int)count;
        }

        public readonly void GetSwapchainImages(Span<Image> destination)
        {
            ThrowIfDisposed();

            Span<VkImage> imageSpan = stackalloc VkImage[destination.Length];
            VkResult result = vkGetSwapchainImagesKHR(logicalDevice.value, value, imageSpan);
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException($"Failed to get swapchain images: {result}");
            }

            for (int i = 0; i < imageSpan.Length; i++)
            {
                destination[i] = new Image(logicalDevice, imageSpan[i], width, height, format);
            }
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is Swapchain swapchain && Equals(swapchain);
        }

        public readonly bool Equals(Swapchain other)
        {
            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return value.GetHashCode();
        }

        [Conditional("DEBUG")]
        private static void ThrowIfUnableToCreate(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new InvalidOperationException($"Failed to create swapchain: {result}");
            }
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