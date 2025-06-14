using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    /// <summary>
    /// Represents a collection of specific memory attachments that are used by a <see cref="RenderPass"/> instance.
    /// </summary>
    [SkipLocalsInit]
    public unsafe struct Framebuffer : IDisposable
    {
        public readonly LogicalDevice logicalDevice;
        public readonly uint width;
        public readonly uint height;

        internal VkFramebuffer value;

        public readonly bool IsDisposed => value.IsNull;

        public Framebuffer(RenderPass renderPass, ImageView imageView, uint width, uint height) : this(renderPass, [imageView], width, height)
        {

        }

        public Framebuffer(RenderPass renderPass, ReadOnlySpan<ImageView> imageViews, uint width, uint height)
        {
            this.logicalDevice = renderPass.logicalDevice;
            this.width = width;
            this.height = height;

            Span<VkImageView> images = stackalloc VkImageView[imageViews.Length];
            for (int i = 0; i < imageViews.Length; i++)
            {
                images[i] = imageViews[i].value;
            }

            VkFramebufferCreateInfo createInfo = new();
            createInfo.renderPass = renderPass.value;
            createInfo.attachmentCount = (uint)imageViews.Length;
            createInfo.pAttachments = images.GetPointer();
            createInfo.width = width;
            createInfo.height = height;
            createInfo.layers = 1;

            VkResult result = vkCreateFramebuffer(logicalDevice.value, &createInfo, null, out value);
            ThrowIfUnableToCreate(result);
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkDestroyFramebuffer(logicalDevice.value, value);
            value = default;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Framebuffer));
            }
        }

        [Conditional("DEBUG")]
        private static void ThrowIfUnableToCreate(VkResult result)
        {
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create framebuffer: {result}");
            }
        }
    }
}