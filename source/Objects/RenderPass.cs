using System;
using System.Diagnostics;
using System.Numerics;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Vulkan
{
    /// <summary>
    /// Contains image state for the render target.
    /// </summary>
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

        public RenderPass(LogicalDevice logicalDevice, Surface surface)
        {
            this.logicalDevice = logicalDevice;
            SwapchainCapabilities swapchainInfo = surface.GetSwapchainInfo(logicalDevice.physicalDevice);
            VkSurfaceFormatKHR surfaceFormat = swapchainInfo.ChooseSwapSurfaceFormat();
            VkFormat colorFormat = surfaceFormat.format;
            VkAttachmentDescription* attachment = stackalloc VkAttachmentDescription[2];
            attachment[0] = new(colorFormat, VkSampleCountFlags.Count1, VkAttachmentLoadOp.Clear, VkAttachmentStoreOp.Store,
                VkAttachmentLoadOp.DontCare, VkAttachmentStoreOp.DontCare, VkImageLayout.Undefined, VkImageLayout.PresentSrcKHR);
            attachment[1] = new(logicalDevice.GetDepthFormat(), VkSampleCountFlags.Count1, VkAttachmentLoadOp.Clear, VkAttachmentStoreOp.DontCare,
                VkAttachmentLoadOp.DontCare, VkAttachmentStoreOp.DontCare, VkImageLayout.Undefined, VkImageLayout.DepthStencilAttachmentOptimal);
            VkAttachmentReference attachmentReference = new(0, VkImageLayout.ColorAttachmentOptimal);
            VkAttachmentReference depthReference = new(1, VkImageLayout.DepthStencilAttachmentOptimal);
            VkSubpassDescription subPass = new()
            {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachmentCount = 1,
                pColorAttachments = &attachmentReference,
                pDepthStencilAttachment = &depthReference
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

            fixed (VkSubpassDependency* dependenciesPtr = dependencies)
            {
                VkRenderPassCreateInfo renderPassCreateInfo = new()
                {
                    attachmentCount = 2,
                    pAttachments = attachment,
                    subpassCount = 1,
                    pSubpasses = &subPass,
                    dependencyCount = (uint)dependencies.Length,
                    pDependencies = dependenciesPtr
                };

                VkResult result = vkCreateRenderPass(logicalDevice.Value, &renderPassCreateInfo, null, out value);
                if (result != VkResult.Success)
                {
                    throw new Exception($"Failed to create render pass: {result}");
                }

                valid = true;
            }
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

        /// <summary>
        /// Begins rendering into the given frame buffer.
        /// </summary>
        public readonly void Begin(CommandBuffer commandBuffer, Framebuffer framebuffer, Vector4 area, Vector4 clearColor)
        {
            ThrowIfDisposed();
            VkClearValue* clearValue = stackalloc VkClearValue[2];
            clearValue[0].color = new VkClearColorValue(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W);
            clearValue[1].depthStencil = new VkClearDepthStencilValue(1.0f, 0);
            VkRenderPassBeginInfo renderPassBeginInfo = new()
            {
                renderPass = value,
                framebuffer = framebuffer.Value,
                renderArea = new VkRect2D((int)area.X, (int)area.Y, (uint)area.Z, (uint)area.W),
                clearValueCount = 2,
                pClearValues = clearValue
            };

            vkCmdBeginRenderPass(commandBuffer.Value, &renderPassBeginInfo, VkSubpassContents.Inline);
        }

        public readonly void End(CommandBuffer commandBuffer)
        {
            ThrowIfDisposed();
            vkCmdEndRenderPass(commandBuffer.Value);
        }
    }
}
