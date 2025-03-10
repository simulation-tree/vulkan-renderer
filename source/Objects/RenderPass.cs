using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    /// <summary>
    /// Contains image state for the render target.
    /// </summary>
    [SkipLocalsInit]
    public unsafe struct RenderPass : IDisposable
    {
        public readonly LogicalDevice logicalDevice;

        private readonly VkRenderPass value;
        private bool valid;

        public readonly VkRenderPass Value
        {
            get
            {
                ThrowIfDisposed();

                return value;
            }
        }

        public readonly bool IsDisposed => !valid;

        public RenderPass(LogicalDevice logicalDevice, Span<Attachment> attachments)
        {
            this.logicalDevice = logicalDevice;
            Span<VkAttachmentDescription> attachmentsPointer = stackalloc VkAttachmentDescription[attachments.Length];
            for (int i = 0; i < attachments.Length; i++)
            {
                Attachment attachment = attachments[i];
                VkFormat format = attachment.format;
                VkSampleCountFlags samples = attachment.samples;
                VkAttachmentLoadOp load = attachment.load;
                VkAttachmentStoreOp store = attachment.store;
                VkAttachmentLoadOp stencilLoad = attachment.stencilLoad;
                VkAttachmentStoreOp stencilStore = attachment.stencilStore;
                VkImageLayout initialLayout = attachment.initialLayout;
                VkImageLayout finalLayout = attachment.finalLayout;
                VkAttachmentDescriptionFlags flags = attachment.flags;
                attachmentsPointer[i] = new(format, samples, load, store, stencilLoad, stencilStore, initialLayout, finalLayout, flags);
            }

            VkAttachmentReference colorAttachment = new(0, VkImageLayout.ColorAttachmentOptimal);
            VkAttachmentReference depthAttachment = new(1, VkImageLayout.DepthStencilAttachmentOptimal);
            VkSubpassDescription subPass = new()
            {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachmentCount = 1,
                pColorAttachments = &colorAttachment,
                pDepthStencilAttachment = &depthAttachment
            };

            Span<VkSubpassDependency> dependencies =
            [
                new VkSubpassDependency
                {
                    srcSubpass = VK_SUBPASS_EXTERNAL,
                    dstSubpass = 0,
                    srcStageMask = VkPipelineStageFlags.ColorAttachmentOutput | VkPipelineStageFlags.EarlyFragmentTests,
                    dstStageMask = VkPipelineStageFlags.ColorAttachmentOutput | VkPipelineStageFlags.EarlyFragmentTests,
                    srcAccessMask = 0,
                    dstAccessMask = VkAccessFlags.ColorAttachmentWrite | VkAccessFlags.DepthStencilAttachmentWrite,
                    dependencyFlags = 0
                }
            ];

            VkRenderPassCreateInfo renderPassCreateInfo = new()
            {
                attachmentCount = (uint)attachments.Length,
                pAttachments = attachmentsPointer.GetPointer(),
                subpassCount = 1,
                pSubpasses = &subPass,
                dependencyCount = (uint)dependencies.Length,
                pDependencies = dependencies.GetPointer()
            };

            VkResult result = vkCreateRenderPass(logicalDevice.Value, &renderPassCreateInfo, null, out value);
            if (result != VkResult.Success)
            {
                throw new Exception($"Failed to create render pass: {result}");
            }

            valid = true;
        }

        public void Dispose()
        {
            ThrowIfDisposed();

            vkDestroyRenderPass(logicalDevice.Value, value);
            valid = false;
        }

        [Conditional("DEBUG")]
        private readonly void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(RenderPass));
            }
        }

        public readonly struct Attachment
        {
            public readonly VkFormat format;
            public readonly VkSampleCountFlags samples;
            public readonly VkAttachmentLoadOp load;
            public readonly VkAttachmentStoreOp store;
            public readonly VkAttachmentLoadOp stencilLoad;
            public readonly VkAttachmentStoreOp stencilStore;
            public readonly VkImageLayout initialLayout;
            public readonly VkImageLayout finalLayout;
            public readonly VkAttachmentDescriptionFlags flags;

            public Attachment(VkFormat format, VkSampleCountFlags samples, VkAttachmentLoadOp load, VkAttachmentStoreOp store,
                VkAttachmentLoadOp stencilLoad, VkAttachmentStoreOp stencilStore, VkImageLayout initialLayout, VkImageLayout finalLayout, VkAttachmentDescriptionFlags flags = VkAttachmentDescriptionFlags.None)
            {
                this.format = format;
                this.samples = samples;
                this.load = load;
                this.store = store;
                this.stencilLoad = stencilLoad;
                this.stencilStore = stencilStore;
                this.initialLayout = initialLayout;
                this.finalLayout = finalLayout;
                this.flags = flags;
            }
        }
    }
}