using Meshes;
using Meshes.Components;
using Rendering.Components;
using Shaders;
using Shaders.Components;
using Simulation;
using System;
using System.Numerics;
using Textures;
using Textures.Components;
using Unmanaged;
using Unmanaged.Collections;
using Vortice.Vulkan;
using Vulkan;

namespace Rendering.Vulkan
{
    internal struct VulkanRenderer : IDisposable
    {
        private const uint MaxFramesInFlight = 2;

        private readonly World world;
        private readonly Destination destination;
        private readonly Instance instance;
        private readonly PhysicalDevice physicalDevice;
        private readonly UnmanagedDictionary<eint, CompiledShader> shaders;
        private readonly UnmanagedDictionary<eint, UnmanagedArray<CompiledPushConstant>> knownPushConstants;
        private readonly UnmanagedDictionary<eint, CompiledRenderer> renderers;
        private readonly UnmanagedDictionary<int, CompiledPipeline> pipelines;
        private readonly UnmanagedDictionary<int, CompiledMesh> meshes;
        private readonly UnmanagedDictionary<int, CompiledComponentBuffer> components;
        private readonly UnmanagedDictionary<int, CompiledImage> images;
        private readonly UnmanagedArray<CommandBuffer> commandBuffers;
        private readonly UnmanagedArray<Fence> submitFences;
        private readonly UnmanagedArray<Semaphore> pullSemaphores;
        private readonly UnmanagedArray<Semaphore> pushSemaphores;
        private readonly UnmanagedList<(eint, eint, eint)> previouslyRenderedGroups;
        private readonly UnmanagedList<eint> previouslyRenderedEntities;

        private DateTime lastUnusuedCheck;
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
        private uint destinationWidth;
        private uint destinationHeight;

        public readonly nint Library => instance.Value.Handle;

        public VulkanRenderer(Destination destination, Instance instance)
        {
            this.destination = destination;
            this.instance = instance;
            this.world = destination.GetWorld();

            if (instance.PhysicalDevices.Length == 0)
            {
                throw new InvalidOperationException("No physical devices found");
            }

            if (TryGetBestPhysicalDevice(instance.PhysicalDevices, ["VK_KHR_swapchain"], out uint index))
            {
                physicalDevice = instance.PhysicalDevices[(int)index];
                Console.WriteLine($"Vulkan instance created for `{destination}`");
            }
            else
            {
                throw new InvalidOperationException("No suitable physical device found");
            }

            images = new();
            shaders = new();
            knownPushConstants = new();
            renderers = new();
            pipelines = new();
            commandBuffers = new();
            submitFences = new();
            pullSemaphores = new();
            pushSemaphores = new();
            previouslyRenderedGroups = new();
            previouslyRenderedEntities = new();
            meshes = new();
            components = new();
        }

        public readonly void Dispose()
        {
            if (surface != default)
            {
                logicalDevice.Wait();
                DisposeComponentBuffers();
                DisposeTextureBuffers();
                DisposePushConstants();
                DisposeRenderers();
                DisposeMeshes();
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
                previouslyRenderedEntities.Dispose();
                previouslyRenderedGroups.Dispose();

                commandPool.Dispose();
                logicalDevice.Dispose();
                surface.Dispose();
            }

            instance.Dispose();
            Console.WriteLine($"Vulkan instance finished for `{destination}`");
        }

        private readonly void DisposeRenderers()
        {
            foreach (eint rendererEntity in renderers.Keys)
            {
                CompiledRenderer renderer = renderers[rendererEntity];
                renderer.Dispose();
            }

            renderers.Dispose();
        }

        private readonly void DisposePushConstants()
        {
            foreach (eint materialEntity in knownPushConstants.Keys)
            {
                UnmanagedArray<CompiledPushConstant> pushConstantArray = knownPushConstants[materialEntity];
                pushConstantArray.Dispose();
            }

            knownPushConstants.Dispose();
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

        private readonly void DisposeComponentBuffers()
        {
            foreach (int componentHash in components.Keys)
            {
                CompiledComponentBuffer componentBuffer = components[componentHash];
                componentBuffer.Dispose();
            }

            components.Dispose();
        }

        private readonly void DisposeTextureBuffers()
        {
            foreach (int textureHash in images.Keys)
            {
                CompiledImage image = images[textureHash];
                image.Dispose();
            }

            images.Dispose();
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
            //todo: fault: should also rebuild the render pass when moving a window to hdr from sdr monitors
            logicalDevice.Wait();
            DisposeSwapchain();
            CreateSwapchain(out destinationWidth, out destinationHeight);
            CreateImageViewsAndBuffers(destinationWidth, destinationHeight);
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

        private readonly bool IsDestinationResized()
        {
            (uint width, uint height) = destination.GetDestinationSize();
            return width != this.destinationWidth || height != this.destinationHeight;
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
            Mesh meshEntity = new(world, mesh);
            ReadOnlySpan<ShaderVertexInputAttribute> shaderVertexAttributes = world.GetList<ShaderVertexInputAttribute>(shader).AsSpan();
            Span<Mesh.Channel> channels = stackalloc Mesh.Channel[shaderVertexAttributes.Length];
            for (int i = 0; i < shaderVertexAttributes.Length; i++)
            {
                ShaderVertexInputAttribute vertexAttribute = shaderVertexAttributes[i];
                if (TryGetMeshChannel(vertexAttribute, out Mesh.Channel channel))
                {
                    if (meshEntity.ContainsChannel(channel))
                    {
                        channels[i] = channel;
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
            meshEntity.Assemble(vertexData, channels);
            uint indexCount = meshEntity.GetIndexCount();
            VertexBuffer vertexBuffer = new(graphicsQueue, commandPool, vertexData.AsSpan());
            IndexBuffer indexBuffer = new(graphicsQueue, commandPool, meshEntity.GetIndices().AsSpan());
            return new(meshEntity.GetVersion(), indexCount, vertexBuffer, indexBuffer, shaderVertexAttributes);
        }

        private readonly CompiledPipeline CompilePipeline(eint materialEntity, eint shaderEntity, World world, CompiledShader compiledShader, CompiledMesh compiledMesh)
        {
            Material material = new(world, materialEntity);
            ReadOnlySpan<ShaderVertexInputAttribute> shaderVertexAttributes = compiledMesh.VertexAttributes;
            Span<VertexInputAttribute> vertexAttributes = stackalloc VertexInputAttribute[shaderVertexAttributes.Length];
            for (int i = 0; i < shaderVertexAttributes.Length; i++)
            {
                ShaderVertexInputAttribute shaderVertexAttribute = shaderVertexAttributes[i];
                vertexAttributes[i] = new(shaderVertexAttribute);
            }

            ReadOnlySpan<MaterialPushBinding> pushBindings = material.GetPushBindings();
            ReadOnlySpan<MaterialComponentBinding> uniformBindings = material.GetComponentBindings();
            ReadOnlySpan<MaterialTextureBinding> textureBindings = material.GetTextureBindings();
            ReadOnlySpan<ShaderPushConstant> pushConstants = world.GetList<ShaderPushConstant>(shaderEntity).AsSpan();
            ReadOnlySpan<ShaderUniformProperty> uniformProperties = world.GetList<ShaderUniformProperty>(shaderEntity).AsSpan();
            ReadOnlySpan<ShaderSamplerProperty> samplerProperties = world.GetList<ShaderSamplerProperty>(shaderEntity).AsSpan();

            //collect information to build the set layout
            Span<(byte, VkDescriptorType, VkShaderStageFlags)> setLayoutBindings = stackalloc (byte, VkDescriptorType, VkShaderStageFlags)[uniformBindings.Length + textureBindings.Length];
            int bindingCount = 0;

            Span<PipelineLayout.PushConstant> pushConstantsBuffer = stackalloc PipelineLayout.PushConstant[4];
            int pushConstantsCount = 0;

            //cant have more than 1 push constant of the same type, so batch them into 1 vertex push constant
            //todo: fault: what if theres fragment push constants? or geometry push constants? this will break
            if (pushConstants.Length > 0)
            {
                uint start = 0;
                uint size = 0;
                foreach (ShaderPushConstant pushConstant in pushConstants)
                {
                    start = Math.Min(start, pushConstant.offset);
                    size += pushConstant.size;
                    bool containsPush = false;
                    foreach (MaterialPushBinding pushBinding in pushBindings)
                    {
                        if (pushBinding.componentType.Size == pushConstant.size && pushBinding.start == pushConstant.offset)
                        {
                            containsPush = true;
                            break;
                        }
                    }

                    if (!containsPush)
                    {
                        throw new InvalidOperationException($"Material `{materialEntity}` is missing a `{typeof(MaterialPushBinding)}` to bind a push constant named `{pushConstant.memberName}`");
                    }
                }

                pushConstantsBuffer[pushConstantsCount++] = new(start, size, VkShaderStageFlags.Vertex);
            }

            foreach (ShaderUniformProperty uniformProperty in uniformProperties)
            {
                bool containsBinding = false;
                foreach (MaterialComponentBinding uniformBinding in uniformBindings)
                {
                    VkShaderStageFlags shaderStage = GetShaderStage(uniformBinding.stage);
                    if (uniformBinding.key == uniformProperty.key)
                    {
                        containsBinding = true;
                        VkDescriptorType descriptorType = VkDescriptorType.UniformBuffer;
                        setLayoutBindings[bindingCount++] = (uniformBinding.Binding, descriptorType, shaderStage);
                        break;
                    }
                }

                if (!containsBinding)
                {
                    throw new InvalidOperationException($"Material `{materialEntity}` is missing a `{typeof(MaterialComponentBinding)}` to bind an entity component to uniform named `{uniformProperty.name}`");
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
                    throw new InvalidOperationException($"Material `{materialEntity}` is missing a `{typeof(MaterialTextureBinding)}` to bind a texture entity to sampler named `{samplerProperty.name}`");
                }
            }

            ///create pipeline
            DescriptorSetLayout setLayout = new(logicalDevice, setLayoutBindings[..bindingCount]);
            PipelineCreateInput pipelineCreation = new(renderPass, compiledShader.vertexShader, compiledShader.fragmentShader, vertexAttributes);
            PipelineLayout pipelineLayout = new(logicalDevice, setLayout, pushConstantsBuffer[..pushConstantsCount]);
            Pipeline pipeline = new(pipelineCreation, pipelineLayout, "main");

            //create descriptor pool
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

            //remember which bindings are push constants
            if (!knownPushConstants.TryGetValue(materialEntity, out UnmanagedArray<CompiledPushConstant> pushConstantArray))
            {
                pushConstantArray = new();
                knownPushConstants.Add(materialEntity, pushConstantArray);
            }

            if (pushBindings.Length > 0)
            {
                Span<CompiledPushConstant> buffer = stackalloc CompiledPushConstant[pushBindings.Length];
                for (int i = 0; i < pushBindings.Length; i++)
                {
                    MaterialPushBinding binding = pushBindings[i];
                    buffer[i] = new(binding.componentType, binding.stage);
                }

                pushConstantArray.Resize((uint)buffer.Length);
                pushConstantArray.CopyFrom(buffer);
            }

            //create buffers for bindings that arent push constants (referring to components on entities)
            VkPhysicalDeviceLimits limits = logicalDevice.physicalDevice.GetLimits();
            foreach (MaterialComponentBinding binding in uniformBindings)
            {
                eint componentEntity = binding.entity;
                if (componentEntity == default)
                {
                    //this binding is a push constant
                }
                else
                {
                    RuntimeType componentType = binding.componentType;
                    if (!world.ContainsEntity(componentEntity))
                    {
                        throw new InvalidOperationException($"Material `{materialEntity}` references entity `{componentEntity}` for a component `{componentType}`, which does not exist");
                    }

                    if (!world.ContainsComponent(componentEntity, componentType))
                    {
                        throw new InvalidOperationException($"Material `{materialEntity}` references entity `{componentEntity}` for a component `{componentType}`, but it is missing");
                    }

                    int componentHash = GetComponentHash(materialEntity, binding);
                    if (!components.TryGetValue(componentHash, out CompiledComponentBuffer componentBuffer))
                    {
                        uint typeSize = componentType.Size;
                        uint bufferSize = (uint)(Math.Ceiling(typeSize / (float)limits.minUniformBufferOffsetAlignment) * limits.minUniformBufferOffsetAlignment);
                        VkBufferUsageFlags usage = VkBufferUsageFlags.UniformBuffer;
                        VkMemoryPropertyFlags flags = VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent;
                        BufferDeviceMemory buffer = new(logicalDevice, bufferSize, usage, flags);
                        componentBuffer = new(materialEntity, binding.entity, componentType, buffer);
                        components.Add(componentHash, componentBuffer);
                    }
                }
            }

            //create buffers for texture bindings
            foreach (MaterialTextureBinding binding in textureBindings)
            {
                eint textureEntity = binding.TextureEntity;
                if (!world.ContainsEntity(textureEntity))
                {
                    throw new InvalidOperationException($"Material `{materialEntity}` references texture entity `{textureEntity}`, which does not exist");
                }

                if (!world.ContainsComponent<IsTexture>(textureEntity))
                {
                    throw new InvalidOperationException($"Material `{materialEntity}` references entity `{textureEntity}` that doesn't qualify as a texture");
                }

                int textureHash = GetTextureHash(materialEntity, binding);
                if (!images.TryGetValue(textureHash, out CompiledImage compiledImage))
                {
                    uint textureVersion = world.GetComponent<IsTexture>(textureEntity).version;
                    compiledImage = CompileImage(materialEntity, textureVersion, binding);
                    images.Add(textureHash, compiledImage);
                }
            }

            uint maxSets = 32; //todo: fault: after 32 allocations it should fail, where another pool should be created????
            DescriptorPool descriptorPool = new(logicalDevice, poolTypes[..poolCount], maxSets);
            return new(pipeline, pipelineLayout, descriptorPool, setLayout, setLayoutBindings[..bindingCount]);
        }

        private readonly CompiledImage CompileImage(eint materialEntity, uint textureVersion, MaterialTextureBinding binding)
        {
            uint depth = 1;
            VkImageUsageFlags usage = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled;
            VkFormat format = VkFormat.R8G8B8A8Srgb;
            eint textureEntity = binding.TextureEntity;
            TextureSize size = world.GetComponent<TextureSize>(textureEntity);
            Vector4 region = binding.Region;
            uint x = (uint)(region.X * size.width);
            uint y = (uint)(region.Y * size.height);
            uint width = (uint)(region.Z * size.width);
            uint height = (uint)(region.W * size.height);
            Image image = new(logicalDevice, width, height, depth, format, usage);
            DeviceMemory imageMemory = new(image, VkMemoryPropertyFlags.DeviceLocal);
            UnmanagedList<Pixel> pixels = world.GetList<Pixel>(textureEntity);

            //copy pixels from the entity, into the temporary buffer, then temporary buffer copies into the buffer
            using BufferDeviceMemory tempStagingBuffer = new(logicalDevice, pixels.Count * 4, VkBufferUsageFlags.TransferSrc, VkMemoryPropertyFlags.HostCoherent | VkMemoryPropertyFlags.HostVisible);
            tempStagingBuffer.CopyFrom(pixels.AsSpan());
            VkImageLayout imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
            using CommandPool tempPool = new(graphicsQueue, true);
            using CommandBuffer tempBuffer = tempPool.CreateCommandBuffer();
            tempBuffer.Begin();
            tempBuffer.TransitionImageLayout(image, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal);
            tempBuffer.End();
            graphicsQueue.Submit(tempBuffer);
            graphicsQueue.Wait();
            tempBuffer.Begin();
            tempBuffer.CopyBufferToImage(tempStagingBuffer.buffer, size.width, size.height, x, y, image, depth);
            tempBuffer.End();
            graphicsQueue.Submit(tempBuffer);
            graphicsQueue.Wait();
            tempBuffer.Begin();
            tempBuffer.TransitionImageLayout(image, VkImageLayout.TransferDstOptimal, imageLayout);
            tempBuffer.End();
            graphicsQueue.Submit(tempBuffer);
            graphicsQueue.Wait();

            ImageView imageView = new(image);
            SamplerCreateParameters samplerParameters = new();
            Sampler sampler = new(logicalDevice, samplerParameters);
            return new(materialEntity, textureVersion, binding, image, imageView, imageMemory, sampler);
        }

        /// <summary>
        /// Copies data from components into the uniform buffers for material bindings.
        /// </summary>
        private readonly void UpdateComponentBuffers(World world)
        {
            foreach (int componentHash in components.Keys)
            {
                ref CompiledComponentBuffer componentBuffer = ref components.GetRef(componentHash);
                eint entity = componentBuffer.containerEntity;
                RuntimeType componentType = componentBuffer.componentType;
                if (!world.ContainsEntity(entity))
                {
                    throw new InvalidOperationException($"Entity `{entity}` that contained component `{componentType}` with data for a uniform buffer has been lost");
                }

                if (!world.ContainsComponent(entity, componentType))
                {
                    throw new InvalidOperationException($"Component `{componentType}` on entity `{entity}` that used to contained data for a uniform buffer has been lost");
                }

                Span<byte> componentData = world.GetComponentBytes(entity, componentType);
                componentBuffer.buffer.CopyFrom(componentData);
            }
        }

        /// <summary>
        /// Rebuilds textures for still used materials when their source updates.
        /// </summary>
        private readonly void UpdateTextureBuffers(World world)
        {
            foreach (int textureHash in images.Keys)
            {
                ref CompiledImage image = ref images.GetRef(textureHash);
                Material material = new(world, image.materialEntity);
                if (material.TryGetTextureBinding(image.binding.TextureEntity, out MaterialTextureBinding binding))
                {
                    uint textureVersion = world.GetComponent<IsTexture>(binding.TextureEntity).version;
                    if (image.textureVersion != textureVersion)
                    {
                        //todo: untested: (triggered when the texture's pixel array changes)
                        logicalDevice.Wait();
                        image.Dispose();
                        image = CompileImage(image.materialEntity, textureVersion, binding);
                    }
                }
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
            Vector4 area = new(0, 0, framebuffer.width, framebuffer.height);
            Vector4 clearColor = new(0, 0, 0, 1);
            commandBuffer.BeginRenderPass(renderPass, framebuffer, area, clearColor);

            Vector4 viewport = new(0, framebuffer.height, framebuffer.width, -framebuffer.height);
            commandBuffer.SetViewport(viewport);

            Vector4 scissor = new(0, 0, framebuffer.width, framebuffer.height);
            commandBuffer.SetScissor(scissor);

            UpdateComponentBuffers(world);
            UpdateTextureBuffers(world);
            return true;
        }

        public readonly void Render(ReadOnlySpan<eint> renderEntities, eint materialEntity, eint shaderEntity, eint meshEntity)
        {
            IsShader shaderComponent = world.GetComponent<IsShader>(shaderEntity);

            //make sure a shader exists for this shader entity, also rebuild it when version changes
            if (!shaders.TryGetValue(shaderEntity, out CompiledShader compiledShader))
            {
                compiledShader = CompileShader(world, shaderEntity);
                shaders.Add(shaderEntity, compiledShader);
            }

            bool shaderChanged = compiledShader.version != shaderComponent.version;
            if (shaderChanged)
            {
                logicalDevice.Wait();
                compiledShader.Dispose();
                compiledShader = CompileShader(world, shaderEntity);
                shaders[shaderEntity] = compiledShader;
            }

            //make sure a processed mesh exists for this combination of shader entity and mesh entity, also rebuild it when it changes
            int groupHash = GetGroupHash(materialEntity, meshEntity);
            uint meshVersion = world.GetComponent<IsMesh>(meshEntity).version;
            if (!meshes.TryGetValue(groupHash, out CompiledMesh compiledMesh))
            {
                compiledMesh = CompileMesh(world, shaderEntity, meshEntity);
                meshes.Add(groupHash, compiledMesh);
            }

            bool meshChanged = compiledMesh.version != meshVersion;
            if (meshChanged || shaderChanged)
            {
                logicalDevice.Wait();
                compiledMesh.Dispose();
                compiledMesh = CompileMesh(world, shaderEntity, meshEntity);
                meshes[groupHash] = compiledMesh;
            }

            //make sure a pipeline exists, the same way a compiled mesh is
            if (!pipelines.TryGetValue(groupHash, out CompiledPipeline pipeline))
            {
                pipeline = CompilePipeline(materialEntity, shaderEntity, world, compiledShader, compiledMesh);
                pipelines.Add(groupHash, pipeline);
            }

            //update images of bindings that change
            bool updateDescriptorSet = false;
            UnmanagedList<MaterialTextureBinding> textureBindings = world.GetList<MaterialTextureBinding>(materialEntity);
            for (uint i = 0; i < textureBindings.Count; i++)
            {
                ref MaterialTextureBinding textureBinding = ref textureBindings.GetRef(i);
                int textureHash = GetTextureHash(materialEntity, textureBinding);
                if (images.TryGetValue(textureHash, out CompiledImage image))
                {
                    if (image.binding.Version != textureBinding.Version)
                    {
                        logicalDevice.Wait();
                        image.Dispose();
                        uint textureVersion = world.GetComponent<IsTexture>(textureBinding.TextureEntity).version;
                        image = CompileImage(materialEntity, textureVersion, textureBinding);
                        images[textureHash] = image;
                        updateDescriptorSet = true;
                    }
                }
            }

            if (meshChanged || shaderChanged || updateDescriptorSet)
            {
                logicalDevice.Wait();

                //need to dispose the descriptor sets before the descriptor pool is gone
                foreach (eint entity in renderEntities)
                {
                    if (renderers.TryRemove(entity, out CompiledRenderer renderer))
                    {
                        renderer.Dispose();
                    }
                }

                pipeline.Dispose();
                pipeline = CompilePipeline(materialEntity, shaderEntity, world, compiledShader, compiledMesh);
                pipelines[groupHash] = pipeline;
            }

            //update descriptor sets if needed
            foreach (eint entity in renderEntities)
            {
                if (!renderers.ContainsKey(entity))
                {
                    if (!pipeline.descriptorPool.TryAllocate(pipeline.setLayout, out DescriptorSet descriptorSet))
                    {
                        throw new InvalidOperationException("Failed to allocate descriptor set");
                    }

                    CompiledRenderer renderer = new(descriptorSet);
                    renderers.Add(entity, renderer);
                    UpdateDescriptorSet(materialEntity, renderer.descriptorSet, pipeline);
                }
            }

            //finally draw everything
            CommandBuffer commandBuffer = commandBuffers[frameIndex];
            commandBuffer.BindPipeline(pipeline.pipeline, VkPipelineBindPoint.Graphics);
            commandBuffer.BindVertexBuffer(compiledMesh.vertexBuffer);
            commandBuffer.BindIndexBuffer(compiledMesh.indexBuffer);

            bool hasPushConstants = knownPushConstants.TryGetValue(materialEntity, out UnmanagedArray<CompiledPushConstant> pushConstants);
            foreach (eint rendererEntity in renderEntities)
            {
                //push constants
                if (hasPushConstants)
                {
                    uint pushOffset = 0;
                    foreach (CompiledPushConstant pushConstant in pushConstants)
                    {
                        Span<byte> componentBytes = world.GetComponentBytes(rendererEntity, pushConstant.componentType);
                        commandBuffer.PushConstants(pipeline.pipelineLayout, GetShaderStage(pushConstant.stage), componentBytes, pushOffset);
                        pushOffset += (uint)componentBytes.Length;
                    }
                }

                CompiledRenderer renderer = renderers[rendererEntity];
                commandBuffer.BindDescriptorSet(pipeline.pipelineLayout, renderer.descriptorSet);
                commandBuffer.DrawIndexed(compiledMesh.indexCount, 1, 0, 0, 0);

                previouslyRenderedEntities.Add(rendererEntity);
            }

            previouslyRenderedGroups.Add((materialEntity, shaderEntity, meshEntity));
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

            //check for undisposed objects every 3 seconds
            DateTime now = DateTime.UtcNow;
            TimeSpan timeSinceLastCheck = now - lastUnusuedCheck;
            if (timeSinceLastCheck.TotalSeconds > 3)
            {
                lastUnusuedCheck = now;
                DisposeUnusued();
            }
        }

        private readonly void DisposeUnusued()
        {
            //dispose unusued buffers
            foreach (int componentHash in components.Keys)
            {
                CompiledComponentBuffer component = components[componentHash];
                bool used = false;
                foreach ((eint materialEntity, eint shaderEntity, eint meshEntity) in previouslyRenderedGroups)
                {
                    if (materialEntity == component.materialEntity)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    logicalDevice.Wait();
                    component.Dispose();
                    components.Remove(componentHash);
                }
            }

            //dispose unused textures
            foreach (int textureHash in images.Keys)
            {
                CompiledImage image = images[textureHash];
                bool used = false;
                foreach ((eint materialEntity, eint shaderEntity, eint meshEntity) in previouslyRenderedGroups)
                {
                    if (materialEntity == image.materialEntity)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    logicalDevice.Wait();
                    image.Dispose();
                    images.Remove(textureHash);
                }
            }

            //dispose unused renderers
            foreach (eint rendererEntity in renderers.Keys)
            {
                bool used = previouslyRenderedEntities.Contains(rendererEntity);
                if (!used)
                {
                    logicalDevice.Wait();
                    CompiledRenderer renderer = renderers.Remove(rendererEntity);
                    renderer.Dispose();
                }
            }

            //dispose unused meshes
            foreach (int groupHash in meshes.Keys)
            {
                bool used = false;
                foreach ((eint materialEntity, eint shaderEntity, eint meshEntity) in previouslyRenderedGroups)
                {
                    int usedGroupHash = GetGroupHash(materialEntity, meshEntity);
                    if (usedGroupHash == groupHash)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    logicalDevice.Wait();
                    CompiledMesh mesh = meshes.Remove(groupHash);
                    mesh.Dispose();
                }
            }

            //dispose unused pipelines
            foreach (int groupHash in pipelines.Keys)
            {
                bool used = false;
                foreach ((eint materialEntity, eint shaderEntity, eint meshEntity) in previouslyRenderedGroups)
                {
                    int usedGroupHash = GetGroupHash(materialEntity, meshEntity);
                    if (usedGroupHash == groupHash)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    logicalDevice.Wait();
                    CompiledPipeline pipeline = pipelines.Remove(groupHash);
                    pipeline.Dispose();
                }
            }

            previouslyRenderedGroups.Clear();
            previouslyRenderedEntities.Clear();
        }

        private readonly void UpdateDescriptorSet(eint materialEntity, DescriptorSet descriptorSet, CompiledPipeline pipeline)
        {
            Material material = new(world, materialEntity);
            foreach ((byte binding, VkDescriptorType type, VkShaderStageFlags flags) in pipeline.Bindings)
            {
                if (type == VkDescriptorType.CombinedImageSampler)
                {
                    MaterialTextureBinding textureBinding = material.GetTextureBindingRef(binding);
                    int textureHash = GetTextureHash(materialEntity, textureBinding);
                    CompiledImage image = images[textureHash];
                    descriptorSet.Update(image.imageView, image.sampler, binding);
                }
                else if (type == VkDescriptorType.UniformBuffer)
                {
                    MaterialComponentBinding componentBinding = material.GetComponentBindingRef(binding);
                    int componentHash = GetComponentHash(materialEntity, componentBinding);
                    CompiledComponentBuffer componentBuffer = components[componentHash];
                    descriptorSet.Update(componentBuffer.buffer.buffer, binding);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported descriptor type `{type}`");
                }
            }
        }

        private static int GetGroupHash(eint materialEntity, eint meshEntity)
        {
            return HashCode.Combine(materialEntity, meshEntity);
        }

        private static int GetTextureHash(eint materialEntity, MaterialTextureBinding binding)
        {
            return HashCode.Combine(materialEntity, binding.Binding);
        }

        private static int GetComponentHash(eint materialEntity, MaterialComponentBinding binding)
        {
            return HashCode.Combine(materialEntity, binding);
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