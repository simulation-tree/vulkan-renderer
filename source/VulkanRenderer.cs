using Collections;
using Collections.Generic;
using Materials;
using Materials.Components;
using Meshes;
using Rendering.Components;
using Shaders;
using Simulation;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Textures;
using Textures.Components;
using Unmanaged;
using Vortice.Vulkan;
using Vulkan;
using Worlds;

namespace Rendering.Vulkan
{
    [SkipLocalsInit]
    public struct VulkanRenderer : IDisposable
    {
        private const uint MaxFramesInFlight = 2;

        private readonly Destination destination;
        private readonly Instance instance;
        private readonly PhysicalDevice physicalDevice;
        private readonly Dictionary<(uint, uint), CompiledShader> shaders;
        private readonly Dictionary<uint, Array<CompiledPushConstant>> knownPushConstants;
        private readonly List<CompiledRenderer> renderers;
        private readonly Dictionary<RendererKey, CompiledPipeline> pipelines;
        private readonly List<RendererKey> pipelineKeys;
        private readonly Dictionary<RendererKey, CompiledMesh> meshes;
        private readonly List<RendererKey> meshKeys;
        private readonly Dictionary<uint, CompiledComponentBuffer> components;
        private readonly Dictionary<uint, CompiledImage> images;
        private readonly Array<CommandBuffer> commandBuffers;
        private readonly Array<Fence> inFlightFences;
        private readonly Array<Semaphore> imageAvailableSemaphores;
        private readonly Array<Semaphore> renderFinishedSemaphores;
        private readonly List<RendererCombination> previouslyRenderedGroups;
        private readonly List<uint> previouslyRenderedEntities;
        private readonly Array<Vector4> scissors;
        private readonly Stack<uint> stack;
        private readonly Array<IsTexture> textureComponents;

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

        public unsafe readonly Allocation Instance => new((void*)instance.Value.Handle);

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
            pipelineKeys = new();
            meshKeys = new();
            meshes = new();
            components = new();
            scissors = new();
            stack = new();
            textureComponents = new();
        }

        /// <summary>
        /// Cleans up everything that the vulkan renderer created.
        /// </summary>
        public readonly void Dispose()
        {
            textureComponents.Dispose();
            stack.Dispose();
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
            foreach (CompiledRenderer renderer in renderers)
            {
                if (renderer != default)
                {
                    renderer.Dispose();
                }
            }

            renderers.Dispose();
        }

        private readonly void DisposePushConstants()
        {
            foreach (Array<CompiledPushConstant> pushConstantArray in knownPushConstants.Values)
            {
                pushConstantArray.Dispose();
            }

            knownPushConstants.Dispose();
        }

        private readonly void DisposePipelines()
        {
            foreach (CompiledPipeline pipeline in pipelines.Values)
            {
                pipeline.Dispose();
            }

            pipelineKeys.Dispose();
            pipelines.Dispose();
        }

        private readonly void DisposeShaderModules()
        {
            foreach (CompiledShader shaderModule in shaders.Values)
            {
                shaderModule.Dispose();
            }

            shaders.Dispose();
        }

        private readonly void DisposeComponentBuffers()
        {
            foreach (CompiledComponentBuffer componentBuffer in components.Values)
            {
                componentBuffer.Dispose();
            }

            components.Dispose();
        }

        private readonly void DisposeTextureBuffers()
        {
            foreach (CompiledImage image in images.Values)
            {
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
            meshKeys.Dispose();
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

        public void SurfaceCreated(Allocation surface)
        {
            this.surface = new(instance, surface);
            (uint graphicsFamily, uint presentationFamily) = physicalDevice.GetQueueFamilies(this.surface);
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

        private readonly CompiledShader CompileShader(World world, VertexShaderData vertexShader, FragmentShaderData fragmentShader)
        {
            Shader vertex = vertexShader.Get(world);
            Shader fragment = fragmentShader.Get(world);
            ShaderModule vertexModule = new(logicalDevice, vertex.Bytes);
            ShaderModule fragmentModule = new(logicalDevice, fragment.Bytes);
            return new(vertexShader.version, fragmentShader.version, vertexModule, fragmentModule);
        }

        private readonly CompiledMesh CompileMesh(World world, uint meshEntity, uint vertexShaderEntity)
        {
            Mesh mesh = new Entity(world, meshEntity).As<Mesh>();
            uint vertexCount = mesh.VertexCount;
            Values<ShaderVertexInputAttribute> shaderVertexAttributes = world.GetArray<ShaderVertexInputAttribute>(vertexShaderEntity);
            USpan<MeshChannel> channels = stackalloc MeshChannel[(int)shaderVertexAttributes.Length];
            for (uint i = 0; i < shaderVertexAttributes.Length; i++)
            {
                ref ShaderVertexInputAttribute vertexAttribute = ref shaderVertexAttributes[i];
                if (TryDeduceMeshChannel(vertexAttribute, out MeshChannel channel))
                {
                    if (!mesh.ContainsChannel(channel))
                    {
                        if (channel == MeshChannel.Color)
                        {
                            //safe to assume (1, 1, 1, 1) is default for colors if needed and its missing
                            USpan<Vector4> defaultColors = mesh.CreateColors(vertexCount);
                            for (uint v = 0; v < vertexCount; v++)
                            {
                                defaultColors[v] = new(1, 1, 1, 1);
                            }
                        }
                        else if (channel == MeshChannel.Normal)
                        {
                            USpan<Vector3> defaultNormals = mesh.CreateNormals(vertexCount);
                            for (uint v = 0; v < vertexCount; v++)
                            {
                                defaultNormals[v] = Vector3.Zero;
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException($"Mesh entity `{meshEntity}` is missing required `{channel}` channel");
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Unable to deduce the mesh channel from property name `{vertexAttribute.name}`, name is too ambiguous");
                }

                channels[i] = channel;
            }

            uint vertexSize = channels.GetVertexSize();
            using Array<float> vertexData = new(vertexCount * vertexSize);
            mesh.Assemble(vertexData.AsSpan(), channels);
            USpan<uint> indices = mesh.Indices;
            uint indexCount = indices.Length;
            VertexBuffer vertexBuffer = new(graphicsQueue, commandPool, vertexData.AsSpan());
            IndexBuffer indexBuffer = new(graphicsQueue, commandPool, indices);
            //Trace.WriteLine($"Compiled mesh `{meshEntity}` with `{vertexCount}` vertices and `{indexCount}` indices");
            return new(mesh.Version, indexCount, vertexBuffer, indexBuffer, shaderVertexAttributes.AsSpan());
        }

        private readonly CompiledPipeline CompilePipeline(World world, uint materialEntity, uint vertexShaderEntity, uint fragmentShaderEntity, CompiledShader compiledShader, CompiledMesh compiledMesh)
        {
            Material material = new Entity(world, materialEntity).As<Material>();
            USpan<ShaderVertexInputAttribute> shaderVertexAttributes = compiledMesh.VertexAttributes;
            USpan<VkVertexInputAttributeDescription> vertexAttributes = stackalloc VkVertexInputAttributeDescription[(int)shaderVertexAttributes.Length];
            uint offset = 0;
            for (uint i = 0; i < shaderVertexAttributes.Length; i++)
            {
                ref ShaderVertexInputAttribute shaderVertexAttribute = ref shaderVertexAttributes[i];
                ref VkVertexInputAttributeDescription vulkanVertexAttribute = ref vertexAttributes[i];
                vulkanVertexAttribute.location = shaderVertexAttribute.location;
                vulkanVertexAttribute.format = GetFormat(shaderVertexAttribute.Type);
                vulkanVertexAttribute.binding = shaderVertexAttribute.binding;
                vulkanVertexAttribute.offset = offset;
                offset += shaderVertexAttribute.size;
            }

            USpan<PushBinding> pushBindings = material.PushBindings;
            USpan<ComponentBinding> uniformBindings = material.ComponentBindings;
            USpan<TextureBinding> textureBindings = material.TextureBindings;
            Values<ShaderPushConstant> pushConstants = world.GetArray<ShaderPushConstant>(vertexShaderEntity);
            Values<ShaderUniformProperty> uniformProperties = world.GetArray<ShaderUniformProperty>(vertexShaderEntity);
            Values<ShaderSamplerProperty> samplerProperties = world.GetArray<ShaderSamplerProperty>(fragmentShaderEntity);

            //collect information to build the set layout
            uint totalCount = uniformBindings.Length + textureBindings.Length;
            USpan<VkDescriptorSetLayoutBinding> setLayoutBindings = stackalloc VkDescriptorSetLayoutBinding[(int)totalCount];
            uint bindingCount = 0;

            USpan<PipelineLayout.PushConstant> pushConstantsBuffer = stackalloc PipelineLayout.PushConstant[4];
            uint pushConstantsCount = 0;

            //cant have more than 1 push constant of the same type, so batch them into 1 vertex push constant
            //todo: fault: ^^^ what if theres fragment push constants? or geometry push constants? this will break
            if (pushConstants.Length > 0)
            {
                Schema schema = world.Schema;
                uint start = 0;
                uint size = 0;
                for (uint c = 0; c < pushConstants.Length; c++)
                {
                    ShaderPushConstant pushConstant = pushConstants[c];
                    start = Math.Min(start, pushConstant.offset);
                    size += pushConstant.size;
                    bool containsPush = false;
                    for (uint p = 0; p < pushBindings.Length; p++)
                    {
                        PushBinding pushBinding = pushBindings[p];
                        ushort componentSize = pushBinding.componentType.size;
                        if (componentSize == pushConstant.size && pushBinding.start == pushConstant.offset)
                        {
                            containsPush = true;
                            break;
                        }
                    }

                    if (!containsPush)
                    {
                        throw new InvalidOperationException($"Material `{materialEntity}` is missing a `{typeof(PushBinding)}` to bind a push constant named `{pushConstant.memberName}`");
                    }
                }

                pushConstantsBuffer[pushConstantsCount++] = new(start, size, VkShaderStageFlags.Vertex);
            }

            foreach (ShaderUniformProperty uniformProperty in uniformProperties)
            {
                bool containsBinding = false;
                foreach (ComponentBinding uniformBinding in uniformBindings)
                {
                    VkShaderStageFlags shaderStage = GetShaderStage(uniformBinding.stage);
                    if (uniformBinding.key == new DescriptorResourceKey(uniformProperty.binding, uniformProperty.set))
                    {
                        containsBinding = true;
                        VkDescriptorSetLayoutBinding binding = default;
                        binding.descriptorType = VkDescriptorType.UniformBuffer;
                        binding.binding = uniformBinding.key.Binding;
                        binding.descriptorCount = 1;
                        binding.stageFlags = shaderStage;
                        setLayoutBindings[bindingCount++] = binding;
                        break;
                    }
                }

                if (!containsBinding)
                {
                    throw new InvalidOperationException($"Material `{materialEntity}` is missing a `{typeof(ComponentBinding).Name}` to bind a component to property at `{uniformProperty.label}`({uniformProperty.binding})");
                }
            }

            foreach (ShaderSamplerProperty samplerProperty in samplerProperties)
            {
                bool containsBinding = false;
                foreach (TextureBinding textureBinding in textureBindings)
                {
                    if (textureBinding.key == new DescriptorResourceKey(samplerProperty.binding, samplerProperty.set))
                    {
                        containsBinding = true;
                        VkDescriptorSetLayoutBinding binding = default;
                        binding.descriptorType = VkDescriptorType.CombinedImageSampler;
                        binding.binding = textureBinding.key.Binding;
                        binding.descriptorCount = 1;
                        binding.stageFlags = VkShaderStageFlags.Fragment;
                        setLayoutBindings[bindingCount++] = binding;
                        break;
                    }
                }

                if (!containsBinding)
                {
                    throw new InvalidOperationException($"Material `{materialEntity}` is missing a `{typeof(TextureBinding).Name}` to bind a texture to property at `{samplerProperty.name}`({samplerProperty.binding})");
                }
            }

            //create pipeline
            DescriptorSetLayout setLayout = new(logicalDevice, setLayoutBindings.GetSpan(bindingCount));
            PipelineCreateInput pipelineCreation = new(renderPass, compiledShader.vertexShader, compiledShader.fragmentShader);
            MaterialFlags flags = material.Flags;
            CompareOperation depthCompareOperation = material.DepthCompareOperation;

            pipelineCreation.depthWriteEnable = (flags & MaterialFlags.DepthWrite) != 0;
            pipelineCreation.depthTestEnable = (flags & MaterialFlags.DepthTest) != 0;
            pipelineCreation.depthCompareOperation = depthCompareOperation;

            USpan<VkVertexInputBindingDescription> vertexBindings = stackalloc VkVertexInputBindingDescription[1];
            vertexBindings[0] = new(offset, VkVertexInputRate.Vertex, 0);
            //vertexBindings[1] = new(instanceSize, VkVertexInputRate.Instance, 1);
            PipelineLayout pipelineLayout = new(logicalDevice, setLayout, pushConstantsBuffer.GetSpan(pushConstantsCount));

            //todo: find the exact entry point string from the shader
            Pipeline pipeline = new(pipelineCreation, pipelineLayout, vertexBindings, vertexAttributes, "main");

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
                    ref PushBinding binding = ref pushBindings[i];
                    buffer[i] = new(binding.componentType, binding.stage, GetShaderStage(binding.stage));
                }

                pushConstantArray.Length = buffer.Length;
                pushConstantArray.CopyFrom(buffer);
            }

            //create buffers for bindings that arent push constants (referring to components on entities)
            VkPhysicalDeviceLimits limits = logicalDevice.physicalDevice.GetLimits();
            foreach (ComponentBinding binding in uniformBindings)
            {
                uint componentEntity = binding.entity;
                DataType dataType = binding.componentType;
                if (!world.ContainsEntity(componentEntity))
                {
                    throw new InvalidOperationException($"Material `{materialEntity}` references missing entity `{componentEntity}` for component `{dataType.ToString(world.Schema)}`");
                }

                if (!world.ContainsComponent(componentEntity, dataType.ComponentType))
                {
                    throw new InvalidOperationException($"Material `{materialEntity}` references entity `{componentEntity}` for a missing component `{dataType.ToString(world.Schema)}`");
                }

                uint componentHash = GetComponentHash(materialEntity, binding);
                if (!components.TryGetValue(componentHash, out CompiledComponentBuffer componentBuffer))
                {
                    ushort componentSize = dataType.size;
                    uint bufferSize = (uint)(Math.Ceiling(componentSize / (float)limits.minUniformBufferOffsetAlignment) * limits.minUniformBufferOffsetAlignment);
                    VkBufferUsageFlags usage = VkBufferUsageFlags.UniformBuffer;
                    VkMemoryPropertyFlags propertyFlags = VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent;
                    BufferDeviceMemory buffer = new(logicalDevice, bufferSize, usage, propertyFlags);
                    componentBuffer = new(materialEntity, binding.entity, dataType, buffer);
                    components.Add(componentHash, componentBuffer);
                }
            }

            //create buffers for texture bindings
            foreach (TextureBinding binding in textureBindings)
            {
                uint textureEntity = binding.Entity;
                if (!world.ContainsEntity(textureEntity))
                {
                    throw new InvalidOperationException($"Material `{materialEntity}` references texture entity `{textureEntity}`, which does not exist");
                }

                IsTexture textureComponent = textureComponents[textureEntity];
                if (textureComponent == default)
                {
                    throw new InvalidOperationException($"Material `{materialEntity}` references entity `{textureEntity}` that doesn't qualify as a texture");
                }

                uint textureHash = GetTextureHash(materialEntity, binding);
                if (!images.TryGetValue(textureHash, out CompiledImage compiledImage))
                {
                    IsTexture component = textureComponents[textureEntity];
                    compiledImage = CompileImage(materialEntity, binding, component);
                    images.Add(textureHash, compiledImage);
                }
            }

            return new(pipeline, pipelineLayout, poolTypes.GetSpan(poolCount), setLayout, setLayoutBindings.GetSpan(bindingCount));
        }

        private readonly CompiledImage CompileImage(uint materialEntity, TextureBinding binding, IsTexture component)
        {
            World world = destination.world;
            uint depth = 1;
            VkImageUsageFlags usage = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled;
            //VkFormat format = VkFormat.R8G8B8A8Srgb; //todo: why is this commented out again? i forget
            VkFormat format = VkFormat.R8G8B8A8Unorm;
            uint textureEntity = binding.Entity;
            bool isCubemap = world.ContainsTag<IsCubemapTexture>(textureEntity);
            Vector4 region = binding.Region;
            uint x = (uint)(region.X * component.width);
            uint y = (uint)(region.Y * component.height);
            uint z = (uint)(region.Z * component.width);
            uint w = (uint)(region.W * component.height);
            uint minX = Math.Min(x, z);
            uint minY = Math.Min(y, w);
            uint maxX = Math.Max(x, z);
            uint maxY = Math.Max(y, w);
            uint width = maxX - minX;
            uint height = maxY - minY;
            Image image = new(logicalDevice, width, height, depth, format, usage, isCubemap);
            DeviceMemory imageMemory = new(image, VkMemoryPropertyFlags.DeviceLocal);
            Values<Pixel> pixels = world.GetArray<Pixel>(textureEntity);
            uint layerCount = isCubemap ? 6u : 1u;

            //copy pixels from the entity, into the temporary buffer, then temporary buffer copies into the buffer... yada yada yada
            using BufferDeviceMemory tempStagingBuffer = new(logicalDevice, pixels.Length * 4, VkBufferUsageFlags.TransferSrc, VkMemoryPropertyFlags.HostCoherent | VkMemoryPropertyFlags.HostVisible);
            tempStagingBuffer.CopyFrom(pixels.AsSpan());
            VkImageLayout imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
            using CommandPool tempPool = new(graphicsQueue, true);
            using CommandBuffer tempBuffer = tempPool.CreateCommandBuffer();
            tempBuffer.Begin();
            tempBuffer.TransitionImageLayout(image, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal, VkImageAspectFlags.Color, layerCount);
            tempBuffer.End();
            graphicsQueue.Submit(tempBuffer);
            graphicsQueue.Wait();
            tempBuffer.Begin();
            tempBuffer.CopyBufferToImage(tempStagingBuffer.buffer, component.width, component.height, minX, minY, image, depth, layerCount);
            tempBuffer.End();
            graphicsQueue.Submit(tempBuffer);
            graphicsQueue.Wait();
            tempBuffer.Begin();
            tempBuffer.TransitionImageLayout(image, VkImageLayout.TransferDstOptimal, imageLayout, VkImageAspectFlags.Color, layerCount);
            tempBuffer.End();
            graphicsQueue.Submit(tempBuffer);
            graphicsQueue.Wait();

            ImageView imageView = new(image, VkImageAspectFlags.Color, isCubemap);
            SamplerCreateParameters samplerParameters = new();
            samplerParameters.minFilter = binding.Filter == TextureFiltering.Linear ? VkFilter.Linear : VkFilter.Nearest;
            samplerParameters.magFilter = samplerParameters.minFilter;
            Sampler sampler = new(logicalDevice, samplerParameters);
            Trace.WriteLine($"Compiled image for material `{materialEntity}` with `{width}`x`{height}` pixels (cubemap: {isCubemap})");
            return new(materialEntity, component.version, binding, image, imageView, imageMemory, sampler);
        }

        /// <summary>
        /// Copies data from components into the uniform buffers for material bindings.
        /// </summary>
        private readonly void UpdateComponentBuffers(World world)
        {
            foreach (CompiledComponentBuffer componentBuffer in components.Values)
            {
                uint entity = componentBuffer.containerEntity;
                DataType dataType = componentBuffer.componentType;
                ComponentType componentType = dataType.ComponentType;
                if (!world.ContainsEntity(entity))
                {
                    throw new InvalidOperationException($"Entity `{entity}` that contained component `{componentType.ToString(world.Schema)}` with data for a uniform buffer has been lost");
                }

                if (!world.ContainsComponent(entity, componentType))
                {
                    throw new InvalidOperationException($"Component `{componentType.ToString(world.Schema)}` on entity `{entity}` that used to contained data for a uniform buffer has been lost");
                }

                Allocation component = world.GetComponent(entity, componentType, out ushort componentSize);
                componentBuffer.buffer.CopyFrom(component, componentSize);
            }
        }

        /// <summary>
        /// Rebuilds textures for still used materials when their source updates.
        /// </summary>
        private readonly void UpdateTextureBuffers(World world)
        {
            foreach ((uint textureHash, CompiledImage image) in images)
            {
                Material material = new Entity(world, image.materialEntity).As<Material>();
                if (material.TryGetFirstTextureBinding(image.binding.Entity, out TextureBinding binding))
                {
                    IsTexture component = textureComponents[binding.Entity];
                    if (image.textureVersion != component.version)
                    {
                        //todo: untested: (triggered when the texture's pixel array changes)
                        logicalDevice.Wait();
                        image.Dispose();
                        images[textureHash] = CompileImage(image.materialEntity, binding, component);
                    }
                }
            }
        }

        public StatusCode BeginRender(Vector4 clearColor)
        {
            World world = destination.world;
            ref Fence submitFence = ref inFlightFences[currentFrame];
            ref CommandBuffer commandBuffer = ref commandBuffers[currentFrame];

            submitFence.Wait();

            VkResult result = logicalDevice.TryAcquireNextImage(swapchain, imageAvailableSemaphores[currentFrame], default, out imageIndex);
            if (result == VkResult.ErrorOutOfDateKHR)
            {
                RebuildSwapchain();
                return StatusCode.Success(0);
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

            ComponentType textureType = world.Schema.GetComponentType<IsTexture>();
            CollectComponents(world, textureType);
            UpdateComponentBuffers(world);
            UpdateTextureBuffers(world);
            ReadScissorValues(world, area);
            return StatusCode.Continue;
        }

        private readonly void CollectComponents(World world, ComponentType textureType)
        {
            uint capacity = Allocations.GetNextPowerOf2(world.MaxEntityValue + 1);
            if (textureComponents.Length < capacity)
            {
                textureComponents.Length = capacity;
            }

            textureComponents.Clear();
            foreach (Chunk chunk in world.Chunks)
            {
                if (chunk.Definition.ContainsComponent(textureType))
                {
                    USpan<IsTexture> components = chunk.GetComponents<IsTexture>(textureType);
                    USpan<uint> entities = chunk.Entities;
                    for (uint i = 0; i < entities.Length; i++)
                    {
                        textureComponents[entities[i]] = components[i];
                    }
                }
            }
        }

        private readonly void ReadScissorValues(World world, Vector4 area)
        {
            uint capacity = Allocations.GetNextPowerOf2(world.MaxEntityValue + 1);
            if (scissors.Length < capacity)
            {
                scissors.Length = capacity;
            }

            scissors.Fill(area);
            stack.Clear(capacity);

            ComponentType worldScissorType = world.Schema.GetComponentType<WorldRendererScissor>();
            USpan<Chunk> chunks = stackalloc Chunk[64]; //todo: fault: this can possibly fail if there are more than 64 chunks that fit the requirement
            uint chunkCount = 0;
            foreach (Chunk chunk in world.Chunks)
            {
                if (chunk.Definition.ContainsComponent(worldScissorType))
                {
                    chunks[chunkCount++] = chunk;
                }
            }

            //propagate scissors down to descendants
            for (uint c = 0; c < chunkCount; c++)
            {
                Chunk chunk = chunks[c];
                USpan<uint> entities = chunk.Entities;
                USpan<WorldRendererScissor> components = chunk.GetComponents<WorldRendererScissor>(worldScissorType);
                for (uint i = 0; i < entities.Length; i++)
                {
                    ref WorldRendererScissor scissor = ref components[i];
                    uint entity = entities[i];
                    scissors[entity] = scissor.value;

                    stack.Push(entity);
                    while (stack.Count > 0)
                    {
                        uint current = stack.Pop();
                        USpan<uint> children = world.GetChildren(current);
                        foreach (uint child in children)
                        {
                            scissors[child] = scissor.value;
                        }

                        stack.PushRange(children);
                    }
                }
            }

            //hard assign the roots back to the original scissor
            for (uint c = 0; c < chunkCount; c++)
            {
                Chunk chunk = chunks[c];
                USpan<uint> entities = chunk.Entities;
                USpan<WorldRendererScissor> components = chunk.GetComponents<WorldRendererScissor>(worldScissorType);
                for (uint i = 0; i < entities.Length; i++)
                {
                    ref WorldRendererScissor scissor = ref components[i];
                    uint entity = entities[i];
                    scissors[entity] = scissor.value;
                }
            }
        }

        public readonly void Render(USpan<uint> renderEntities, MaterialData material, MeshData mesh, VertexShaderData vertexShader, FragmentShaderData fragmentShader)
        {
            World world = destination.world;
            ArrayElementType textureBindingType = world.Schema.GetArrayType<TextureBinding>();
            uint materialEntity = material.entity;
            uint meshEntity = mesh.entity;
            uint vertexShaderEntity = vertexShader.entity;
            uint fragmentShaderEntity = fragmentShader.entity;
            bool deviceWaited = false;

            void TryWait(LogicalDevice logicalDevice)
            {
                if (!deviceWaited)
                {
                    deviceWaited = true;
                    logicalDevice.Wait();
                }
            }

            //make sure a shader exists for this shader entity, also rebuild it when version changes
            bool shaderChanged = false;
            if (!shaders.TryGetValue((vertexShaderEntity, fragmentShaderEntity), out CompiledShader compiledShader))
            {
                compiledShader = CompileShader(world, vertexShader, fragmentShader);
                shaders.Add((vertexShaderEntity, fragmentShaderEntity), compiledShader);
            }
            else
            {
                shaderChanged = compiledShader.vertexVersion != vertexShader.version;
                if (shaderChanged)
                {
                    TryWait(logicalDevice);
                    compiledShader.Dispose();
                    compiledShader = CompileShader(world, vertexShader, fragmentShader);
                    shaders[(vertexShaderEntity, fragmentShaderEntity)] = compiledShader;
                }
            }

            //make sure a processed mesh exists for this combination of shader entity and mesh entity, also rebuild it when it changes
            bool meshChanged = false;
            RendererKey key = new(materialEntity, meshEntity);
            ref CompiledMesh compiledMesh = ref meshes.TryGetValue(key, out bool containsMesh);
            if (!containsMesh)
            {
                compiledMesh = ref meshes.Add(key);
                compiledMesh = CompileMesh(world, meshEntity, vertexShaderEntity);
                meshKeys.Add(key);
            }
            else
            {
                meshChanged = compiledMesh.version != mesh.version;
                if (meshChanged || shaderChanged)
                {
                    TryWait(logicalDevice);
                    compiledMesh.Dispose();
                    compiledMesh = CompileMesh(world, meshEntity, vertexShaderEntity);
                }
            }

            //make sure a pipeline exists, the same way a compiled mesh is
            ref CompiledPipeline compiledPipeline = ref pipelines.TryGetValue(key, out bool containsPipeline);
            if (!containsPipeline)
            {
                Trace.WriteLine($"Creating pipeline for material `{materialEntity}` and mesh `{meshEntity}` for the first time");
                compiledPipeline = ref pipelines.Add(key);
                compiledPipeline = CompilePipeline(world, materialEntity, vertexShaderEntity, fragmentShaderEntity, compiledShader, compiledMesh);
                pipelineKeys.Add(key);
            }

            //update images of bindings that change
            bool updateDescriptorSet = false;
            Values<TextureBinding> textureBindings = world.GetArray<TextureBinding>(materialEntity, textureBindingType);
            for (uint i = 0; i < textureBindings.Length; i++)
            {
                ref TextureBinding textureBinding = ref textureBindings[i];
                uint textureHash = GetTextureHash(materialEntity, textureBinding);
                if (images.ContainsKey(textureHash))
                {
                    ref CompiledImage image = ref images[textureHash];
                    if (image.binding.Version != textureBinding.Version || image.binding.Region != textureBinding.Region)
                    {
                        TryWait(logicalDevice);
                        image.Dispose();

                        IsTexture component = textureComponents[textureBinding.Entity];
                        image = CompileImage(materialEntity, textureBinding, component);
                        updateDescriptorSet = true;
                    }
                }
            }

            if (shaderChanged || updateDescriptorSet)
            {
                TryWait(logicalDevice);

                //todo: handle possible cases where a pipeline rebuild isnt needed, for example: mesh only and within alloc size
                //need to dispose the descriptor sets before the descriptor pool is gone
                for (uint i = 0; i < renderEntities.Length; i++)
                {
                    uint entity = renderEntities[i];
                    if (renderers.Count > entity)
                    {
                        ref CompiledRenderer renderer = ref renderers[entity];
                        if (renderer != default)
                        {
                            renderer.Dispose();
                            renderer = default;
                        }
                    }
                }

                Trace.WriteLine($"Rebuilding pipeline for material `{materialEntity}` with and mesh `{meshEntity}`");
                compiledPipeline.Dispose();
                compiledPipeline = CompilePipeline(world, materialEntity, vertexShaderEntity, fragmentShaderEntity, compiledShader, compiledMesh);
            }

            //update descriptor sets if needed
            uint maxEntityPosition = 0;
            for (uint i = 0; i < renderEntities.Length; i++)
            {
                uint entity = renderEntities[i];
                if (entity > maxEntityPosition)
                {
                    maxEntityPosition = entity;
                }
            }

            if (maxEntityPosition >= renderers.Count)
            {
                uint toAdd = maxEntityPosition - renderers.Count + 1;
                renderers.AddDefault(toAdd);
            }

            for (uint i = 0; i < renderEntities.Length; i++)
            {
                uint entity = renderEntities[i];
                ref CompiledRenderer renderer = ref renderers[entity];
                if (renderer == default)
                {
                    DescriptorSet descriptorSet = compiledPipeline.Allocate();
                    UpdateDescriptorSet(materialEntity, descriptorSet, compiledPipeline);
                    renderer = new(descriptorSet);
                }
            }

            //finally draw everything
            ref CommandBuffer commandBuffer = ref commandBuffers[currentFrame];
            commandBuffer.BindPipeline(compiledPipeline.pipeline, VkPipelineBindPoint.Graphics);
            commandBuffer.BindVertexBuffer(compiledMesh.vertexBuffer);
            commandBuffer.BindIndexBuffer(compiledMesh.indexBuffer);

            bool hasPushConstants = knownPushConstants.TryGetValue(materialEntity, out Array<CompiledPushConstant> pushConstants);
            if (hasPushConstants)
            {
                for (uint i = 0; i < renderEntities.Length; i++)
                {
                    //apply scissor
                    uint entity = renderEntities[i];
                    ref Vector4 scissor = ref scissors[entity];
                    commandBuffer.SetScissor(scissor);

                    //push constants
                    uint pushOffset = 0;
                    for (uint p = 0; p < pushConstants.Length; p++)
                    {
                        ref CompiledPushConstant pushConstant = ref pushConstants[p];
                        ComponentType componentType = pushConstant.componentType.ComponentType;
                        Allocation component = world.GetComponent(entity, componentType, out ushort componentSize);
                        commandBuffer.PushConstants(compiledPipeline.pipelineLayout, pushConstant.stageFlags, component, pushConstant.componentType.size, pushOffset);
                        pushOffset += componentSize;
                    }

                    ref CompiledRenderer renderer = ref renderers[entity];
                    commandBuffer.BindDescriptorSet(compiledPipeline.pipelineLayout, renderer.descriptorSet);
                    commandBuffer.DrawIndexed(compiledMesh.indexCount, 1, 0, 0, 0);
                }
            }
            else
            {
                for (uint i = 0; i < renderEntities.Length; i++)
                {
                    //apply scissor
                    uint entity = renderEntities[i];
                    ref Vector4 scissor = ref scissors[entity];
                    commandBuffer.SetScissor(scissor);

                    ref CompiledRenderer renderer = ref renderers[entity];
                    commandBuffer.BindDescriptorSet(compiledPipeline.pipelineLayout, renderer.descriptorSet);
                    commandBuffer.DrawIndexed(compiledMesh.indexCount, 1, 0, 0, 0);
                }
            }


            previouslyRenderedEntities.AddRange(renderEntities);
            previouslyRenderedGroups.TryAdd(new(materialEntity, meshEntity, vertexShaderEntity, fragmentShaderEntity));
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
            DisposeUnusued();
        }

        private readonly void DisposeUnusued()
        {
            bool waited = false;

            //dispose unusued buffers
            USpan<uint> toRemove = stackalloc uint[512]; //todo: this can crash if not enough space
            uint removeCount = 0;
            foreach ((uint componentHash, CompiledComponentBuffer component) in components)
            {
                bool used = false;
                foreach (RendererCombination combination in previouslyRenderedGroups)
                {
                    if (combination.material == component.materialEntity)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    toRemove[removeCount++] = componentHash;
                }
            }

            if (removeCount > 0)
            {
                if (!waited)
                {
                    waited = true;
                    logicalDevice.Wait();
                }

                for (uint i = 0; i < removeCount; i++)
                {
                    components.Remove(toRemove[i], out CompiledComponentBuffer component);
                    component.Dispose();
                }

                removeCount = 0;
            }

            //dispose unused textures
            foreach ((uint textureHash, CompiledImage image) in images)
            {
                bool used = false;
                foreach (RendererCombination combination in previouslyRenderedGroups)
                {
                    if (combination.material == image.materialEntity)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    toRemove[removeCount++] = textureHash;
                }
            }

            if (removeCount > 0)
            {
                if (!waited)
                {
                    waited = true;
                    logicalDevice.Wait();
                }

                for (uint i = 0; i < removeCount; i++)
                {
                    images.Remove(toRemove[i], out CompiledImage image);
                    image.Dispose();
                }

                removeCount = 0;
            }

            //dispose unused renderers
            for (uint e = 1; e < renderers.Count; e++)
            {
                if (renderers[e] != default)
                {
                    if (!previouslyRenderedEntities.Contains(e))
                    {
                        toRemove[removeCount++] = e;
                    }
                }
            }

            if (removeCount > 0)
            {
                if (!waited)
                {
                    waited = true;
                    logicalDevice.Wait();
                }

                for (uint i = 0; i < removeCount; i++)
                {
                    ref CompiledRenderer renderer = ref renderers[toRemove[i]];
                    renderer.Dispose();
                    renderer = default;
                }

                removeCount = 0;
            }

            //dispose unused meshes
            USpan<RendererKey> toRemoveKeys = stackalloc RendererKey[256];
            for (uint i = 0; i < meshKeys.Count; i++)
            {
                RendererKey key = meshKeys[i];
                bool used = false;
                foreach (RendererCombination combination in previouslyRenderedGroups)
                {
                    if (combination.Key == key.value)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    toRemoveKeys[removeCount++] = key;
                }
            }

            if (removeCount > 0)
            {
                if (!waited)
                {
                    waited = true;
                    logicalDevice.Wait();
                }

                for (uint i = 0; i < removeCount; i++)
                {
                    RendererKey key = toRemoveKeys[i];
                    meshes.Remove(key, out CompiledMesh mesh);
                    meshKeys.TryRemoveBySwapping(key);
                    mesh.Dispose();
                }

                removeCount = 0;
            }

            //dispose unused pipelines
            for (uint i = 0; i < pipelineKeys.Count; i++)
            {
                RendererKey key = pipelineKeys[i];
                bool used = false;
                foreach (RendererCombination combination in previouslyRenderedGroups)
                {
                    if (combination.Key == key.value)
                    {
                        used = true;
                        break;
                    }
                }

                if (!used)
                {
                    toRemoveKeys[removeCount++] = key;
                }
            }

            if (removeCount > 0)
            {
                if (!waited)
                {
                    waited = true;
                    logicalDevice.Wait();
                }

                for (uint i = 0; i < removeCount; i++)
                {
                    RendererKey key = toRemoveKeys[i];
                    pipelines.Remove(key, out CompiledPipeline pipeline);
                    pipelineKeys.TryRemoveBySwapping(key);
                    pipeline.Dispose();
                }

                removeCount = 0;
            }

            previouslyRenderedGroups.Clear();
            previouslyRenderedEntities.Clear();
        }

        private readonly void UpdateDescriptorSet(uint materialEntity, DescriptorSet descriptorSet, CompiledPipeline pipeline)
        {
            World world = destination.world;
            Material material = new Entity(world, materialEntity).As<Material>();
            byte set = 0;
            foreach (VkDescriptorSetLayoutBinding descriptorBinding in pipeline.DescriptorBindings)
            {
                byte binding = (byte)descriptorBinding.binding;
                DescriptorResourceKey key = new(binding, set);
                if (descriptorBinding.descriptorType == VkDescriptorType.CombinedImageSampler)
                {
                    TextureBinding textureBinding = material.GetTextureBinding(key);
                    uint textureHash = GetTextureHash(materialEntity, textureBinding);
                    ref CompiledImage image = ref images[textureHash];
                    descriptorSet.Update(image.imageView, image.sampler, binding);
                }
                else if (descriptorBinding.descriptorType == VkDescriptorType.UniformBuffer)
                {
                    ComponentBinding componentBinding = material.GetComponentBinding(key, ShaderType.Vertex);
                    uint componentHash = GetComponentHash(materialEntity, componentBinding);
                    ref CompiledComponentBuffer componentBuffer = ref components[componentHash];
                    descriptorSet.Update(componentBuffer.buffer.buffer, binding);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported descriptor type `{descriptorBinding.descriptorType}`");
                }
            }
        }

        private static uint GetTextureHash(uint materialEntity, TextureBinding binding)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (int)materialEntity;
                hash = hash * 23 + binding.key.GetHashCode();
                hash = hash * 23 + (int)binding.Entity;
                return (uint)hash;
            }
        }

        private static uint GetComponentHash(uint materialEntity, ComponentBinding binding)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (int)materialEntity;
                hash = hash * 23 + binding.GetHashCode();
                return (uint)hash;
            }
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

        private static bool TryDeduceMeshChannel(ShaderVertexInputAttribute attribute, out MeshChannel channel)
        {
            //get lowercase version
            USpan<char> nameBuffer = stackalloc char[FixedString.Capacity];
            uint length = attribute.name.CopyTo(nameBuffer);
            for (uint i = 0; i < length; i++)
            {
                nameBuffer[i] = char.ToLower(nameBuffer[i]);
            }

            if (attribute.Type == typeof(Vector2))
            {
                if (nameBuffer.GetSpan(length).Contains("uv".AsSpan()))
                {
                    channel = MeshChannel.UV;
                    return true;
                }
            }
            else if (attribute.Type == typeof(Vector3))
            {
                if (nameBuffer.GetSpan(length).Contains("normal".AsSpan()))
                {
                    channel = MeshChannel.Normal;
                    return true;
                }
                else if (nameBuffer.GetSpan(length).Contains("tangent".AsSpan()))
                {
                    channel = MeshChannel.Tangent;
                    return true;
                }
                else if (nameBuffer.GetSpan(length).Contains("position".AsSpan()))
                {
                    channel = MeshChannel.Position;
                    return true;
                }
                else if (nameBuffer.GetSpan(length).Contains("bitangent".AsSpan()))
                {
                    channel = MeshChannel.BiTangent;
                    return true;
                }
            }
            else if (attribute.Type == typeof(Vector4))
            {
                if (nameBuffer.GetSpan(length).Contains("color".AsSpan()))
                {
                    channel = MeshChannel.Color;
                    return true;
                }
            }

            channel = default;
            return false;
        }

        private static VkShaderStageFlags GetShaderStage(ShaderType stage)
        {
            return stage switch
            {
                ShaderType.Vertex => VkShaderStageFlags.Vertex,
                ShaderType.Fragment => VkShaderStageFlags.Fragment,
                ShaderType.Geometry => VkShaderStageFlags.Geometry,
                ShaderType.Compute => VkShaderStageFlags.Compute,
                _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
            };
        }

        private static VkFormat GetFormat(Type type)
        {
            if (type == typeof(Vector2))
            {
                return VkFormat.R32G32Sfloat;
            }
            else if (type == typeof(Vector3))
            {
                return VkFormat.R32G32B32Sfloat;
            }
            else if (type == typeof(Vector4))
            {
                return VkFormat.R32G32B32A32Sfloat;
            }
            else
            {
                throw new NotSupportedException($"Unsupported type {type}");
            }
        }
    }
}