using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct ImageView : IDisposable, IEquatable<ImageView>
    {
        public readonly VkImageAspectFlags aspects;
        public readonly LogicalDevice logicalDevice;

        private readonly VkImageView value;
        private bool valid;

        public readonly VkImageView Value
        {
            get
            {
                ThrowIfDisposed();

                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        public ImageView(Image image, VkImageAspectFlags aspects = VkImageAspectFlags.Color, bool isCubemap = false)
        {
            uint mipLevels = 1;
            this.aspects = aspects;
            this.logicalDevice = image.logicalDevice;
            VkImageViewType viewType = isCubemap ? VkImageViewType.ImageCube : VkImageViewType.Image2D;
            VkImageSubresourceRange range = new(aspects, 0, mipLevels, 0, isCubemap ? 6u : 1u);
            VkImageViewCreateInfo imageCreateInfo = new(image.Value, viewType, image.format, VkComponentMapping.Rgba, range);
            VkResult result = vkCreateImageView(logicalDevice.Value, &imageCreateInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create image view: {result}");
            }

            valid = true;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(ImageView));
            }
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkDestroyImageView(logicalDevice.Value, value);
            valid = false;
        }

        public readonly override bool Equals(object? obj)
        {
            return obj is ImageView view && Equals(view);
        }

        public readonly bool Equals(ImageView other)
        {
            if (IsDisposed && other.IsDisposed)
            {
                return true;
            }

            return value.Equals(other.value);
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(value);
        }

        public static bool operator ==(ImageView left, ImageView right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ImageView left, ImageView right)
        {
            return !(left == right);
        }
    }
}