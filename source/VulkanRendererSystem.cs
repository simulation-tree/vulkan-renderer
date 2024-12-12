using Collections;
using Data;
using Meshes;
using Meshes.Components;
using Rendering.Components;
using Shaders;
using Shaders.Components;
using System;
using System.Diagnostics;
using System.Numerics;
using Textures;
using Textures.Components;
using Unmanaged;
using Vortice.Vulkan;
using Vulkan;
using Worlds;

namespace Rendering.Vulkan
{
    internal struct VulkanRendererSystem : IDisposable
    {
        private const uint MaxFramesInFlight = 2;

        private readonly Destination destination;
        private readonly Instance instance;
        private readonly PhysicalDevice physicalDevice;
        private readonly Dictionary<uint, CompiledShader> shaders;
        private readonly Dictionary<uint, Array<CompiledPushConstant>> knownPushConstants;
        private readonly Dictionary<uint, CompiledRenderer> renderers;
        private readonly Dictionary<RendererKey, CompiledPipeline> pipelines;
        private readonly Dictionary<RendererKey, CompiledMesh> meshes;
        private readonly Dictionary<int, CompiledComponentBuffer> components;
        private readonly Dictionary<int, CompiledImage> images;
        private readonly Array<CommandBuffer> commandBuffers;
        private readonly Array<Fence> inFlightFences;
        private readonly Array<Semaphore> imageAvailableSemaphores;
        private readonly Array<Semaphore> renderFinishedSemaphores;
        private readonly List<(uint, uint, uint)> previouslyRenderedGroups;
        private readonly List<uint> previouslyRenderedEntities;
        private readonly Array<Vector4> scissors;

        private DateTime lastUnusuedCheck;
        private Array<ImageView> surfaceImageViews;
        private Array<Framebuffer> swapChainFramebuffers;
        private LogicalDevice logicalDevice;
        private Surface surface;
        private Swapchain swapchain;
        private Queue graphicsQueue;
        private Queue presentationQueue;
        private RenderPass renderPass;
        private CommandPool commandPool;
        private DepthImage depthImage;
        private uint currentFrame;
        private uint imageIndex;
        private uint destinationWidth;
        private uint destinationHeight;

        public readonly nint Library => instance.Value.Handle;

        public VulkanRendererSystem(Destination destination, Instance instance)
        {
            this.destination = destination;
            this.instance = instance;

            if (instance.PhysicalDevices.Length == 0)
            {
                throw new InvalidOperationException("No physical devices found");
            }

            if (TryGetBestPhysicalDevice(instance.PhysicalDevices, ["VK_KHR_swapchain"], out uint index))
            {
                physicalDevice = instance.PhysicalDevices[index];
                Trace.WriteLine($"Vulkan instance created for `{destination}`");
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
            inFlightFences = new();
            imageAvailableSemaphores = new();
            renderFinishedSemaphores = new();
            previouslyRenderedGroups = new();
            previouslyRenderedEntities = new();
            meshes = new();
            components = new();
            scissors = new();
        }

        public readonly void Dispose()
        {
            scissors.Dispose();

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
                    inFlightFences[i].Dispose();
                    imageAvailableSemaphores[i].Dispose();
                    renderFinishedSemaphores[i].Dispose();
                }

                commandBuffers.Dispose();
                inFlightFences.Dispose();
                imageAvailableSemaphores.Dispose();
                renderFinishedSemaphores.Dispose();
                previouslyRenderedEntities.Dispose();
                previouslyRenderedGroups.Dispose();

                commandPool.Dispose();
                logicalDevice.Dispose();
                surface.Dispose();
            }

            instance.Dispose();
            Trace.WriteLine($"Vulkan instance finished for `{destination}`");
        }

        private readonly void DisposeRenderers()
        {
            foreach (uint rendererEntity in renderers.Keys)
            {
                ref CompiledRenderer renderer = ref renderers[rendererEntity];
                renderer.Dispose();
            }

            renderers.Dispose();
        }

        private readonly void DisposePushConstants()
        {
            foreach (uint materialEntity in knownPushConstants.Keys)
            {
                Array<CompiledPushConstant> pushConstantArray = knownPushConstants[materialEntity];
                pushConstantArray.Dispose();
            }

            knownPushConstants.Dispose();
        }

        private readonly void DisposePipelines()
        {
            foreach (RendererKey hash in pipelines.Keys)
            {
                ref CompiledPipeline pipeline = ref pipelines[hash];
                pipeline.Dispose();
            }

            pipelines.Dispose();
        }

        private readonly void DisposeShaderModules()
        {
            foreach (uint shaderEntity in shaders.Keys)
            {
                ref CompiledShader shaderModule = ref shaders[shaderEntity];
                shaderModule.Dispose();
            }

            shaders.Dispose();
        }

        private readonly void DisposeComponentBuffers()
        {
            foreach (int componentHash in components.Keys)
            {
                ref CompiledComponentBuffer componentBuffer = ref components[componentHash];
                componentBuffer.Dispose();
            }

            components.Dispose();
        }

        private readonly void DisposeTextureBuffers()
        {
            foreach (int textureHash in images.Keys)
            {
                ref CompiledImage image = ref images[textureHash];
                image.Dispose();
            }

            images.Dispose();
        }

        private readonly void DisposeMeshes()
        {
            foreach (RendererKey key in meshes.Keys)
            {
                ref CompiledMesh compiledMesh = ref meshes[key];
                compiledMesh.Dispose();
            }

            meshes.Dispose();
        }

        private readonly void DisposeSwapchain()
        {
            foreach (Framebuffer framebuffer in swapChainFramebuffers)
            {
                framebuffer.Dispose();
            }

            swapChainFramebuffers.Dispose();

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
            CreateSwapchain(out destinationWidth, out destinationHeight);
            USpan<RenderPass.Attachment> attachments =
            [
                new(swapchain.format, VkSampleCountFlags.Count1, VkAttachmentLoadOp.Clear, VkAttachmentStoreOp.Store, VkAttachmentLoadOp.DontCare,
                    VkAttachmentStoreOp.DontCare, VkImageLayout.Undefined, VkImageLayout.PresentSrcKHR),
                new(logicalDevice.GetDepthFormat(), VkSampleCountFlags.Count1, VkAttachmentLoadOp.Clear, VkAttachmentStoreOp.DontCare, VkAttachmentLoadOp.DontCare,
                    VkAttachmentStoreOp.DontCare, VkImageLayout.DepthStencilAttachmentOptimal, VkImageLayout.DepthStencilAttachmentOptimal),
            ];

            renderPass = new(logicalDevice, attachments);
            CreateImageViewsAndBuffers(destinationWidth, destinationHeight);
            commandPool = new(graphicsQueue, true);

            //create multiples of these, 1 for each concurrent frame
            commandBuffers.Length = MaxFramesInFlight;
            inFlightFences.Length = MaxFramesInFlight;
            imageAvailableSemaphores.Length = MaxFramesInFlight;
            renderFinishedSemaphores.Length = MaxFramesInFlight;
            commandPool.CreateCommandBuffers(commandBuffers.AsSpan());

            for (uint i = 0; i < MaxFramesInFlight; i++)
            {
                inFlightFences[i] = new(logicalDevice);
                imageAvailableSemaphores[i] = new(logicalDevice);
                renderFinishedSemaphores[i] = new(logicalDevice);
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
            swapChainFramebuffers = new(imageCount);
            for (uint i = 0; i < imageCount; i++)
            {
                ImageView imageView = new(images[i]);
                Framebuffer framebuffer = new(renderPass, [imageView, depthImage.imageView], width, height);
                surfaceImageViews[i] = imageView;
                swapChainFramebuffers[i] = framebuffer;
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
            uint vertexCount = meshEntity.GetVertexCount();
            USpan<ShaderVertexInputAttribute> shaderVertexAttributes = world.GetArray<ShaderVertexInputAttribute>(shader);
            USpan<Mesh.Channel> channels = stackalloc Mesh.Channel[(int)shaderVertexAttributes.Length];
            for (uint i = 0; i < shaderVertexAttributes.Length; i++)
            {
                ref ShaderVertexInputAttribute vertexAttribute = ref shaderVertexAttributes[i];
                if (TryDeduceMeshChannel(vertexAttribute, out Mesh.Channel channel))
                {
                    if (!meshEntity.ContainsChannel(channel))
                    {
                        if (channel == Mesh.Channel.Color)
                        {
                            //safe to assume (1, 1, 1, 1) is default for colors if needed and its missing
                            USpan<Vector4> defaultColors = meshEntity.CreateColors(vertexCount);
                            for (uint v = 0; v < vertexCount; v++)
                            {
                                defaultColors[v] = Color.White;
                            }
                        }
                        else if (channel == Mesh.Channel.Normal)
                        {
                            USpan<Vector3> defaultNormals = meshEntity.CreateNormals(vertexCount);
                            for (uint v = 0; v < vertexCount; v++)
                            {
                                defaultNormals[v] = Vector3.Zero;
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

            using List<float> vertexData = new();
            meshEntity.Assemble(vertexData, channels);
            uint indexCount = meshEntity.GetIndexCount();
            VertexBuffer vertexBuffer = new(graphicsQueue, commandPool, vertexData.AsSpan());
            IndexBuffer indexBuffer = new(graphicsQueue, commandPool, meshEntity.GetIndices());
            Trace.WriteLine($"Compiled mesh `{mesh}` with `{vertexCount}` vertices and `{indexCount}` indices");
            return new(meshEntity.GetVersion(), indexCount, vertexBuffer, indexBuffer, shaderVertexAttributes);
        }

        private readonly CompiledPipeline CompilePipeline(uint materialEntity, uint shaderEntity, World world, CompiledShader compiledShader, CompiledMesh compiledMesh)
        {
            Material material = new(world, materialEntity);
            USpan<ShaderVertexInputAttribute> shaderVertexAttributes = compiledMesh.VertexAttributes;
            USpan<VertexInputAttribute> vertexAttributes = stackalloc VertexInputAttribute[(int)shaderVertexAttributes.Length];
            for (uint i = 0; i < shaderVertexAttributes.Length; i++)
            {
                ref ShaderVertexInputAttribute shaderVertexAttribute = ref shaderVertexAttributes[i];
                vertexAttributes[i] = new(shaderVertexAttribute);
            }

            USpan<MaterialPushBinding> pushBindings = material.PushBindings;
            USpan<MaterialComponentBinding> uniformBindings = material.ComponentBindings;
            USpan<MaterialTextureBinding> textureBindings = material.TextureBindings;
            USpan<ShaderPushConstant> pushConstants = world.GetArray<ShaderPushConstant>(shaderEntity);
            USpan<ShaderUniformProperty> uniformProperties = world.GetArray<ShaderUniformProperty>(shaderEntity);
            USpan<ShaderSamplerProperty> samplerProperties = world.GetArray<ShaderSamplerProperty>(shaderEntity);

            //collect information to build the set layout
            uint totalCount = uniformBindings.Length + textureBindings.Length;
            USpan<(byte, VkDescriptorType, VkShaderStageFlags)> setLayoutBindings = stackalloc (byte, VkDescriptorType, VkShaderStageFlags)[(int)totalCount];
            uint bindingCount = 0;

            USpan<PipelineLayout.PushConstant> pushConstantsBuffer = stackalloc PipelineLayout.PushConstant[4];
            uint pushConstantsCount = 0;

            //cant have more than 1 push constant of the same type, so batch them into 1 vertex push constant
            //todo: fault: ^^^ what if theres fragment push constants? or geometry push constants? this will break
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
                    if (uniformBinding.key == new DescriptorResourceKey(uniformProperty.binding, uniformProperty.set))
                    {
                        containsBinding = true;
                        VkDescriptorType descriptorType = VkDescriptorType.UniformBuffer;
                        setLayoutBindings[bindingCount++] = (uniformBinding.Binding, descriptorType, shaderStage);
                        break;
                    }
                }

                if (!containsBinding)
                {
                    throw new InvalidOperationException($"Material `{materialEntity}` is missing a `{typeof(MaterialComponentBinding).Name}` to bind a component to property at `{uniformProperty.label}`({uniformProperty.binding})");
                }
            }

            foreach (ShaderSamplerProperty samplerProperty in samplerProperties)
            {
                bool containsBinding = false;
                foreach (MaterialTextureBinding textureBinding in textureBindings)
                {
                    if (textureBinding.key == new DescriptorResourceKey(samplerProperty.binding, samplerProperty.set))
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
                    throw new InvalidOperationException($"Material `{materialEntity}` is missing a `{typeof(MaterialTextureBinding).Name}` to bind a texture to property at `{samplerProperty.name}`({samplerProperty.binding})");
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
            if (uniformProperties.Length > 0)
            {
                poolTypes[poolCount++] = (VkDescriptorType.UniformBuffer, uniformProperties.Length);
            }

            if (samplerProperties.Length > 0)
            {
                poolTypes[poolCount++] = (VkDescriptorType.CombinedImageSampler, samplerProperties.Length);
            }

            //remember which bindings are push constants
            if (!knownPushConstants.TryGetValue(materialEntity, out Array<CompiledPushConstant> pushConstantArray))
            {
                pushConstantArray = new();
                knownPushConstants.Add(materialEntity, pushConstantArray);
            }

            if (pushBindings.Length > 0)
            {
                USpan<CompiledPushConstant> buffer = stackalloc CompiledPushConstant[(int)pushBindings.Length];
                for (uint i = 0; i < pushBindings.Length; i++)
                {
                    ref MaterialPushBinding binding = ref pushBindings[i];
                    buffer[i] = new(binding.componentType, binding.stage);
                }

                pushConstantArray.Length = buffer.Length;
                pushConstantArray.CopyFrom(buffer);
            }

            //create buffers for bindings that arent push constants (referring to components on entities)
            VkPhysicalDeviceLimits limits = logicalDevice.physicalDevice.GetLimits();
            foreach (MaterialComponentBinding binding in uniformBindings)
            {
                uint componentEntity = binding.entity;
                ComponentType componentType = binding.componentType;
                if (!world.ContainsEntity(componentEntity))
                {
                    throw new InvalidOperationException($"Material `{materialEntity}` references missing entity `{componentEntity}` for component `{componentType}`");
                }

                if (!world.ContainsComponent(componentEntity, componentType))
                {
                    throw new InvalidOperationException($"Material `{materialEntity}` references entity `{componentEntity}` for a missing component `{componentType}`");
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

            return new(pipeline, pipelineLayout, poolTypes.Slice(0, poolCount), setLayout, setLayoutBindings.Slice(0, bindingCount));
        }

        private readonly CompiledImage CompileImage(uint materialEntity, uint textureVersion, MaterialTextureBinding binding)
        {
            World world = destination.GetWorld();
            uint depth = 1;
            VkImageUsageFlags usage = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled;
            //VkFormat format = VkFormat.R8G8B8A8Srgb;
            VkFormat format = VkFormat.R8G8B8A8Unorm;
            uint textureEntity = binding.TextureEntity;
            IsTexture size = world.GetComponent<IsTexture>(textureEntity);
            Vector4 region = binding.Region;
            uint x = (uint)(region.X * size.width);
            uint y = (uint)(region.Y * size.height);
            uint z = (uint)(region.Z * size.width);
            uint w = (uint)(region.W * size.height);
            uint minX = Math.Min(x, z);
            uint minY = Math.Min(y, w);
            uint maxX = Math.Max(x, z);
            uint maxY = Math.Max(y, w);
            uint width = maxX - minX;
            uint height = maxY - minY;
            Image image = new(logicalDevice, width, height, depth, format, usage);
            DeviceMemory imageMemory = new(image, VkMemoryPropertyFlags.DeviceLocal);
            USpan<Pixel> pixels = world.GetArray<Pixel>(textureEntity);

            //copy pixels from the entity, into the temporary buffer, then temporary buffer copies into the buffer
            using BufferDeviceMemory tempStagingBuffer = new(logicalDevice, pixels.Length * 4, VkBufferUsageFlags.TransferSrc, VkMemoryPropertyFlags.HostCoherent | VkMemoryPropertyFlags.HostVisible);
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
            tempBuffer.CopyBufferToImage(tempStagingBuffer.buffer, size.width, size.height, minX, minY, image, depth);
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
            samplerParameters.minFilter = binding.Filter == TextureFiltering.Linear ? VkFilter.Linear : VkFilter.Nearest;
            samplerParameters.magFilter = samplerParameters.minFilter;
            Sampler sampler = new(logicalDevice, samplerParameters);
            Trace.WriteLine($"Compiled image for material `{materialEntity}` with `{width}`x`{height}` pixels");
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
                ComponentType componentType = componentBuffer.componentType;
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

        public bool BeginRender(Vector4 clearColor)
        {
            World world = destination.GetWorld();
            ref Fence submitFence = ref inFlightFences[currentFrame];
            ref CommandBuffer commandBuffer = ref commandBuffers[currentFrame];

            submitFence.Wait();

            VkResult result = logicalDevice.TryAcquireNextImage(swapchain, imageAvailableSemaphores[currentFrame], default, out imageIndex);
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

            ref Framebuffer framebuffer = ref swapChainFramebuffers[imageIndex];
            Vector4 area = new(0, 0, framebuffer.width, framebuffer.height);
            commandBuffer.BeginRenderPass(renderPass, framebuffer, area, clearColor);

            Vector4 viewport = new(0, framebuffer.height, framebuffer.width, -framebuffer.height);
            commandBuffer.SetViewport(viewport);

            Vector4 scissor = new(0, 0, framebuffer.width, framebuffer.height);
            commandBuffer.SetScissor(scissor);

            UpdateComponentBuffers(world);
            UpdateTextureBuffers(world);
            ReadScissorValues(world, area);
            return true;
        }

        private readonly void ReadScissorValues(World world, Vector4 area)
        {
            uint capacity = Allocations.GetNextPowerOf2(world.MaxEntityValue + 1);
            if (scissors.Length < capacity)
            {
                scissors.Length = capacity;
            }

            scissors.Fill(area);

            using List<uint> stack = new(capacity);
            ComponentQuery<WorldRendererScissor> query = new(world);
            foreach (var r in query)
            {
                ref WorldRendererScissor scissor = ref r.component1;
                scissors[r.entity] = scissor.value;

                //propagate this scissor down to descendants
                stack.Add(r.entity);
                while (stack.Count > 0)
                {
                    uint entity = stack.RemoveAt(0);
                    USpan<uint> children = world.GetChildren(entity);
                    foreach (uint child in children)
                    {
                        scissors[child] = scissor.value;
                    }

                    stack.AddRange(children);
                }
            }

            foreach (var r in query)
            {
                ref WorldRendererScissor scissor = ref r.component1;
                scissors[r.entity] = scissor.value;
            }
        }

        public readonly void Render(USpan<uint> renderEntities, uint materialEntity, uint shaderEntity, uint meshEntity)
        {
            World world = destination.GetWorld();
            IsShader shaderComponent = world.GetComponent<IsShader>(shaderEntity);
            bool deviceWaited = false;

            void Wait(LogicalDevice logicalDevice)
            {
                if (!deviceWaited)
                {
                    deviceWaited = true;
                    logicalDevice.Wait();
                }
            }

            //make sure a shader exists for this shader entity, also rebuild it when version changes
            if (!shaders.TryGetValue(shaderEntity, out CompiledShader compiledShader))
            {
                compiledShader = CompileShader(world, shaderEntity);
                shaders.Add(shaderEntity, compiledShader);
            }

            bool shaderChanged = compiledShader.version != shaderComponent.version;
            if (shaderChanged)
            {
                Wait(logicalDevice);
                compiledShader.Dispose();
                compiledShader = CompileShader(world, shaderEntity);
                shaders[shaderEntity] = compiledShader;
            }

            //make sure a processed mesh exists for this combination of shader entity and mesh entity, also rebuild it when it changes
            RendererKey key = new(materialEntity, meshEntity);
            uint meshVersion = world.GetComponent<IsMesh>(meshEntity).version;
            ref CompiledMesh compiledMesh = ref meshes.TryGetValue(key, out bool containsMesh);
            if (!containsMesh)
            {
                CompiledMesh newCompiledMesh = CompileMesh(world, shaderEntity, meshEntity);
                compiledMesh = ref meshes.Add(key, newCompiledMesh);
            }

            bool meshChanged = compiledMesh.version != meshVersion;
            if (meshChanged || shaderChanged)
            {
                Wait(logicalDevice);
                compiledMesh.Dispose();
                compiledMesh = CompileMesh(world, shaderEntity, meshEntity);
            }

            //make sure a pipeline exists, the same way a compiled mesh is
            ref CompiledPipeline compiledPipeline = ref pipelines.TryGetValue(key, out bool containsPipeline);
            if (!containsPipeline)
            {
                Trace.WriteLine($"Creating pipeline for material `{materialEntity}` with shader `{shaderEntity}` and mesh `{meshEntity}` for the first time");
                CompiledPipeline newCompiledPipeline = CompilePipeline(materialEntity, shaderEntity, world, compiledShader, compiledMesh);
                compiledPipeline = ref pipelines.Add(key, newCompiledPipeline);
            }

            //update images of bindings that change
            bool updateDescriptorSet = false;
            USpan<MaterialTextureBinding> textureBindings = world.GetArray<MaterialTextureBinding>(materialEntity);
            for (uint i = 0; i < textureBindings.Length; i++)
            {
                ref MaterialTextureBinding textureBinding = ref textureBindings[i];
                int textureHash = GetTextureHash(materialEntity, textureBinding);
                if (images.ContainsKey(textureHash))
                {
                    ref CompiledImage image = ref images[textureHash];
                    if (image.binding.Version != textureBinding.Version)
                    {
                        Wait(logicalDevice);
                        image.Dispose();
                        uint textureVersion = world.GetComponent<IsTexture>(textureBinding.TextureEntity).version;
                        image = CompileImage(materialEntity, textureVersion, textureBinding);
                        updateDescriptorSet = true;
                    }
                }
            }

            if (meshChanged || shaderChanged || updateDescriptorSet)
            {
                Wait(logicalDevice);

                //todo: handle possible cases where a pipeline rebuild isnt needed, for example: mesh only and within alloc size
                //need to dispose the descriptor sets before the descriptor pool is gone
                foreach (uint entity in renderEntities)
                {
                    if (renderers.TryRemove(entity, out CompiledRenderer renderer))
                    {
                        renderer.Dispose();
                    }
                }

                Trace.WriteLine($"Rebuilding pipeline for material `{materialEntity}` with shader `{shaderEntity}` and mesh `{meshEntity}`");
                compiledPipeline.Dispose();
                compiledPipeline = CompilePipeline(materialEntity, shaderEntity, world, compiledShader, compiledMesh);
            }

            //update descriptor sets if needed
            foreach (uint entity in renderEntities)
            {
                if (!renderers.ContainsKey(entity))
                {
                    DescriptorSet descriptorSet = compiledPipeline.Allocate();
                    CompiledRenderer renderer = new(descriptorSet);
                    renderers.Add(entity, renderer);
                    UpdateDescriptorSet(materialEntity, renderer.descriptorSet, compiledPipeline);
                }
            }

            //finally draw everything
            ref CommandBuffer commandBuffer = ref commandBuffers[currentFrame];
            commandBuffer.BindPipeline(compiledPipeline.pipeline, VkPipelineBindPoint.Graphics);
            commandBuffer.BindVertexBuffer(compiledMesh.vertexBuffer);
            commandBuffer.BindIndexBuffer(compiledMesh.indexBuffer);

            bool hasPushConstants = knownPushConstants.TryGetValue(materialEntity, out Array<CompiledPushConstant> pushConstants);
            foreach (uint rendererEntity in renderEntities)
            {
                //apply scissor
                ref Vector4 scissor = ref scissors[rendererEntity];
                commandBuffer.SetScissor(scissor);

                //push constants
                if (hasPushConstants)
                {
                    uint pushOffset = 0;
                    foreach (CompiledPushConstant pushConstant in pushConstants)
                    {
                        USpan<byte> componentBytes = world.GetComponentBytes(rendererEntity, pushConstant.componentType);
                        commandBuffer.PushConstants(compiledPipeline.pipelineLayout, GetShaderStage(pushConstant.stage), componentBytes, pushOffset);
                        pushOffset += componentBytes.Length;
                    }
                }

                ref CompiledRenderer renderer = ref renderers[rendererEntity];
                commandBuffer.BindDescriptorSet(compiledPipeline.pipelineLayout, renderer.descriptorSet);
                commandBuffer.DrawIndexed(compiledMesh.indexCount, 1, 0, 0, 0);

                previouslyRenderedEntities.TryAdd(rendererEntity);
            }

            previouslyRenderedGroups.TryAdd((materialEntity, shaderEntity, meshEntity));
        }

        public void EndRender()
        {
            ref Semaphore signalSemaphore = ref renderFinishedSemaphores[currentFrame];
            ref Semaphore waitSemaphore = ref imageAvailableSemaphores[currentFrame];
            ref CommandBuffer commandBuffer = ref commandBuffers[currentFrame];

            commandBuffer.EndRenderPass();
            commandBuffer.End();

            graphicsQueue.Submit(commandBuffer, waitSemaphore, VkPipelineStageFlags.ColorAttachmentOutput, signalSemaphore, inFlightFences[currentFrame]);

            VkResult result = presentationQueue.TryPresent(signalSemaphore, swapchain, imageIndex);
            if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR || IsDestinationResized())
            {
                RebuildSwapchain();
            }
            else if (result != VkResult.Success)
            {
                throw new InvalidOperationException($"Failed to present image: {result}");
            }

            currentFrame = (currentFrame + 1) % MaxFramesInFlight;

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
                ref CompiledComponentBuffer component = ref components[componentHash];
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
                ref CompiledImage image = ref images[textureHash];
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
            foreach (RendererKey key in meshes.Keys)
            {
                bool used = false;
                foreach ((uint materialEntity, uint shaderEntity, uint meshEntity) in previouslyRenderedGroups)
                {
                    if (new RendererKey(materialEntity, meshEntity) == key)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    logicalDevice.Wait();
                    CompiledMesh mesh = meshes.Remove(key);
                    mesh.Dispose();
                }
            }

            //dispose unused pipelines
            foreach (RendererKey key in pipelines.Keys)
            {
                bool used = false;
                foreach ((uint materialEntity, uint shaderEntity, uint meshEntity) in previouslyRenderedGroups)
                {
                    if (new RendererKey(materialEntity, meshEntity) == key)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    logicalDevice.Wait();
                    CompiledPipeline pipeline = pipelines.Remove(key);
                    pipeline.Dispose();
                }
            }

            previouslyRenderedGroups.Clear();
            previouslyRenderedEntities.Clear();
        }

        private readonly void UpdateDescriptorSet(uint materialEntity, DescriptorSet descriptorSet, CompiledPipeline pipeline)
        {
            World world = destination.GetWorld();
            Material material = new(world, materialEntity);
            byte set = 0;
            foreach ((byte binding, VkDescriptorType type, _) in pipeline.Bindings)
            {
                if (type == VkDescriptorType.CombinedImageSampler)
                {
                    MaterialTextureBinding textureBinding = material.GetTextureBindingRef(binding, set);
                    int textureHash = GetTextureHash(materialEntity, textureBinding);
                    ref CompiledImage image = ref images[textureHash];
                    descriptorSet.Update(image.imageView, image.sampler, binding);
                }
                else if (type == VkDescriptorType.UniformBuffer)
                {
                    MaterialComponentBinding componentBinding = material.GetComponentBindingRef(binding, set);
                    int componentHash = GetComponentHash(materialEntity, componentBinding);
                    ref CompiledComponentBuffer componentBuffer = ref components[componentHash];
                    descriptorSet.Update(componentBuffer.buffer.buffer, binding);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported descriptor type `{type}`");
                }
            }
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
            for (uint i = 0; i < physicalDevices.Length; i++)
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
                if (availableExtensions.Length > 0)
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
                else if (requiredExtensions.Length > 0)
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
            USpan<char> nameBuffer = stackalloc char[(int)FixedString.Capacity]; //<- only int allowed so cringe
            uint length = attribute.name.CopyTo(nameBuffer);
            for (uint i = 0; i < length; i++)
            {
                nameBuffer[i] = char.ToLower(nameBuffer[i]);
            }

            if (attribute.Type == typeof(Vector2))
            {
                if (nameBuffer.Slice(0, length).Contains("uv".AsUSpan()))
                {
                    channel = Mesh.Channel.UV;
                    return true;
                }
            }
            else if (attribute.Type == typeof(Vector3))
            {
                if (nameBuffer.Slice(0, length).Contains("normal".AsUSpan()))
                {
                    channel = Mesh.Channel.Normal;
                    return true;
                }
                else if (nameBuffer.Slice(0, length).Contains("tangent".AsUSpan()))
                {
                    channel = Mesh.Channel.Tangent;
                    return true;
                }
                else if (nameBuffer.Slice(0, length).Contains("position".AsUSpan()))
                {
                    channel = Mesh.Channel.Position;
                    return true;
                }
                else if (nameBuffer.Slice(0, length).Contains("bitangent".AsUSpan()))
                {
                    channel = Mesh.Channel.BiTangent;
                    return true;
                }
            }
            else if (attribute.Type == typeof(Vector4))
            {
                if (nameBuffer.Slice(0, length).Contains("color".AsUSpan()))
                {
                    channel = Mesh.Channel.Color;
                    return true;
                }
            }

            channel = default;
            return false;
        }

        private static VkShaderStageFlags GetShaderStage(RenderStage shaderStage)
        {
            return shaderStage switch
            {
                RenderStage.Vertex => VkShaderStageFlags.Vertex,
                RenderStage.Fragment => VkShaderStageFlags.Fragment,
                RenderStage.Geometry => VkShaderStageFlags.Geometry,
                RenderStage.Compute => VkShaderStageFlags.Compute,
                _ => throw new ArgumentOutOfRangeException(nameof(shaderStage), shaderStage, null),
            };
        }
    }
}