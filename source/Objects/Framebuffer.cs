using System;
using System.Diagnostics;
using Unmanaged;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    /// <summary>
    /// Represents a collection of specific memory attachments that are used by a <see cref="RenderPass"/> instance.
    /// </summary>
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

        public Framebuffer(RenderPass renderPass, USpan<ImageView> imageViews, uint width, uint height)
        {
            this.logicalDevice = renderPass.logicalDevice;
            this.width = width;
            this.height = height;

            VkImageView* images = stackalloc VkImageView[(int)imageViews.length];
            for (uint i = 0; i < imageViews.length; i++)
            {
                images[i] = imageViews[i].Value;
            }

            VkFramebufferCreateInfo createInfo = new();
            createInfo.renderPass = renderPass.Value;
            createInfo.attachmentCount = imageViews.length;
            createInfo.pAttachments = images;
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