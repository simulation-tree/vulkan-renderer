using Rendering.Components;
using Shaders.Components;
using Simulation;
using System;
using System.Numerics;
using Unmanaged;
using Unmanaged.Collections;
using Vortice.Vulkan;
using Vulkan;

namespace Rendering.Vulkan
{
    internal struct VulkanRenderer : IDisposable
    {
        private const uint MaxFramesInFlight = 2;

        private readonly Destination destination;
        private readonly Instance instance;
        private readonly PhysicalDevice physicalDevice;
        private readonly UnmanagedDictionary<eint, ShaderModule> shaderModules;
        private readonly UnmanagedDictionary<int, Pipeline> pipelines;
        private readonly UnmanagedArray<CommandBuffer> commandBuffers;
        private readonly UnmanagedArray<Fence> submitFences;
        private readonly UnmanagedArray<Semaphore> pullSemaphores;
        private readonly UnmanagedArray<Semaphore> pushSemaphores;
        private UnmanagedArray<ImageView> surfaceImageViews;
        private UnmanagedArray<Framebuffer> surfaceFramebuffers;
        private LogicalDevice logicalDevice;
        private Surface surface;
        private Swapchain swapchain;
        private Queue graphicsQueue;
        private Queue presentationQueue;
        private RenderPass renderPass;
        private CommandPool commandPool;
        private DepthImage depthImage;
        private uint frameIndex;
        private uint imageIndex;
        private uint width;
        private uint height;

        public readonly nint Library => instance.Value.Handle;

        public VulkanRenderer(Destination destination, Instance instance)
        {
            this.destination = destination;
            this.instance = instance;

            if (instance.PhysicalDevices.Length == 0)
            {
                throw new InvalidOperationException("No physical devices found");
            }

            if (TryGetBestPhysicalDevice(instance.PhysicalDevices, ["VK_KHR_swapchain"], out uint index))
            {
                physicalDevice = instance.PhysicalDevices[(int)index];
                Console.WriteLine($"Vulkan instance created for {destination.entity.value}");
            }
            else
            {
                throw new InvalidOperationException("No suitable physical device found");
            }

            shaderModules = new();
            pipelines = new();
            commandBuffers = new();
            submitFences = new();
            pullSemaphores = new();
            pushSemaphores = new();
        }

        public readonly void Dispose()
        {
            if (surface != default)
            {
                logicalDevice.Wait();
                DisposeSwapchain();
                DisposePipelines();
                DisposeShaderModules();
                renderPass.Dispose();
                for (uint i = 0; i < MaxFramesInFlight; i++)
                {
                    commandBuffers[i].Dispose();
                    submitFences[i].Dispose();
                    pullSemaphores[i].Dispose();
                    pushSemaphores[i].Dispose();
                }

                commandBuffers.Dispose();
                submitFences.Dispose();
                pullSemaphores.Dispose();
                pushSemaphores.Dispose();

                commandPool.Dispose();
                logicalDevice.Dispose();
                surface.Dispose();
            }

            instance.Dispose();
            Console.WriteLine($"Vulkan instance finished for {destination.entity.value}");
        }

        private readonly void DisposePipelines()
        {
            foreach (int hash in pipelines.Keys)
            {
                Pipeline pipeline = pipelines[hash];
                pipeline.Dispose();
            }

            pipelines.Dispose();
        }

        private readonly void DisposeShaderModules()
        {
            foreach (eint shaderEntity in shaderModules.Keys)
            {
                ShaderModule shaderModule = shaderModules[shaderEntity];
                shaderModule.Dispose();
            }

            shaderModules.Dispose();
        }

        private readonly void DisposeSwapchain()
        {
            foreach (Framebuffer framebuffer in surfaceFramebuffers)
            {
                framebuffer.Dispose();
            }

            surfaceFramebuffers.Dispose();

            foreach (ImageView imageView in surfaceImageViews)
            {
                imageView.Dispose();
            }

            surfaceImageViews.Dispose();

            if (swapchain != default)
            {
                swapchain.Dispose();
                depthImage.Dispose();
            }
        }

        private void RebuildSwapchain()
        {
            //todo: can also rebuild the render pass when moving a window to hdr from sdr monitors
            logicalDevice.Wait();
            DisposeSwapchain();
            CreateSwapchain(out width, out height);
            CreateImageViewsAndBuffers(width, height);
        }

        public void SurfaceCreated(nint surfaceAddress)
        {
            surface = new(instance, surfaceAddress);
            (uint graphicsFamily, uint presentationFamily) = physicalDevice.GetQueueFamilies(surface);
            logicalDevice = new(physicalDevice, [graphicsFamily, presentationFamily], ["VK_KHR_swapchain"]);
            graphicsQueue = new(logicalDevice, graphicsFamily, 0);
            presentationQueue = new(logicalDevice, presentationFamily, 0);
            CreateSwapchain(out uint width, out uint height);
            Span<RenderPass.Attachment> attachments =
            [
                new(swapchain.format, VkSampleCountFlags.Count1, VkAttachmentLoadOp.Clear, VkAttachmentStoreOp.Store, VkAttachmentLoadOp.DontCare,
                    VkAttachmentStoreOp.DontCare, VkImageLayout.Undefined, VkImageLayout.PresentSrcKHR),
                new(logicalDevice.GetDepthFormat(), VkSampleCountFlags.Count1, VkAttachmentLoadOp.Clear, VkAttachmentStoreOp.DontCare, VkAttachmentLoadOp.DontCare,
                    VkAttachmentStoreOp.DontCare, VkImageLayout.Undefined, VkImageLayout.DepthStencilAttachmentOptimal),
            ];

            renderPass = new(logicalDevice, attachments);
            CreateImageViewsAndBuffers(width, height);
            commandPool = new(graphicsQueue, true);

            //create multiples of these, 1 for each concurrent frame
            commandBuffers.Resize(MaxFramesInFlight);
            submitFences.Resize(MaxFramesInFlight);
            pullSemaphores.Resize(MaxFramesInFlight);
            pushSemaphores.Resize(MaxFramesInFlight);
            commandPool.CreateCommandBuffers(commandBuffers.AsSpan());

            for (uint i = 0; i < MaxFramesInFlight; i++)
            {
                submitFences[i] = new(logicalDevice);
                pullSemaphores[i] = new(logicalDevice);
                pushSemaphores[i] = new(logicalDevice);
            }
        }

        private void CreateSwapchain(out uint width, out uint height)
        {
            if (surface.TryGetBestSize(physicalDevice, out width, out height))
            {
                swapchain = new(logicalDevice, surface, width, height);
            }
            else
            {
                (uint minWidth, uint maxWidth, uint minHeight, uint maxHeight) = surface.GetSizeRange(physicalDevice);
                (width, height) = destination.GetDestinationSize();
                width = Math.Max(minWidth, Math.Min(maxWidth, width));
                height = Math.Max(minHeight, Math.Min(maxHeight, height));
                swapchain = new(logicalDevice, surface, width, height);
            }

            depthImage = new(swapchain, graphicsQueue);
        }

        private void CreateImageViewsAndBuffers(uint width, uint height)
        {
            Span<Image> images = stackalloc Image[8];
            int imageCount = swapchain.CopyImagesTo(images);
            surfaceImageViews = new((uint)imageCount);
            surfaceFramebuffers = new((uint)imageCount);
            for (int i = 0; i < imageCount; i++)
            {
                ImageView imageView = new(images[i]);
                Framebuffer framebuffer = new(renderPass, [imageView, depthImage.imageView], width, height);
                surfaceImageViews[(uint)i] = imageView;
                surfaceFramebuffers[(uint)i] = framebuffer;
            }
        }

        public bool BeginRender()
        {
            Fence submitFence = submitFences[frameIndex];
            Semaphore pullSemaphore = pullSemaphores[frameIndex];
            Semaphore pushSemaphore = pushSemaphores[frameIndex];
            CommandBuffer commandBuffer = commandBuffers[frameIndex];

            submitFence.Wait();

            VkResult result = logicalDevice.TryAcquireNextImage(swapchain, pullSemaphore, default, out imageIndex);
            if (result == VkResult.ErrorOutOfDateKHR)
            {
                RebuildSwapchain();
                return false;
            }
            else if (result != VkResult.Success && result != VkResult.SuboptimalKHR)
            {
                throw new InvalidOperationException($"Failed to acquire next image: {result}");
            }

            submitFence.Reset();
            commandBuffer.Reset();
            commandBuffer.Begin();

            Framebuffer framebuffer = surfaceFramebuffers[imageIndex];
            Vector4 area = new(0, 0, 1, 1);
            Vector4 clearColor = new(0, 0, 0, 1);
            commandBuffer.BeginRenderPass(renderPass, framebuffer, area, clearColor);

            Vector4 viewport = new(0, 0, framebuffer.width, framebuffer.height);
            commandBuffer.SetViewport(viewport);

            Vector4 scissor = new(0, 0, framebuffer.width, framebuffer.height);
            commandBuffer.SetScissor(scissor);
            return true;
        }

        private readonly bool IsDestinationResized()
        {
            (uint width, uint height) = destination.GetDestinationSize();
            return width != this.width || height != this.height;
        }

        public readonly void Render(ReadOnlySpan<eint> entities, eint material, eint mesh, eint camera)
        {
            World world = destination.entity.world;
            IsMaterial materialComponent = world.GetComponent<IsMaterial>(material);
            eint shader = materialComponent.shader;
            IsShader shaderComponent = world.GetComponent<IsShader>(shader);

            //todo: detect when shader has been modified, could use a version number instead of a boolean
            if (!shaderModules.TryGetValue(shaderComponent.vertex, out ShaderModule vertexShader))
            {
                UnmanagedList<byte> bytecode = world.GetList<byte>(shaderComponent.vertex);
                vertexShader = new(logicalDevice, bytecode.AsSpan());
                shaderModules.Add(shaderComponent.vertex, vertexShader);
            }

            if (!shaderModules.TryGetValue(shaderComponent.fragment, out ShaderModule fragmentShader))
            {
                UnmanagedList<byte> bytecode = world.GetList<byte>(shaderComponent.fragment);
                fragmentShader = new(logicalDevice, bytecode.AsSpan());
                shaderModules.Add(shaderComponent.fragment, fragmentShader);
            }

            CommandBuffer commandBuffer = commandBuffers[frameIndex];
            //todo: mesh hash isnt as good as the vertex/fragment, if mesh is actually a compiled object then it can work
            //int hash = HashCode.Combine(vertexShader, fragmentShader, mesh);
            //if (!pipelines.TryGetValue(hash, out Pipeline pipeline))
            //{
            //    ReadOnlySpan<DescriptorSetLayout> setLayouts = stackalloc DescriptorSetLayout[0];
            //    ReadOnlySpan<VertexInputAttribute> vertexAttributes = stackalloc VertexInputAttribute[0];
            //    PipelineCreateInput pipelineCreation = new(setLayouts, renderPass, vertexShader, fragmentShader, vertexAttributes);
            //    pipeline = new(pipelineCreation, "main");
            //    pipelines.Add(hash, pipeline);
            //}
            //
            //commandBuffer.BindPipeline(pipeline, VkPipelineBindPoint.Graphics);
        }

        public void EndRender()
        {
            Fence submitFence = submitFences[frameIndex];
            Semaphore pullSemaphore = pullSemaphores[frameIndex];
            Semaphore pushSemaphore = pushSemaphores[frameIndex];
            CommandBuffer commandBuffer = commandBuffers[frameIndex];

            commandBuffer.EndRenderPass();
            commandBuffer.End();

            graphicsQueue.Submit(commandBuffer, pullSemaphore, VkPipelineStageFlags.ColorAttachmentOutput, pushSemaphore, submitFence);
            VkResult result = presentationQueue.TryPresent(pushSemaphore, swapchain, imageIndex);
            if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR || IsDestinationResized())
            {
                RebuildSwapchain();
            }
            else if (result != VkResult.Success)
            {
                throw new InvalidOperationException($"Failed to present image: {result}");
            }

            frameIndex = (frameIndex + 1) % MaxFramesInFlight;
        }

        private static bool TryGetBestPhysicalDevice(ReadOnlySpan<PhysicalDevice> physicalDevices, ReadOnlySpan<FixedString> requiredExtensions, out uint index)
        {
            uint highestScore = 0;
            index = uint.MaxValue;
            for (int i = 0; i < physicalDevices.Length; i++)
            {
                uint score = GetScore(physicalDevices[i], requiredExtensions);
                if (score > highestScore)
                {
                    highestScore = score;
                    index = (uint)i;
                }
            }

            return true;

            unsafe static uint GetScore(PhysicalDevice physicalDevice, ReadOnlySpan<FixedString> requiredExtensions)
            {
                Vortice.Vulkan.VkPhysicalDeviceFeatures features = physicalDevice.GetFeatures();
                if (!features.geometryShader)
                {
                    //no geometry shader support
                    return 0;
                }

                if (!physicalDevice.TryGetGraphicsQueueFamily(out _))
                {
                    //no ability to render
                    return 0;
                }

                ReadOnlySpan<Vortice.Vulkan.VkExtensionProperties> availableExtensions = physicalDevice.GetExtensions();
                if (availableExtensions.Length > 0)
                {
                    foreach (FixedString requiredExtension in requiredExtensions)
                    {
                        bool isAvailable = false;
                        foreach (Vortice.Vulkan.VkExtensionProperties extension in availableExtensions)
                        {
                            FixedString extensionName = new(extension.extensionName);
                            if (extensionName == requiredExtension)
                            {
                                isAvailable = true;
                                break;
                            }
                        }

                        if (!isAvailable)
                        {
                            //required extensions missing
                            return 0;
                        }
                    }
                }
                else if (requiredExtensions.Length > 0)
                {
                    //required extensions missing
                    return 0;
                }

                Vortice.Vulkan.VkPhysicalDeviceProperties properties = physicalDevice.GetProperties();
                uint score = properties.limits.maxImageDimension2D;
                if (properties.deviceType == Vortice.Vulkan.VkPhysicalDeviceType.DiscreteGpu)
                {
                    //discrete gpus greatly preferred
                    score *= 1024;
                }

                return score;
            }
        }
    }
}