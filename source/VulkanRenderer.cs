using Materials;
using Meshes;
using Meshes.Components;
using Shaders;
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
        private readonly UnmanagedDictionary<eint, CompiledShader> shaders;
        private readonly UnmanagedDictionary<int, CompiledPipeline> pipelines;
        private readonly UnmanagedDictionary<int, CompiledMesh> meshes;
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

            shaders = new();
            pipelines = new();
            commandBuffers = new();
            submitFences = new();
            pullSemaphores = new();
            pushSemaphores = new();
            meshes = new();
        }

        public readonly void Dispose()
        {
            DisposeMeshes();
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
                CompiledPipeline pipeline = pipelines[hash];
                pipeline.Dispose();
            }

            pipelines.Dispose();
        }

        private readonly void DisposeShaderModules()
        {
            foreach (eint shaderEntity in shaders.Keys)
            {
                CompiledShader shaderModule = shaders[shaderEntity];
                shaderModule.Dispose();
            }

            shaders.Dispose();
        }

        private readonly void DisposeMeshes()
        {
            foreach (CompiledMesh compiledMesh in meshes.Values)
            {
                compiledMesh.Dispose();
            }

            meshes.Dispose();
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

        public readonly void Render(ReadOnlySpan<eint> entities, eint material, eint shader, eint mesh, eint camera)
        {
            World world = destination.entity.world;
            IsShader shaderComponent = world.GetComponent<IsShader>(shader);

            //make sure a shader exists for this shader entity, also rebuild it when version changes
            if (!shaders.TryGetValue(shader, out CompiledShader compiledShader))
            {
                compiledShader = CompileShader(world, shader);
                shaders.Add(shader, compiledShader);
            }

            bool shaderChanged = compiledShader.version != shaderComponent.version;
            if (shaderChanged)
            {
                compiledShader.Dispose();
                compiledShader = CompileShader(world, shader);
                shaders[shader] = compiledShader;
                //todo: efficiency: could use TryGetRef and AddRef instead to minimize instructions
            }

            //make sure a processed mesh exists for this combination of shader entity and mesh entity, also rebuild it when it changes
            int instanceHash = HashCode.Combine(shader, mesh);
            uint meshVersion = world.GetComponent<IsMesh>(mesh).version;
            if (!meshes.TryGetValue(instanceHash, out CompiledMesh compiledMesh))
            {
                compiledMesh = CompileMesh(world, shader, mesh);
                meshes.Add(instanceHash, compiledMesh);
            }

            bool meshChanged = compiledMesh.version != meshVersion;
            if (meshChanged || shaderChanged)
            {
                compiledMesh.Dispose();
                compiledMesh = CompileMesh(world, shader, mesh);
                meshes[instanceHash] = compiledMesh;
            }

            //make sure a pipeline exists, the same way a compiled mesh is
            if (!pipelines.TryGetValue(instanceHash, out CompiledPipeline pipeline))
            {
                pipeline = CompilePipeline(material, shader, world, compiledShader, compiledMesh);
                pipelines.Add(instanceHash, pipeline);
            }

            if (meshChanged || shaderChanged)
            {
                pipeline.Dispose();
                pipeline = CompilePipeline(material, shader, world, compiledShader, compiledMesh);
                pipelines[instanceHash] = pipeline;
            }

            CommandBuffer commandBuffer = commandBuffers[frameIndex];
            //commandBuffer.BindPipeline(pipeline.pipeline, VkPipelineBindPoint.Graphics);
        }

        private readonly CompiledPipeline CompilePipeline(eint material, eint shader, World world, CompiledShader compiledShader, CompiledMesh compiledMesh)
        {
            Material materialEntity = new(world, material);
            ReadOnlySpan<ShaderVertexInputAttribute> shaderVertexAttributes = compiledMesh.VertexAttributes;
            Span<VertexInputAttribute> vertexAttributes = stackalloc VertexInputAttribute[shaderVertexAttributes.Length];
            for (int i = 0; i < shaderVertexAttributes.Length; i++)
            {
                ShaderVertexInputAttribute shaderVertexAttribute = shaderVertexAttributes[i];
                vertexAttributes[i] = new(shaderVertexAttribute);
            }

            ReadOnlySpan<MaterialComponentBinding> uniformBindings = materialEntity.GetComponentBindings();
            ReadOnlySpan<MaterialTextureBinding> textureBindings = materialEntity.GetTextureBindings();
            ReadOnlySpan<ShaderUniformProperty> uniformProperties = world.GetList<ShaderUniformProperty>(shader).AsSpan();
            ReadOnlySpan<ShaderSamplerProperty> samplerProperties = world.GetList<ShaderSamplerProperty>(shader).AsSpan();

            //todo: qol: when theres missing bindings, reference an empty default one so shaders can assume empty data at the least
            Span<(uint, VkDescriptorType, VkShaderStageFlags)> setLayoutBindings = stackalloc (uint, VkDescriptorType, VkShaderStageFlags)[uniformBindings.Length + textureBindings.Length];
            int bindingCount = 0;
            foreach (ShaderUniformProperty uniformProperty in uniformProperties)
            {
                bool containsBinding = false;
                foreach (MaterialComponentBinding uniformBinding in uniformBindings)
                {
                    if (uniformBinding.key == uniformProperty.key)
                    {
                        containsBinding = true;
                        VkDescriptorType descriptorType = VkDescriptorType.UniformBuffer;
                        VkShaderStageFlags shaderStage = GetShaderStage(uniformBinding.stage);
                        setLayoutBindings[bindingCount++] = (uniformBinding.Binding, descriptorType, shaderStage);
                        break;
                    }
                }

                if (!containsBinding)
                {
                    throw new InvalidOperationException($"Material `{material}` is missing a `{typeof(MaterialComponentBinding)}` to bind an entity component to uniform named `{uniformProperty.name}`");
                }
            }

            foreach (ShaderSamplerProperty samplerProperty in samplerProperties)
            {
                bool containsBinding = false;
                foreach (MaterialTextureBinding textureBinding in textureBindings)
                {
                    if (textureBinding.key == samplerProperty.key)
                    {
                        containsBinding = true;
                        VkDescriptorType descriptorType = VkDescriptorType.CombinedImageSampler;
                        VkShaderStageFlags shaderStage = VkShaderStageFlags.Fragment;
                        setLayoutBindings[bindingCount++] = (textureBinding.Binding, descriptorType, shaderStage);
                        break;
                    }
                }

                if (!containsBinding)
                {
                    throw new InvalidOperationException($"Material `{material}` is missing a `{typeof(MaterialTextureBinding)}` to bind a texture entity to sampler named `{samplerProperty.name}`");
                }
            }

            DescriptorSetLayout setLayout = new(logicalDevice, setLayoutBindings);
            PipelineCreateInput pipelineCreation = new(renderPass, compiledShader.vertexShader, compiledShader.fragmentShader, vertexAttributes);

            Pipeline pipeline = new(pipelineCreation, setLayout, "main");
            Span<(VkDescriptorType, uint)> poolTypes = stackalloc (VkDescriptorType, uint)[2];
            int poolCount = 0;
            if (uniformProperties.Length > 0)
            {
                poolTypes[poolCount++] = (VkDescriptorType.UniformBuffer, (uint)uniformProperties.Length);
            }

            if (samplerProperties.Length > 0)
            {
                poolTypes[poolCount++] = (VkDescriptorType.CombinedImageSampler, (uint)samplerProperties.Length);
            }

            DescriptorPool descriptorPool = new(logicalDevice, poolTypes[..poolCount]);
            CompiledPipeline compiledPipeline = new(pipeline, descriptorPool);
            return compiledPipeline;
        }

        private readonly CompiledShader CompileShader(World world, eint shader)
        {
            Shader shaderEntity = new(world, shader);
            ShaderModule vertexShader = new(logicalDevice, shaderEntity.GetVertexBytes());
            ShaderModule fragmentShader = new(logicalDevice, shaderEntity.GetFragmentBytes());
            return new(shaderEntity.GetVersion(), vertexShader, fragmentShader);
        }

        private readonly CompiledMesh CompileMesh(World world, eint shader, eint mesh)
        {
            Mesh.ChannelMask mask = default;
            Mesh meshEntity = new(world, mesh);
            ReadOnlySpan<ShaderVertexInputAttribute> shaderVertexAttributes = world.GetList<ShaderVertexInputAttribute>(shader).AsSpan();
            foreach (ShaderVertexInputAttribute vertexAttribute in shaderVertexAttributes)
            {
                if (TryGetMeshChannel(vertexAttribute, out Mesh.Channel channel))
                {
                    if (meshEntity.ContainsChannel(channel))
                    {
                        mask.AddChannel(channel);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Mesh does not contain channel `{channel}` but shader `{shader}` expects it");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Unable to map attribute `{vertexAttribute.name}` to a mesh channel");
                }
            }

            using UnmanagedList<float> vertexData = new();
            uint vertexCount = meshEntity.Assemble(vertexData, mask);
            VertexBuffer vertexBuffer = new(graphicsQueue, commandPool, vertexData.AsSpan());
            IndexBuffer indexBuffer = new(graphicsQueue, commandPool, meshEntity.GetIndices().AsSpan());
            return new(meshEntity.GetVersion(), vertexBuffer, indexBuffer, shaderVertexAttributes);
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

        private static bool TryGetMeshChannel(ShaderVertexInputAttribute attribute, out Mesh.Channel channel)
        {
            if (attribute.type == RuntimeType.Get<Vector2>())
            {
                if (attribute.name.Contains("uv", StringComparison.OrdinalIgnoreCase))
                {
                    channel = Mesh.Channel.UV;
                    return true;
                }
            }
            else if (attribute.type == RuntimeType.Get<Vector3>())
            {
                if (attribute.name.Contains("normal", StringComparison.OrdinalIgnoreCase))
                {
                    channel = Mesh.Channel.Normal;
                    return true;
                }
                else if (attribute.name.Contains("tangent", StringComparison.OrdinalIgnoreCase))
                {
                    channel = Mesh.Channel.Tangent;
                    return true;
                }
                else if (attribute.name.Contains("position", StringComparison.OrdinalIgnoreCase))
                {
                    channel = Mesh.Channel.Position;
                    return true;
                }
                else if (attribute.name.Contains("bitangent", StringComparison.OrdinalIgnoreCase))
                {
                    channel = Mesh.Channel.BiTangent;
                    return true;
                }
            }
            else if (attribute.type == RuntimeType.Get<Vector4>())
            {
                if (attribute.name.Contains("color", StringComparison.OrdinalIgnoreCase))
                {
                    channel = Mesh.Channel.Color;
                    return true;
                }
            }

            channel = default;
            return false;
        }

        private static VkShaderStageFlags GetShaderStage(ShaderStage shaderStage)
        {
            return shaderStage switch
            {
                ShaderStage.Vertex => VkShaderStageFlags.Vertex,
                ShaderStage.Fragment => VkShaderStageFlags.Fragment,
                ShaderStage.Geometry => VkShaderStageFlags.Geometry,
                ShaderStage.Compute => VkShaderStageFlags.Compute,
                _ => throw new ArgumentOutOfRangeException(nameof(shaderStage), shaderStage, null),
            };
        }
    }
}