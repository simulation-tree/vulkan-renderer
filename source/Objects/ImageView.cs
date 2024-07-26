using System;
using System.Diagnostics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    public unsafe struct ImageView : IDisposable, IEquatable<ImageView>
    {
        public readonly VkImageAspectFlags aspects;
        public readonly LogicalDevice device;

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

        public ImageView(Image image, VkImageAspectFlags aspects = VkImageAspectFlags.Color)
        {
            this.aspects = aspects;
            this.device = image.logicalDevice;
            VkImageViewCreateInfo imageCreateInfo = new(image.Value, VkImageViewType.Image2D, image.format, VkComponentMapping.Rgba, new VkImageSubresourceRange(aspects, 0, 1, 0, 1));
            VkResult result = vkCreateImageView(device.Value, &imageCreateInfo, null, out value);
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
            vkDestroyImageView(device.Value, value);
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
