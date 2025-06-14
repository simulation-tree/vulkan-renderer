using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct Image : IDisposable, IEquatable<Image>
    {
        public readonly LogicalDevice logicalDevice;
        public readonly uint width;
        public readonly uint height;
        public readonly VkFormat format;

        internal VkImage value;

        public readonly bool IsDisposed => value.IsNull;

        internal Image(LogicalDevice logicalDevice, VkImage existingImage, uint width, uint height, VkFormat format)
        {
            this.logicalDevice = logicalDevice;
            this.format = format;
            this.width = width;
            this.height = height;
            value = existingImage;
        }

        public Image(LogicalDevice logicalDevice, uint width, uint height, uint depth, VkFormat format, VkImageUsageFlags usage, bool isCubemap = false)
        {
            this.logicalDevice = logicalDevice;
            this.format = format;
            this.width = width;
            this.height = height;

            VkImageCreateInfo createInfo = new();
            createInfo.imageType = VkImageType.Image2D;
            createInfo.extent.width = width;
            createInfo.extent.height = height;
            createInfo.extent.depth = depth;
            createInfo.mipLevels = 1;
            createInfo.arrayLayers = isCubemap ? 6u : 1u;
            createInfo.format = format;
            createInfo.tiling = VkImageTiling.Optimal;
            createInfo.initialLayout = VkImageLayout.Undefined;
            createInfo.usage = usage;
            createInfo.flags = isCubemap ? VkImageCreateFlags.CubeCompatible : default;
            createInfo.samples = VkSampleCountFlags.Count1;
            createInfo.sharingMode = VkSharingMode.Exclusive;

            VkResult result = vkCreateImage(logicalDevice.value, &createInfo, null, out value);
            ThrowIfUnableToCreate(result);
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            logicalDevice.Wait();
            vkDestroyImage(logicalDevice.value, value);
            value = default;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Image));
            }
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is Image image && Equals(image);
        }

        public readonly bool Equals(Image other)
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
                throw new InvalidOperationException($"Failed to create image: {result}");
            }
        }

        public static bool operator ==(Image left, Image right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Image left, Image right)
        {
            return !(left == right);
        }
    }
}