using Data;
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

        private readonly Destination destination;
        private readonly Instance instance;
        private readonly PhysicalDevice physicalDevice;
        private readonly UnmanagedDictionary<uint, CompiledShader> shaders;
        private readonly UnmanagedDictionary<uint, UnmanagedArray<CompiledPushConstant>> knownPushConstants;
        private readonly UnmanagedDictionary<uint, CompiledRenderer> renderers;
        private readonly UnmanagedDictionary<int, CompiledPipeline> pipelines;
        private readonly UnmanagedDictionary<int, CompiledMesh> meshes;
        private readonly UnmanagedDictionary<int, CompiledComponentBuffer> components;
        private readonly UnmanagedDictionary<int, CompiledImage> images;
        private readonly UnmanagedArray<CommandBuffer> commandBuffers;
        private readonly UnmanagedArray<Fence> submitFences;
        private readonly UnmanagedArray<Semaphore> pullSemaphores;
        private readonly UnmanagedArray<Semaphore> pushSemaphores;
        private readonly UnmanagedList<(uint, uint, uint)> previouslyRenderedGroups;
        private readonly UnmanagedList<uint> previouslyRenderedEntities;

        private readonly ComponentQuery<RendererScissor> scissorsQuery;
        private readonly UnmanagedArray<Vector4> scissors;

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

            if (instance.PhysicalDevices.length == 0)
            {
                throw new InvalidOperationException("No physical devices found");
            }

            if (TryGetBestPhysicalDevice(instance.PhysicalDevices, ["VK_KHR_swapchain"], out uint index))
            {
                physicalDevice = instance.PhysicalDevices[index];
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

            scissorsQuery = new();
            scissors = new();
        }

        public readonly void Dispose()
        {
            scissors.Dispose();
            scissorsQuery.Dispose();

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
            foreach (uint rendererEntity in renderers.Keys)
            {
                CompiledRenderer renderer = renderers[rendererEntity];
                renderer.Dispose();
            }

            renderers.Dispose();
        }

        private readonly void DisposePushConstants()
        {
            foreach (uint materialEntity in knownPushConstants.Keys)
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
            foreach (uint shaderEntity in shaders.Keys)
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
            foreach (int groupHash in meshes.Keys)
            {
                CompiledMesh compiledMesh = meshes[groupHash];
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
            USpan<RenderPass.Attachment> attachments =
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
                (width, height) = destination.Size;
                width = Math.Max(minWidth, Math.Min(maxWidth, width));
                height = Math.Max(minHeight, Math.Min(maxHeight, height));
                swapchain = new(logicalDevice, surface, width, height);
            }

            depthImage = new(swapchain, graphicsQueue);
        }

        private void CreateImageViewsAndBuffers(uint width, uint height)
        {
            USpan<Image> images = stackalloc Image[8];
            uint imageCount = swapchain.CopyImagesTo(images);
            surfaceImageViews = new(imageCount);
            surfaceFramebuffers = new(imageCount);
            for (uint i = 0; i < imageCount; i++)
            {
                ImageView imageView = new(images[i]);
                Framebuffer framebuffer = new(renderPass, [imageView, depthImage.imageView], width, height);
                surfaceImageViews[i] = imageView;
                surfaceFramebuffers[i] = framebuffer;
            }
        }

        private readonly bool IsDestinationResized()
        {
            (uint width, uint height) = destination.Size;
            return width != this.destinationWidth || height != this.destinationHeight;
        }

        private readonly CompiledShader CompileShader(World world, uint shader)
        {
            Shader shaderEntity = new(world, shader);
            ShaderModule vertexShader = new(logicalDevice, shaderEntity.GetVertexBytes());
            ShaderModule fragmentShader = new(logicalDevice, shaderEntity.GetFragmentBytes());
            return new(shaderEntity.GetVersion(), vertexShader, fragmentShader);
        }

        private readonly CompiledMesh CompileMesh(World world, uint shader, uint mesh)
        {
            Mesh meshEntity = new Entity(world, mesh).As<Mesh>();
            uint vertexCount = meshEntity.VertexCount;
            USpan<ShaderVertexInputAttribute> shaderVertexAttributes = world.GetArray<ShaderVertexInputAttribute>(shader);
            USpan<Mesh.Channel> channels = stackalloc Mesh.Channel[(int)shaderVertexAttributes.length];
            for (uint i = 0; i < shaderVertexAttributes.length; i++)
            {
                ShaderVertexInputAttribute vertexAttribute = shaderVertexAttributes[i];
                if (TryDeduceMeshChannel(vertexAttribute, out Mesh.Channel channel))
                {
                    if (!meshEntity.ContainsChannel(channel))
                    {
                        if (channel == Mesh.Channel.Color)
                        {
                            //safe to assume (1, 1, 1, 1) is default for colors if needed and its missing
                            Mesh.Collection<Color> defaultColors = meshEntity.CreateColors();
                            for (uint v = 0; v < vertexCount; v++)
                            {
                                defaultColors.Add(Color.White);
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException($"Mesh entity `{mesh}` is missing required `{channel}` channel");
                        }
                    }

                    channels[i] = channel;
                }
                else
                {
                    throw new InvalidOperationException($"Unable to deduce the mesh channel from property name `{vertexAttribute.name}`, name is too ambiguous");
                }
            }

            using UnmanagedList<float> vertexData = new();
            meshEntity.Assemble(vertexData, channels);
            uint indexCount = meshEntity.IndexCount;
            VertexBuffer vertexBuffer = new(graphicsQueue, commandPool, vertexData.AsSpan());
            IndexBuffer indexBuffer = new(graphicsQueue, commandPool, meshEntity.Indices.AsSpan());
            return new(meshEntity.GetVersion(), indexCount, vertexBuffer, indexBuffer, shaderVertexAttributes);
        }

        private readonly CompiledPipeline CompilePipeline(uint materialEntity, uint shaderEntity, World world, CompiledShader compiledShader, CompiledMesh compiledMesh)
        {
            Material material = new(world, materialEntity);
            USpan<ShaderVertexInputAttribute> shaderVertexAttributes = compiledMesh.VertexAttributes;
            USpan<VertexInputAttribute> vertexAttributes = stackalloc VertexInputAttribute[(int)shaderVertexAttributes.length];
            for (uint i = 0; i < shaderVertexAttributes.length; i++)
            {
                ShaderVertexInputAttribute shaderVertexAttribute = shaderVertexAttributes[i];
                vertexAttributes[i] = new(shaderVertexAttribute);
            }

            USpan<MaterialPushBinding> pushBindings = material.PushBindings;
            USpan<MaterialComponentBinding> uniformBindings = material.ComponentBindings;
            USpan<MaterialTextureBinding> textureBindings = material.TextureBindings;
            USpan<ShaderPushConstant> pushConstants = world.GetArray<ShaderPushConstant>(shaderEntity);
            USpan<ShaderUniformProperty> uniformProperties = world.GetArray<ShaderUniformProperty>(shaderEntity);
            USpan<ShaderSamplerProperty> samplerProperties = world.GetArray<ShaderSamplerProperty>(shaderEntity);

            //collect information to build the set layout
            uint totalCount = uniformBindings.length + textureBindings.length;
            USpan<(byte, VkDescriptorType, VkShaderStageFlags)> setLayoutBindings = stackalloc (byte, VkDescriptorType, VkShaderStageFlags)[(int)totalCount];
            uint bindingCount = 0;

            USpan<PipelineLayout.PushConstant> pushConstantsBuffer = stackalloc PipelineLayout.PushConstant[4];
            uint pushConstantsCount = 0;

            //cant have more than 1 push constant of the same type, so batch them into 1 vertex push constant
            //todo: fault: ^^^ what if theres fragment push constants? or geometry push constants? this will break
            if (pushConstants.length > 0)
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
                    throw new InvalidOperationException($"Material `{materialEntity}` is missing a `{typeof(MaterialComponentBinding).Name}` to bind a component to property at `{uniformProperty.label}`({uniformProperty.key.Binding})");
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
                    throw new InvalidOperationException($"Material `{materialEntity}` is missing a `{typeof(MaterialTextureBinding).Name}` to bind a texture to property at `{samplerProperty.name}`({samplerProperty.key.Binding})");
                }
            }

            ///create pipeline
            DescriptorSetLayout setLayout = new(logicalDevice, setLayoutBindings.Slice(0, bindingCount));
            PipelineCreateInput pipelineCreation = new(renderPass, compiledShader.vertexShader, compiledShader.fragmentShader, vertexAttributes);
            PipelineLayout pipelineLayout = new(logicalDevice, setLayout, pushConstantsBuffer.Slice(0, pushConstantsCount));
            Pipeline pipeline = new(pipelineCreation, pipelineLayout, "main");

            //create descriptor pool
            USpan<(VkDescriptorType, uint)> poolTypes = stackalloc (VkDescriptorType, uint)[2];
            uint poolCount = 0;
            if (uniformProperties.length > 0)
            {
                poolTypes[poolCount++] = (VkDescriptorType.UniformBuffer, uniformProperties.length);
            }

            if (samplerProperties.length > 0)
            {
                poolTypes[poolCount++] = (VkDescriptorType.CombinedImageSampler, samplerProperties.length);
            }

            //remember which bindings are push constants
            if (!knownPushConstants.TryGetValue(materialEntity, out UnmanagedArray<CompiledPushConstant> pushConstantArray))
            {
                pushConstantArray = new();
                knownPushConstants.Add(materialEntity, pushConstantArray);
            }

            if (pushBindings.length > 0)
            {
                USpan<CompiledPushConstant> buffer = stackalloc CompiledPushConstant[(int)pushBindings.length];
                for (uint i = 0; i < pushBindings.length; i++)
                {
                    MaterialPushBinding binding = pushBindings[i];
                    buffer[i] = new(binding.componentType, binding.stage);
                }

                pushConstantArray.Resize(buffer.length);
                pushConstantArray.CopyFrom(buffer);
            }

            //create buffers for bindings that arent push constants (referring to components on entities)
            VkPhysicalDeviceLimits limits = logicalDevice.physicalDevice.GetLimits();
            foreach (MaterialComponentBinding binding in uniformBindings)
            {
                uint componentEntity = binding.entity;
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
                uint textureEntity = binding.TextureEntity;
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
            DescriptorPool descriptorPool = new(logicalDevice, poolTypes.Slice(0, poolCount), maxSets);
            return new(pipeline, pipelineLayout, descriptorPool, setLayout, setLayoutBindings.Slice(0, bindingCount));
        }

        private readonly CompiledImage CompileImage(uint materialEntity, uint textureVersion, MaterialTextureBinding binding)
        {
            World world = destination.entity.world;
            uint depth = 1;
            VkImageUsageFlags usage = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled;
            VkFormat format = VkFormat.R8G8B8A8Srgb;
            uint textureEntity = binding.TextureEntity;
            IsTexture size = world.GetComponent<IsTexture>(textureEntity);
            Vector4 region = binding.Region;
            uint x = (uint)(region.X * size.width);
            uint y = (uint)(region.Y * size.height);
            uint width = (uint)(region.Z * size.width);
            uint height = (uint)(region.W * size.height);
            Image image = new(logicalDevice, width, height, depth, format, usage);
            DeviceMemory imageMemory = new(image, VkMemoryPropertyFlags.DeviceLocal);
            USpan<Pixel> pixels = world.GetArray<Pixel>(textureEntity);

            //copy pixels from the entity, into the temporary buffer, then temporary buffer copies into the buffer
            using BufferDeviceMemory tempStagingBuffer = new(logicalDevice, pixels.length * 4, VkBufferUsageFlags.TransferSrc, VkMemoryPropertyFlags.HostCoherent | VkMemoryPropertyFlags.HostVisible);
            tempStagingBuffer.CopyFrom(pixels);
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
                ref CompiledComponentBuffer componentBuffer = ref components[componentHash];
                uint entity = componentBuffer.containerEntity;
                RuntimeType componentType = componentBuffer.componentType;
                if (!world.ContainsEntity(entity))
                {
                    throw new InvalidOperationException($"Entity `{entity}` that contained component `{componentType}` with data for a uniform buffer has been lost");
                }

                if (!world.ContainsComponent(entity, componentType))
                {
                    throw new InvalidOperationException($"Component `{componentType}` on entity `{entity}` that used to contained data for a uniform buffer has been lost");
                }

                USpan<byte> componentData = world.GetComponentBytes(entity, componentType);
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
                ref CompiledImage image = ref images[textureHash];
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
            World world = destination.entity.world;
            Fence submitFence = submitFences[frameIndex];
            Semaphore pullSemaphore = pullSemaphores[frameIndex];
            //Semaphore pushSemaphore = pushSemaphores[frameIndex];
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
            scissorsQuery.Update(world);
            scissors.Resize(world.MaxEntityValue);
            scissors.Fill(new Vector4(0, 0, framebuffer.width, framebuffer.height));
            foreach (var s in scissorsQuery)
            {
                scissors[s.entity] = s.Component1.region;
            }

            return true;
        }

        public readonly void Render(USpan<uint> renderEntities, uint materialEntity, uint shaderEntity, uint meshEntity)
        {
            World world = destination.entity.world;
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
            if (!pipelines.TryGetValue(groupHash, out CompiledPipeline compiledPipeline))
            {
                compiledPipeline = CompilePipeline(materialEntity, shaderEntity, world, compiledShader, compiledMesh);
                pipelines.Add(groupHash, compiledPipeline);
            }

            //update images of bindings that change
            bool updateDescriptorSet = false;
            USpan<MaterialTextureBinding> textureBindings = world.GetArray<MaterialTextureBinding>(materialEntity);
            for (uint i = 0; i < textureBindings.length; i++)
            {
                ref MaterialTextureBinding textureBinding = ref textureBindings[i];
                int textureHash = GetTextureHash(materialEntity, textureBinding);
                if (images.ContainsKey(textureHash))
                {
                    ref CompiledImage image = ref images[textureHash];
                    if (image.binding.Version != textureBinding.Version)
                    {
                        logicalDevice.Wait();
                        image.Dispose();
                        uint textureVersion = world.GetComponent<IsTexture>(textureBinding.TextureEntity).version;
                        image = CompileImage(materialEntity, textureVersion, textureBinding);
                        updateDescriptorSet = true;
                    }
                }
            }

            if (meshChanged || shaderChanged || updateDescriptorSet)
            {
                logicalDevice.Wait();

                //need to dispose the descriptor sets before the descriptor pool is gone
                foreach (uint entity in renderEntities)
                {
                    if (renderers.TryRemove(entity, out CompiledRenderer renderer))
                    {
                        renderer.Dispose();
                    }
                }

                compiledPipeline.Dispose();
                compiledPipeline = CompilePipeline(materialEntity, shaderEntity, world, compiledShader, compiledMesh);
                pipelines[groupHash] = compiledPipeline;
            }

            //update descriptor sets if needed
            foreach (uint entity in renderEntities)
            {
                if (!renderers.ContainsKey(entity))
                {
                    if (!compiledPipeline.descriptorPool.TryAllocate(compiledPipeline.setLayout, out DescriptorSet descriptorSet))
                    {
                        throw new InvalidOperationException("Failed to allocate descriptor set");
                    }

                    CompiledRenderer renderer = new(descriptorSet);
                    renderers.Add(entity, renderer);
                    UpdateDescriptorSet(materialEntity, renderer.descriptorSet, compiledPipeline);
                }
            }

            //finally draw everything
            CommandBuffer commandBuffer = commandBuffers[frameIndex];
            commandBuffer.BindPipeline(compiledPipeline.pipeline, VkPipelineBindPoint.Graphics);
            commandBuffer.BindVertexBuffer(compiledMesh.vertexBuffer);
            commandBuffer.BindIndexBuffer(compiledMesh.indexBuffer);

            bool hasPushConstants = knownPushConstants.TryGetValue(materialEntity, out UnmanagedArray<CompiledPushConstant> pushConstants);
            foreach (uint rendererEntity in renderEntities)
            {
                //apply scissor
                Vector4 scissor = scissors[rendererEntity];
                commandBuffer.SetScissor(scissor);

                //push constants
                if (hasPushConstants)
                {
                    uint pushOffset = 0;
                    foreach (CompiledPushConstant pushConstant in pushConstants)
                    {
                        USpan<byte> componentBytes = world.GetComponentBytes(rendererEntity, pushConstant.componentType);
                        commandBuffer.PushConstants(compiledPipeline.pipelineLayout, GetShaderStage(pushConstant.stage), componentBytes, pushOffset);
                        pushOffset += componentBytes.length;
                    }
                }

                CompiledRenderer renderer = renderers[rendererEntity];
                commandBuffer.BindDescriptorSet(compiledPipeline.pipelineLayout, renderer.descriptorSet);
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
                foreach ((uint materialEntity, uint shaderEntity, uint meshEntity) in previouslyRenderedGroups)
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
                foreach ((uint materialEntity, uint shaderEntity, uint meshEntity) in previouslyRenderedGroups)
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
            foreach (uint rendererEntity in renderers.Keys)
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
                foreach ((uint materialEntity, uint shaderEntity, uint meshEntity) in previouslyRenderedGroups)
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
                foreach ((uint materialEntity, uint shaderEntity, uint meshEntity) in previouslyRenderedGroups)
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

        private readonly void UpdateDescriptorSet(uint materialEntity, DescriptorSet descriptorSet, CompiledPipeline pipeline)
        {
            World world = destination.entity.world;
            Material material = new(world, materialEntity);
            byte set = 0;
            foreach ((byte binding, VkDescriptorType type, _) in pipeline.Bindings)
            {
                if (type == VkDescriptorType.CombinedImageSampler)
                {
                    MaterialTextureBinding textureBinding = material.GetTextureBindingRef(binding, set);
                    int textureHash = GetTextureHash(materialEntity, textureBinding);
                    CompiledImage image = images[textureHash];
                    descriptorSet.Update(image.imageView, image.sampler, binding);
                }
                else if (type == VkDescriptorType.UniformBuffer)
                {
                    MaterialComponentBinding componentBinding = material.GetComponentBindingRef(binding, set);
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

        private static int GetGroupHash(uint materialEntity, uint meshEntity)
        {
            return HashCode.Combine(materialEntity, meshEntity);
        }

        private static int GetTextureHash(uint materialEntity, MaterialTextureBinding binding)
        {
            return HashCode.Combine(materialEntity, binding.Binding);
        }

        private static int GetComponentHash(uint materialEntity, MaterialComponentBinding binding)
        {
            return HashCode.Combine(materialEntity, binding);
        }

        private static bool TryGetBestPhysicalDevice(USpan<PhysicalDevice> physicalDevices, USpan<FixedString> requiredExtensions, out uint index)
        {
            uint highestScore = 0;
            index = uint.MaxValue;
            for (uint i = 0; i < physicalDevices.length; i++)
            {
                uint score = GetScore(physicalDevices[i], requiredExtensions);
                if (score > highestScore)
                {
                    highestScore = score;
                    index = i;
                }
            }

            return true;

            static unsafe uint GetScore(PhysicalDevice physicalDevice, USpan<FixedString> requiredExtensions)
            {
                VkPhysicalDeviceFeatures features = physicalDevice.GetFeatures();
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

                USpan<VkExtensionProperties> availableExtensions = physicalDevice.GetExtensions();
                if (availableExtensions.length > 0)
                {
                    foreach (FixedString requiredExtension in requiredExtensions)
                    {
                        bool isAvailable = false;
                        foreach (VkExtensionProperties extension in availableExtensions)
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
                else if (requiredExtensions.length > 0)
                {
                    //required extensions missing
                    return 0;
                }

                VkPhysicalDeviceProperties properties = physicalDevice.GetProperties();
                uint score = properties.limits.maxImageDimension2D;
                if (properties.deviceType == VkPhysicalDeviceType.DiscreteGpu)
                {
                    //discrete gpus greatly preferred
                    score *= 1024;
                }

                return score;
            }
        }

        private static bool TryDeduceMeshChannel(ShaderVertexInputAttribute attribute, out Mesh.Channel channel)
        {
            //get lowercase version
            USpan<char> nameBuffer = stackalloc char[(int)FixedString.MaxLength]; //<- only int allowed so cringe
            uint length = attribute.name.CopyTo(nameBuffer);
            for (uint i = 0; i < length; i++)
            {
                nameBuffer[i] = char.ToLower(nameBuffer[i]);
            }

            if (attribute.type == RuntimeType.Get<Vector2>())
            {
                if (nameBuffer.Slice(0, length).Contains("uv".AsSpan()))
                {
                    channel = Mesh.Channel.UV;
                    return true;
                }
            }
            else if (attribute.type == RuntimeType.Get<Vector3>())
            {
                if (nameBuffer.Slice(0, length).Contains("normal".AsSpan()))
                {
                    channel = Mesh.Channel.Normal;
                    return true;
                }
                else if (nameBuffer.Slice(0, length).Contains("tangent".AsSpan()))
                {
                    channel = Mesh.Channel.Tangent;
                    return true;
                }
                else if (nameBuffer.Slice(0, length).Contains("position".AsSpan()))
                {
                    channel = Mesh.Channel.Position;
                    return true;
                }
                else if (nameBuffer.Slice(0, length).Contains("bitangent".AsSpan()))
                {
                    channel = Mesh.Channel.BiTangent;
                    return true;
                }
            }
            else if (attribute.type == RuntimeType.Get<Vector4>())
            {
                if (nameBuffer.Slice(0, length).Contains("color".AsSpan()))
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