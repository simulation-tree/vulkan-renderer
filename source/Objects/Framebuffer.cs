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

        private readonly VkFramebuffer value;
        private bool valid;

        public readonly VkFramebuffer Value
        {
            get
            {
                ThrowIfDisposed();

                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

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
                images[i] = imageViews[i].Value;
            }

            VkFramebufferCreateInfo createInfo = new();
            createInfo.renderPass = renderPass.Value;
            createInfo.attachmentCount = (uint)imageViews.Length;
            createInfo.pAttachments = images.GetPointer();
            createInfo.width = width;
            createInfo.height = height;
            createInfo.layers = 1;

            VkResult result = vkCreateFramebuffer(logicalDevice.Value, &createInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create framebuffer: {result}");
            }

            valid = true;
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkDestroyFramebuffer(logicalDevice.Value, value);
            valid = false;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Framebuffer));
            }
        }
    }
}